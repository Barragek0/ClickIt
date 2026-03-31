using ClickIt.Utils;

namespace ClickIt.Services.Label.Inventory
{
    internal sealed class InventoryDiagnosticsChannel(int trailCapacity)
    {
        private readonly DebugSnapshotStore<LabelFilterService.InventoryDebugSnapshot> _store = new(
            LabelFilterService.InventoryDebugSnapshot.Empty,
            trailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot =>
                $"{snapshot.Sequence:00000} {snapshot.Stage} | f:{snapshot.InventoryFull} a:{snapshot.DecisionAllowPickup} c:{snapshot.CapacityCells} o:{snapshot.OccupiedCells} s:{snapshot.IsGroundStackable} p:{snapshot.HasPartialMatchingStack} n:{snapshot.Notes}");

        public LabelFilterService.InventoryDebugSnapshot GetLatest() => _store.GetLatest();

        public IReadOnlyList<string> GetTrail() => _store.GetTrail();

        public void Publish(LabelFilterService.InventoryDebugSnapshot snapshot) => _store.SetLatest(snapshot);
    }
}