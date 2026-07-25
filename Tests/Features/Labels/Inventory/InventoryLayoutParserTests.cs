namespace ClickIt.Tests.Features.Labels.Inventory
{
    [TestClass]
    public class InventoryLayoutParserTests
    {
        public sealed class InventoryWithDimensions
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int TotalBoxes { get; set; }
            public int Capacity { get; set; }
        }

        public sealed class InventoryWithTotalBoxes
        {
            public int TotalBoxes { get; set; }
        }

        public sealed class SlotEntryWithPositionAndSize
        {
            public int PosX { get; set; }
            public int PosY { get; set; }
            public int SizeX { get; set; }
            public int SizeY { get; set; }
        }

        public sealed class PositionWrapper
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public sealed class LocationWrapper
        {
            public PositionWrapper InventoryPosition { get; set; } = new();
        }

        public sealed class SlotEntryWithLocation
        {
            public LocationWrapper Location { get; set; } = new();
        }

        public sealed class SlotEntryWithPositionOnly
        {
            public int PosX { get; set; }
            public int PosY { get; set; }
        }

        [TestMethod]
        public void TryResolveInventoryCapacity_PrefersWidthAndHeightOverFallbackProperties()
        {
            var parser = CreateParser();
            var primaryInventory = new InventoryWithDimensions
            {
                Width = 12,
                Height = 5,
                TotalBoxes = 77,
                Capacity = 88
            };

            bool success = parser.TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity);

            success.Should().BeTrue();
            totalCellCapacity.Should().Be(60);
        }

        [TestMethod]
        public void TryResolveInventoryDimensions_UsesTotalBoxesFallback_WhenWidthAndHeightUnavailable()
        {
            var parser = CreateParser();
            var primaryInventory = new InventoryWithTotalBoxes
            {
                TotalBoxes = 48
            };

            bool success = parser.TryResolveInventoryDimensions(primaryInventory, out int width, out int height);

            success.Should().BeTrue();
            width.Should().Be(12);
            height.Should().Be(4);
        }

        [TestMethod]
        public void TryResolveInventoryLayoutEntries_ReturnsCachedSnapshot_WithoutTouchingLiveSlotItems()
        {
            InventoryLayoutSnapshot expected = new(
                Entries: [new InventoryLayoutEntry(1, 2, 3, 4)],
                Source: "cache",
                DebugDetails: "cached",
                IsReliable: true,
                RawEntryCount: 9);
            int slotAccessorCalls = 0;
            object primaryInventory = new();
            var layoutCache = new InventoryLayoutCache(cacheWindowMs: 50);
            layoutCache.Set(primaryInventory, Environment.TickCount64, 12, 5, expected);

            var parser = CreateParser(
                layoutCache: layoutCache,
                tryGetPrimaryServerInventorySlotItems: _ =>
                {
                    slotAccessorCalls++;
                    return (true, new object[] { new object() });
                });

            bool success = parser.TryResolveInventoryLayoutEntries(
                primaryInventory,
                inventoryWidth: 12,
                inventoryHeight: 5,
                out IReadOnlyList<InventoryLayoutEntry> entries,
                out string source,
                out string debugDetails,
                out bool isReliable,
                out int rawEntryCount);

            success.Should().BeTrue();
            slotAccessorCalls.Should().Be(0);
            entries.Should().Equal(expected.Entries);
            source.Should().Be(expected.Source);
            debugDetails.Should().Be(expected.DebugDetails);
            isReliable.Should().BeTrue();
            rawEntryCount.Should().Be(expected.RawEntryCount);
        }

        [TestMethod]
        public void TryResolveInventoryLayoutEntries_ReturnsFailureAndCachesReadFailedSnapshot_WhenSlotItemsUnavailable()
        {
            object primaryInventory = new();
            var layoutCache = new InventoryLayoutCache(cacheWindowMs: 50);
            var parser = CreateParser(
                layoutCache: layoutCache,
                tryGetPrimaryServerInventorySlotItems: _ => (false, null));

            bool success = parser.TryResolveInventoryLayoutEntries(
                primaryInventory,
                inventoryWidth: 12,
                inventoryHeight: 5,
                out IReadOnlyList<InventoryLayoutEntry> entries,
                out string source,
                out string debugDetails,
                out bool isReliable,
                out int rawEntryCount);

            success.Should().BeFalse();
            entries.Should().BeEmpty();
            source.Should().Be("PlayerInventories[0].InventorySlotItems");
            debugDetails.Should().Contain("read-failed");
            isReliable.Should().BeFalse();
            rawEntryCount.Should().Be(0);

            bool cached = layoutCache.TryGet(primaryInventory, Environment.TickCount64, 12, 5, out InventoryLayoutSnapshot capturedSnapshot);

            cached.Should().BeTrue();
            capturedSnapshot.Entries.Should().BeEmpty();
            capturedSnapshot.Source.Should().Be(source);
            capturedSnapshot.DebugDetails.Should().Be(debugDetails);
            capturedSnapshot.IsReliable.Should().BeFalse();
            capturedSnapshot.RawEntryCount.Should().Be(0);
        }

        private static InventoryLayoutParser CreateParser(
            InventoryLayoutCache? layoutCache = null,
            Func<object?, IEnumerable<object?>>? enumerateObjects = null,
            Func<object, (bool Success, object? SlotItemsCollection)>? tryGetPrimaryServerInventorySlotItems = null,
            Func<object, Entity?>? tryGetInventoryItemEntityFromEntry = null,
            Func<Entity, (bool Success, int Width, int Height)>? tryResolveInventoryItemSize = null,
            Func<object?, Func<dynamic, object?>, (bool Success, object? Value)>? tryGetDynamicValue = null,
            Func<object?, Func<dynamic, object?>, (bool Success, int Value)>? tryReadInt = null)
        {
            var entryResolver = new InventoryLayoutEntryResolver(new InventoryLayoutEntryResolverDependencies(
                EnumerateObjects: enumerateObjects ?? InventoryDynamicAccess.EnumerateObjects,
                TryGetInventoryItemEntityFromEntry: tryGetInventoryItemEntityFromEntry ?? InventoryDynamicAccess.TryGetInventoryItemEntityFromEntry,
                TryResolveInventoryItemSize: tryResolveInventoryItemSize ?? (_ => (false, 0, 0)),
                TryGetDynamicValue: tryGetDynamicValue ?? InventoryDynamicAccess.TryGetDynamicValueResult,
                TryReadInt: tryReadInt ?? InventoryDynamicAccess.TryReadIntResult));

            return new InventoryLayoutParser(new InventoryLayoutParserDependencies(
                TryGetPrimaryServerInventorySlotItems: tryGetPrimaryServerInventorySlotItems ?? (_ => (false, null)),
                LayoutCache: layoutCache ?? new InventoryLayoutCache(cacheWindowMs: 50),
                EntryResolver: entryResolver,
                TryReadInt: tryReadInt ?? InventoryDynamicAccess.TryReadIntResult));
        }
    }
}