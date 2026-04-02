using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Label.Inventory
{
    internal readonly record struct InventoryStackMatchDependencies(
        Func<GameController?, (bool Success, IReadOnlyList<Entity> Items)> TryEnumerateInventoryItemEntities);

    internal sealed class InventoryStackMatchService(InventoryStackMatchDependencies dependencies)
    {
        private readonly InventoryStackMatchDependencies _dependencies = dependencies;

        public (bool HasPartialMatchingStack, int MatchingPathCount, int PartialMatchingStackCount) HasMatchingPartialStackInInventory(
            string? worldItemPath,
            Entity? groundItemEntity,
            GameController? gameController)
        {
            int matchingPathCount = 0;
            int partialMatchingStackCount = 0;

            if (string.IsNullOrWhiteSpace(worldItemPath))
                return (false, matchingPathCount, partialMatchingStackCount);

            (bool success, IReadOnlyList<Entity> inventoryItems) = _dependencies.TryEnumerateInventoryItemEntities(gameController);
            if (!success)
                return (false, matchingPathCount, partialMatchingStackCount);

            bool requiresIncubatorLevelMatch = IsIncubatorPath(worldItemPath);
            bool hasGroundIncubatorLevel = TryResolveCurrencyItemLevel(groundItemEntity, out int groundIncubatorLevel);

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity inventoryItem = inventoryItems[i];
                if (inventoryItem == null)
                    continue;

                string inventoryPath = inventoryItem.Path ?? string.Empty;
                if (!inventoryPath.Equals(worldItemPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                matchingPathCount++;

                bool hasInventoryIncubatorLevel = TryResolveCurrencyItemLevel(inventoryItem, out int inventoryIncubatorLevel);
                if (!InventoryStackingEngine.ShouldAllowIncubatorStackMatch(
                    requiresIncubatorLevelMatch,
                    hasGroundIncubatorLevel,
                    groundIncubatorLevel,
                    hasInventoryIncubatorLevel,
                    inventoryIncubatorLevel))
                {
                    continue;
                }

                if (TryResolveServerStackState(inventoryItem, out bool fullStack, out int stackSize)
                    && InventoryStackingEngine.IsPartialServerStack(fullStack, stackSize))
                {
                    partialMatchingStackCount++;
                    return (true, matchingPathCount, partialMatchingStackCount);
                }
            }

            return (false, matchingPathCount, partialMatchingStackCount);
        }

        internal static bool IsGroundItemStackable(Entity? itemEntity)
            => itemEntity != null && TryResolveServerStackState(itemEntity, out _, out _);

        private static bool IsIncubatorPath(string? metadataPath)
            => !string.IsNullOrWhiteSpace(metadataPath)
               && metadataPath.IndexOf("Incubation", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool TryResolveCurrencyItemLevel(Entity? itemEntity, out int currencyItemLevel)
        {
            currencyItemLevel = 0;
            if (itemEntity == null)
                return false;

            try
            {
                Base? baseComponent = itemEntity.GetComponent<Base>();
                if (baseComponent == null)
                    return false;

                if (DynamicObjectAdapter.TryReadInt(baseComponent, static s => s.CurrencyItemLevel, out currencyItemLevel))
                    return currencyItemLevel > 0;

                currencyItemLevel = baseComponent.CurrencyItemLevel;
                return currencyItemLevel > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveServerStackState(Entity itemEntity, out bool fullStack, out int stackSize)
        {
            fullStack = false;
            stackSize = 0;

            try
            {
                object? stack = itemEntity.GetComponent<Stack>();
                bool hasFullFlag = TryReadStackFullFlag(stack, out fullStack);
                bool hasSize = TryReadStackSize(stack, out stackSize);
                return hasFullFlag && hasSize && stackSize > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadStackFullFlag(object? stack, out bool fullStack)
            => DynamicObjectAdapter.TryReadBool(stack, static s => s.FullStack, out fullStack)
               || DynamicObjectAdapter.TryReadBool(stack, static s => s.IsFull, out fullStack)
               || DynamicObjectAdapter.TryReadBool(stack, static s => s.Full, out fullStack);

        private static bool TryReadStackSize(object? stack, out int stackSize)
            => DynamicObjectAdapter.TryReadInt(stack, static s => s.Size, out stackSize)
               || DynamicObjectAdapter.TryReadInt(stack, static s => s.Count, out stackSize)
               || DynamicObjectAdapter.TryReadInt(stack, static s => s.StackSize, out stackSize)
               || DynamicObjectAdapter.TryReadInt(stack, static s => s.Amount, out stackSize);
    }
}