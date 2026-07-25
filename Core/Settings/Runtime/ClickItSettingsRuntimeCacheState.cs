namespace ClickIt.Core.Settings.Runtime
{
    internal sealed class ClickItSettingsRuntimeCacheState
    {
        internal string[] UltimatumPrioritySnapshot { get; set; } = [];
        internal string[] MechanicPrioritySnapshot { get; set; } = [];
        internal string[] MechanicIgnoreDistanceSnapshot { get; set; } = [];
        internal KeyValuePair<string, int>[] MechanicIgnoreDistanceWithinSnapshot { get; set; } = [];
        internal IReadOnlyDictionary<string, int> MechanicIgnoreDistanceWithinMapSnapshot { get; set; } = new Dictionary<string, int>(ClickItSettings.PriorityComparer);
        internal MechanicToggleTableEntry[]? MechanicTableEntriesCache { get; set; }
        internal Dictionary<string, ToggleNode>? MechanicToggleNodeByIdCache { get; set; }
        internal int ItemTypeMetadataSnapshotSignature { get; set; } = int.MinValue;
        internal string[] ItemTypeWhitelistMetadataSnapshot { get; set; } = [];
        internal string[] ItemTypeBlacklistMetadataSnapshot { get; set; } = [];
        internal int StrongboxMetadataSnapshotSignature { get; set; } = int.MinValue;
        internal string[] StrongboxClickMetadataSnapshot { get; set; } = [];
        internal string[] StrongboxDontClickMetadataSnapshot { get; set; } = [];
    }
}