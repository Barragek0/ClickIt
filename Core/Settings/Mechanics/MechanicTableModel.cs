namespace ClickIt
{
    public partial class ClickItSettings
    {
        internal static bool ShouldRenderMechanicEntry(MechanicToggleTableEntry entry, bool moveToClick, string filter)
            => MechanicTableModelService.ShouldRenderEntry(entry, moveToClick, filter);

        internal static bool ShouldRenderMechanicGroup(MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries, bool moveToClick, string filter)
            => MechanicTableModelService.ShouldRenderGroup(group, entries, moveToClick, filter);

        internal static void SetMechanicGroupState(string groupId, IReadOnlyList<MechanicToggleTableEntry> entries, bool enabled)
            => MechanicTableModelService.SetGroupState(groupId, entries, enabled);

        internal IReadOnlyList<MechanicToggleTableEntry> GetMechanicTableEntries()
            => MechanicTableModelService.GetTableEntries(this);
    }
}