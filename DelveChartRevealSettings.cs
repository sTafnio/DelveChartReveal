using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace DelveChartReveal;

public class DelveChartRevealSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Reveal Subterranean Chart", "Patch the Delve cell constructor so every node the client knows about renders with its real icon. Close and reopen the chart afterwards.")]
    public ButtonNode Apply { get; set; } = new ButtonNode();

    [Menu("Apply at Launch", "Apply the reveal patch automatically when the plugin loads.")]
    public ToggleNode ApplyAtLaunch { get; set; } = new ToggleNode(false);

    [Menu("Restore Original", "Undo the patch (restores the original bytes). Close and reopen the chart afterwards.")]
    public ButtonNode Restore { get; set; } = new ButtonNode();
}
