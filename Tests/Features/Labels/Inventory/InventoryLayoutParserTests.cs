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
                    return (true, new object());
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
            entries.Should().HaveCount(1);
            entries[0].X.Should().Be(1);
            entries[0].Y.Should().Be(2);
            entries[0].Width.Should().Be(3);
            entries[0].Height.Should().Be(4);
            source.Should().Be("cache");
            debugDetails.Should().Be("cached");
            isReliable.Should().BeTrue();
            rawEntryCount.Should().Be(9);
            slotAccessorCalls.Should().Be(0);
        }

        [TestMethod]
        public void TryResolveInventoryLayoutEntries_BuildsClampedEntries_AndCachesSuccessfulSnapshot()
        {
            object firstEntry = new SlotEntryWithPositionAndSize
            {
                PosX = 3,
                PosY = 3,
                SizeX = 4,
                SizeY = 3
            };
            object secondEntry = new SlotEntryWithLocation
            {
                Location = new LocationWrapper
                {
                    InventoryPosition = new PositionWrapper
                    {
                        X = 4,
                        Y = 4
                    }
                }
            };
            Entity fallbackSizedEntity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            object primaryInventory = new();
            var layoutCache = new InventoryLayoutCache(cacheWindowMs: 50);

            var parser = CreateParser(
                layoutCache: layoutCache,
                tryGetPrimaryServerInventorySlotItems: _ => (true, new object[] { firstEntry, secondEntry }),
                enumerateObjects: collection => collection as object[] ?? Array.Empty<object?>(),
                tryGetInventoryItemEntityFromEntry: entry => ReferenceEquals(entry, secondEntry) ? fallbackSizedEntity : null,
                tryResolveInventoryItemSize: entity => ReferenceEquals(entity, fallbackSizedEntity) ? (true, 3, 4) : (false, 0, 0));

            bool success = parser.TryResolveInventoryLayoutEntries(
                primaryInventory,
                inventoryWidth: 5,
                inventoryHeight: 5,
                out IReadOnlyList<InventoryLayoutEntry> entries,
                out string source,
                out string debugDetails,
                out bool isReliable,
                out int rawEntryCount);

            success.Should().BeTrue();
            entries.Should().HaveCount(2);
            entries[0].X.Should().Be(3);
            entries[0].Y.Should().Be(3);
            entries[0].Width.Should().Be(2);
            entries[0].Height.Should().Be(2);
            entries[1].X.Should().Be(4);
            entries[1].Y.Should().Be(4);
            entries[1].Width.Should().Be(1);
            entries[1].Height.Should().Be(1);
            source.Should().Be("PlayerInventories[0].InventorySlotItems");
            debugDetails.Should().Be("raw:2 parsed:2");
            isReliable.Should().BeTrue();
            rawEntryCount.Should().Be(2);

            bool cached = layoutCache.TryGet(primaryInventory, Environment.TickCount64, 5, 5, out InventoryLayoutSnapshot capturedSnapshot);

            cached.Should().BeTrue();
            capturedSnapshot.Source.Should().Be(source);
            capturedSnapshot.DebugDetails.Should().Be(debugDetails);
            capturedSnapshot.IsReliable.Should().BeTrue();
            capturedSnapshot.RawEntryCount.Should().Be(2);
            capturedSnapshot.Entries.Should().HaveCount(2);
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