namespace ClickIt.Tests.Features.Labels.Inventory
{
    [TestClass]
    public class InventoryLayoutEntryResolverTests
    {
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
        public void TryBuildInventoryLayoutEntriesFromCollection_BuildsClampedEntries_UsingLocationFallbackAndEntitySize()
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
            var resolver = CreateResolver(
                enumerateObjects: collection => collection as object[] ?? Array.Empty<object?>(),
                tryGetInventoryItemEntityFromEntry: entry => ReferenceEquals(entry, secondEntry) ? fallbackSizedEntity : null,
                tryResolveInventoryItemSize: entity => ReferenceEquals(entity, fallbackSizedEntity) ? (true, 3, 4) : (false, 0, 0));

            bool success = resolver.TryBuildInventoryLayoutEntriesFromCollection(
                new object[] { firstEntry, secondEntry },
                inventoryWidth: 5,
                inventoryHeight: 5,
                out List<InventoryLayoutEntry> entries,
                out int rawEntryCount);

            success.Should().BeTrue();
            rawEntryCount.Should().Be(2);
            entries.Should().HaveCount(2);
            entries[0].X.Should().Be(3);
            entries[0].Y.Should().Be(3);
            entries[0].Width.Should().Be(2);
            entries[0].Height.Should().Be(2);
            entries[1].X.Should().Be(4);
            entries[1].Y.Should().Be(4);
            entries[1].Width.Should().Be(1);
            entries[1].Height.Should().Be(1);
        }

        [TestMethod]
        public void TryResolveInventoryEntrySize_FallsBackToEntitySize_WhenEntrySizeFieldsAreMissing()
        {
            Entity entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            var resolver = CreateResolver(
                tryResolveInventoryItemSize: resolved => ReferenceEquals(resolved, entity) ? (true, 2, 4) : (false, 0, 0));

            bool success = resolver.TryResolveInventoryEntrySize(new SlotEntryWithPositionOnly { PosX = 1, PosY = 2 }, entity, out int width, out int height);

            success.Should().BeTrue();
            width.Should().Be(2);
            height.Should().Be(4);
        }

        private static InventoryLayoutEntryResolver CreateResolver(
            Func<object?, IEnumerable<object?>>? enumerateObjects = null,
            Func<object, Entity?>? tryGetInventoryItemEntityFromEntry = null,
            Func<Entity, (bool Success, int Width, int Height)>? tryResolveInventoryItemSize = null,
            Func<object?, Func<dynamic, object?>, (bool Success, object? Value)>? tryGetDynamicValue = null,
            Func<object?, Func<dynamic, object?>, (bool Success, int Value)>? tryReadInt = null)
        {
            return new InventoryLayoutEntryResolver(new InventoryLayoutEntryResolverDependencies(
                EnumerateObjects: enumerateObjects ?? InventoryDynamicAccess.EnumerateObjects,
                TryGetInventoryItemEntityFromEntry: tryGetInventoryItemEntityFromEntry ?? InventoryDynamicAccess.TryGetInventoryItemEntityFromEntry,
                TryResolveInventoryItemSize: tryResolveInventoryItemSize ?? (_ => (false, 0, 0)),
                TryGetDynamicValue: tryGetDynamicValue ?? InventoryDynamicAccess.TryGetDynamicValueResult,
                TryReadInt: tryReadInt ?? InventoryDynamicAccess.TryReadIntResult));
        }
    }
}