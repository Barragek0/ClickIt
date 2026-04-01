using ClickIt.Definitions;
using ClickIt.Services.Mechanics;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal const string ShrineMechanicId = MechanicIds.Shrines;
        internal const string LostShipmentMechanicId = MechanicIds.LostShipment;
        internal const int HiddenFallbackCandidateCacheWindowMs = 150;
        internal const int VisibleMechanicCandidateCacheWindowMs = 80;
        internal const int GroundLabelEntityAddressCacheWindowMs = 150;

        private readonly IMechanicPrioritySnapshotProvider _mechanicPrioritySnapshotService = new MechanicPrioritySnapshotService();

        private MechanicPriorityContext CreateMechanicPriorityContext()
        {
            MechanicPrioritySnapshot snapshot = _mechanicPrioritySnapshotService.Snapshot;
            return new(
                snapshot.PriorityIndexMap,
                snapshot.IgnoreDistanceSet,
                snapshot.IgnoreDistanceWithinByMechanicId,
                settings.MechanicPriorityDistancePenalty.Value);
        }

        private void RefreshMechanicPriorityCaches()
        {
            _ = _mechanicPrioritySnapshotService.Refresh(
                settings.GetMechanicPriorityOrder(),
                settings.GetMechanicPriorityIgnoreDistanceIds(),
                settings.GetMechanicPriorityIgnoreDistanceWithinById());
        }
    }
}