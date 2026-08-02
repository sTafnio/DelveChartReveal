using ExileCore;
using System;
using System.Runtime.InteropServices;

namespace DelveChartReveal;

public class DelveChartReveal : BaseSettingsPlugin<DelveChartRevealSettings>
{
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint size, out int lpNumberOfBytesWritten);

    /*
        Delve cell constructor. r12 is always zero here, so the original code initializes
        the cell's Visible qword to 0; rax holds a non-zero value at this point, so
        swapping the source register marks every constructed cell visible. Only the three
        opcode/register bytes change - the displacement encoded by the game is preserved,
        so a struct layout shift does not break the patch.

            41 88 86 ? ? ? ?    mov [r14+HasEncounter], al
            4D 89 A6 ? ? ? ?    mov [r14+Visible], r12     -> 49 89 86 (mov [r14+Visible], rax)
            45 89 A6 ? ? ? ?    mov [r14+...], r12d
            49 8D 8E ? ? ? ?    lea rcx, [r14+...]
    */
    const string UnpatchedSig = "41 88 86 ? ? ? ? 4D 89 A6 ? ? ? ? 45 89 A6 ? ? ? ? 49 8D 8E";
    const string PatchedSig = "41 88 86 ? ? ? ? 49 89 86 ? ? ? ? 45 89 A6 ? ? ? ? 49 8D 8E";
    const int PatchOffset = 7;
    static readonly byte[] PatchBytes = { 0x49, 0x89, 0x86 };
    static readonly byte[] OriginalBytes = { 0x4D, 0x89, 0xA6 };

    IntPtr processHandle = IntPtr.Zero;
    SigScanSharp SigScan;

    private bool InitializeProcess()
    {
        if (processHandle != IntPtr.Zero) return true;

        processHandle = OpenProcess(0x1F0FFF, false, GameController.Memory.Process.Id);
        if (processHandle == IntPtr.Zero)
        {
            DebugWindow.LogError("DelveChartReveal: failed to open process.");
            return false;
        }

        SigScan = new SigScanSharp(processHandle);
        return true;
    }

    private IntPtr FindSignature(string pattern)
    {
        // Re-read the module every time so the scan sees the current (possibly patched) bytes
        if (!SigScan.SelectModule(GameController.Memory.Process.Modules[0]))
        {
            DebugWindow.LogError("DelveChartReveal: failed to read game module.");
            return IntPtr.Zero;
        }

        return (nint)SigScan.FindPattern(pattern, out _);
    }

    private bool WriteBytes(IntPtr address, byte[] bytes)
    {
        if (!WriteProcessMemory(processHandle, address, bytes, (uint)bytes.Length, out _))
        {
            DebugWindow.LogError($"DelveChartReveal: failed to write memory. Error: {Marshal.GetLastWin32Error()}");
            return false;
        }

        var verify = new byte[bytes.Length];
        if (!ReadProcessMemory(processHandle, address, verify, (uint)verify.Length, out _)
            || !((ReadOnlySpan<byte>)verify).SequenceEqual(bytes))
        {
            DebugWindow.LogError("DelveChartReveal: write verification failed.");
            return false;
        }

        return true;
    }

    private void ApplyPatch()
    {
        if (!InitializeProcess()) return;

        var address = FindSignature(UnpatchedSig);
        if (address == IntPtr.Zero)
        {
            if (FindSignature(PatchedSig) != IntPtr.Zero)
            {
                DebugWindow.LogMsg("DelveChartReveal: patch is already applied.");
                return;
            }

            DebugWindow.LogError("DelveChartReveal: signature not found - the game may have been updated.");
            return;
        }

        if (WriteBytes(IntPtr.Add(address, PatchOffset), PatchBytes))
        {
            DebugWindow.LogMsg("DelveChartReveal: patch applied - close and reopen the Subterranean Chart.");
        }
    }

    private void RestorePatch()
    {
        if (!InitializeProcess()) return;

        var address = FindSignature(PatchedSig);
        if (address == IntPtr.Zero)
        {
            if (FindSignature(UnpatchedSig) != IntPtr.Zero)
            {
                DebugWindow.LogMsg("DelveChartReveal: patch is not applied, nothing to restore.");
                return;
            }

            DebugWindow.LogError("DelveChartReveal: signature not found - the game may have been updated.");
            return;
        }

        if (WriteBytes(IntPtr.Add(address, PatchOffset), OriginalBytes))
        {
            DebugWindow.LogMsg("DelveChartReveal: original bytes restored - close and reopen the Subterranean Chart.");
        }
    }

    public override void OnLoad()
    {
        Settings.Apply.OnPressed = ApplyPatch;
        Settings.Restore.OnPressed = RestorePatch;

        if (Settings.ApplyAtLaunch)
        {
            ApplyPatch();
        }
    }
}
