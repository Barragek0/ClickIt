namespace ClickIt.Features.Labels.Inventory
{
    internal interface IInventorySnapshotProvider
    {
        bool TryBuild(GameController? gameController, out InventorySnapshot snapshot);
    }
}