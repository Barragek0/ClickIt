namespace ClickIt.Services.Mechanics
{
    internal interface IMechanicPrioritySnapshotProvider
    {
        MechanicPrioritySnapshot Refresh(
            IReadOnlyList<string> mechanicPriorities,
            IReadOnlyCollection<string> ignoreDistance,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId);

        MechanicPrioritySnapshot Snapshot { get; }
    }
}