using ClickIt.Utils;
using ClickIt.Services.Label.Inventory;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const string StoneOfPassageMetadataIdentifier = "Incursion/IncursionKey";

        private InventoryDomainFacade? _inventoryDomain;

        private InventoryDomainFacade InventoryDomain
            => _inventoryDomain ??= InventoryDomainComposition.Create(
                new InventoryDomainCompositionDependencies(_worldItemMetadataPolicy.GetWorldItemBaseName));

        internal InventoryDebugSnapshot GetLatestInventoryDebug() => InventoryDomain.GetLatestDebug();

        internal IReadOnlyList<string> GetLatestInventoryDebugTrail() => InventoryDomain.GetLatestDebugTrail();

        private bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            => InventoryDomain.ShouldAllowWorldItemWhenInventoryFull(groundItem, gameController);

        private bool ShouldAllowClosedDoorPastMechanic(GameController? gameController)
            => InventoryDomain.ShouldAllowClosedDoorPastMechanic(gameController, StoneOfPassageMetadataIdentifier);

        internal static bool HasSpaceForItemFootprintCore(
            int inventoryWidth,
            int inventoryHeight,
            IReadOnlyList<InventoryLayoutEntry> occupiedEntries,
            int requiredWidth,
            int requiredHeight)
            => InventoryCapacityEngine.HasSpaceForItemFootprint(
                inventoryWidth,
                inventoryHeight,
                occupiedEntries,
                requiredWidth,
                requiredHeight);

        private static bool TryResolveInventoryItemSize(Entity itemEntity, out int width, out int height)
            => InventoryCoreLogic.TryResolveInventoryItemSize(itemEntity, out width, out height);

        internal static bool TryResolveInventoryItemSizeFromBase(object? baseComponent, out int width, out int height)
            => InventoryCoreLogic.TryResolveInventoryItemSizeFromBase(baseComponent, out width, out height);

        internal static bool TryResolveFallbackInventoryItemSizeFromPathCore(string? metadataPath, out int width, out int height)
            => InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath(metadataPath, out width, out height);

        internal static bool ShouldAllowIncubatorStackMatchCore(
            bool requiresIncubatorLevelMatch,
            bool hasGroundIncubatorLevel,
            int groundIncubatorLevel,
            bool hasInventoryIncubatorLevel,
            int inventoryIncubatorLevel)
            => InventoryStackingEngine.ShouldAllowIncubatorStackMatch(
                requiresIncubatorLevelMatch,
                hasGroundIncubatorLevel,
                groundIncubatorLevel,
                hasInventoryIncubatorLevel,
                inventoryIncubatorLevel);

        internal void ClearInventoryProbeCacheForShutdown()
            => InventoryDomain.ClearForShutdown();
    }
}