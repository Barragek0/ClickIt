namespace ClickIt.Features.Click.Ranking
{
    internal sealed class MechanicPriorityContextProvider(
        ClickItSettings settings,
        IMechanicPrioritySnapshotProvider snapshotProvider)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly IMechanicPrioritySnapshotProvider _snapshotProvider = snapshotProvider;

        public MechanicPriorityContext CreateContext()
        {
            MechanicPrioritySnapshot snapshot = _snapshotProvider.Snapshot;
            return new(
                snapshot.PriorityIndexMap,
                snapshot.IgnoreDistanceSet,
                snapshot.IgnoreDistanceWithinByMechanicId,
                _settings.MechanicPriorityDistancePenalty.Value);
        }

        public void Refresh()
        {
            _ = _snapshotProvider.Refresh(
                _settings.GetMechanicPriorityOrder(),
                _settings.GetMechanicPriorityIgnoreDistanceIds(),
                _settings.GetMechanicPriorityIgnoreDistanceWithinById());
        }
    }
}