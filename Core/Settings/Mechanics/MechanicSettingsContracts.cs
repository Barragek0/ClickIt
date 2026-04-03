using ExileCore.Shared.Nodes;

namespace ClickIt.Core.Settings.Mechanics
{
    internal sealed record MechanicToggleGroupEntry(string Id, string DisplayName);

    internal sealed record MechanicToggleTableEntry(
        string Id,
        string DisplayName,
        ToggleNode Node,
        string? GroupId = null,
        bool DefaultEnabled = false,
        string? Subgroup = null);
}