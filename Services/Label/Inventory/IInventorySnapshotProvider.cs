using ExileCore;

namespace ClickIt.Services.Label.Inventory
{
    internal interface IInventorySnapshotProvider
    {
        bool TryBuild(GameController? gameController, out InventorySnapshot snapshot);
    }
}