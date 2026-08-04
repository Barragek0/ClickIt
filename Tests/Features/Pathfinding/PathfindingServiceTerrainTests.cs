namespace ClickIt.Tests.Features.Pathfinding
{
    [TestClass]
    public class PathfindingServiceTerrainTests
    {
        [TestMethod]
        public void TryConvertPathfindingData_ConvertsArrayRows_AndRejectsRaggedRows()
        {
            object good = new object[]
            {
                new object[] { 1, 0, 2 },
                new object[] { 3, 4, 5 }
            };

            object bad = new object[]
            {
                new object[] { 1, 2, 3 },
                new object[] { 4, 5 }
            };

            bool okGood = PathTerrainSnapshotProvider.TryConvertPathfindingData(good, out int[][]? goodGrid);
            bool okBad = PathTerrainSnapshotProvider.TryConvertPathfindingData(bad, out _);

            okGood.Should().BeTrue();
            okBad.Should().BeFalse();
            goodGrid.Should().BeEquivalentTo(new[]
            {
                new[] { 1, 0, 2 },
                new[] { 3, 4, 5 }
            });
        }

        [TestMethod]
        public void TryConvertRow_ParsesNumericArrays_AndRejectsInvalidValues()
        {
            bool intRowOk = PathTerrainSnapshotProvider.TryConvertRow(new[] { 1, 2, 3 }, out int[]? intRow);
            bool objRowOk = PathTerrainSnapshotProvider.TryConvertRow(new object[] { 4, 5, 6 }, out int[]? objRow);
            bool badRowOk = PathTerrainSnapshotProvider.TryConvertRow(new object[] { 1, "x" }, out _);
            bool nullValueOk = PathTerrainSnapshotProvider.TryConvertRow(new object?[] { 1, (object?)null }, out _);

            intRowOk.Should().BeTrue();
            objRowOk.Should().BeTrue();
            badRowOk.Should().BeFalse();
            nullValueOk.Should().BeFalse();
            intRow.Should().Equal(1, 2, 3);
            objRow.Should().Equal(4, 5, 6);
        }

        [TestMethod]
        public void ShouldUseTerrainCache_ReturnsTrue_WhenDimsAndAreaHashMatch()
        {
            PathfindingTerrainCache cache = new()
            {
                AreaDims = new Vector2i { X = 1242, Y = 1242 },
                AreaHash = 42,
                Walkable = new bool[1][]
            };

            bool result = PathTerrainSnapshotProvider.ShouldUseTerrainCache(
                cache, new Vector2i { X = 1242, Y = 1242 }, 42, cacheableArea: true, hasAreaHash: true);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldUseTerrainCache_ReturnsFalse_WhenDimsMatchButAreaHashDiffers()
        {
            // Two different maps can share the same grid dimensions; a stale cache must not be
            // reused just because the dimensions match.
            PathfindingTerrainCache cache = new()
            {
                AreaDims = new Vector2i { X = 1242, Y = 1242 },
                AreaHash = 42,
                Walkable = new bool[1][]
            };

            bool result = PathTerrainSnapshotProvider.ShouldUseTerrainCache(
                cache, new Vector2i { X = 1242, Y = 1242 }, 43, cacheableArea: true, hasAreaHash: true);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldUseTerrainCache_ReturnsFalse_WhenAreaHashIsUnavailable()
        {
            PathfindingTerrainCache cache = new()
            {
                AreaDims = new Vector2i { X = 1242, Y = 1242 },
                AreaHash = 42,
                Walkable = new bool[1][]
            };

            bool result = PathTerrainSnapshotProvider.ShouldUseTerrainCache(
                cache, new Vector2i { X = 1242, Y = 1242 }, 42, cacheableArea: true, hasAreaHash: false);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldUseTerrainCache_ReturnsFalse_WhenDimsDiffer()
        {
            PathfindingTerrainCache cache = new()
            {
                AreaDims = new Vector2i { X = 1242, Y = 1242 },
                AreaHash = 42,
                Walkable = new bool[1][]
            };

            bool result = PathTerrainSnapshotProvider.ShouldUseTerrainCache(
                cache, new Vector2i { X = 900, Y = 900 }, 42, cacheableArea: true, hasAreaHash: true);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldUseTerrainCache_ReturnsFalse_WhenNoCachedGridOrAreaNotCacheable()
        {
            PathfindingTerrainCache empty = new();

            bool noGrid = PathTerrainSnapshotProvider.ShouldUseTerrainCache(
                empty, new Vector2i { X = 1242, Y = 1242 }, 42, cacheableArea: true, hasAreaHash: true);
            bool noArea = PathTerrainSnapshotProvider.ShouldUseTerrainCache(
                empty, new Vector2i { X = 0, Y = 0 }, 42, cacheableArea: false, hasAreaHash: true);

            noGrid.Should().BeFalse();
            noArea.Should().BeFalse();
        }

    }
}
