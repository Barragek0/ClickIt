namespace ClickIt.Tests.Features.Pathfinding
{
    [TestClass]
    public class PathfindingServiceTerrainTests
    {
        [TestMethod]
        [TestCategory("Regression")]
        public void TerrainRefresh_SameShapeKeepsTheLastGoodGridUntilTheNewOwnerIsConfirmed()
        {
            PathfindingTerrainCache cache = new();
            IngameData first = ExileCoreOpaqueFactory.CreateOpaque<IngameData>();
            IngameData next = ExileCoreOpaqueFactory.CreateOpaque<IngameData>();
            int[][] firstTerrain = [[1, 0]];
            int[][] nextTerrain = [[0, 1]];

            PathTerrainSnapshotProvider.TryRefreshTerrainData(cache, first, firstTerrain, 10, 1_000,
                out bool[][] original, out _, out bool fromCache).Should().BeTrue();
            fromCache.Should().BeFalse();
            WeakReference<IngameData> owner = cache.DataOwner!;

            PathTerrainSnapshotProvider.TryRefreshTerrainData(cache, next, nextTerrain, 20, 2_000,
                out bool[][] pending, out _, out fromCache).Should().BeTrue();
            fromCache.Should().BeTrue();
            pending.Should().BeSameAs(original);
            pending[0].Should().Equal(true, false);

            PathTerrainSnapshotProvider.TryRefreshTerrainData(cache, next, nextTerrain, 20, 2_150,
                out bool[][] refreshed, out var dims, out fromCache).Should().BeTrue();
            fromCache.Should().BeFalse();
            dims.Should().Be(new PathfindingService.GridPoint(2, 1));
            refreshed.Should().BeSameAs(original);
            refreshed[0].Should().Equal(false, true);
            cache.DataOwner.Should().BeSameAs(owner);
            owner.TryGetTarget(out IngameData? tracked).Should().BeTrue();
            tracked.Should().BeSameAs(next);
            cache.DataOwnerAddress.Should().Be(20);
            GC.KeepAlive(next);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void TerrainRefresh_DifferentShapeReleasesThePreviousGridOnSuccessfulReplacement()
        {
            PathfindingTerrainCache cache = new();
            IngameData first = ExileCoreOpaqueFactory.CreateOpaque<IngameData>();
            IngameData next = ExileCoreOpaqueFactory.CreateOpaque<IngameData>();
            int[][] firstTerrain = [[1, 0]];
            object[] invalidTerrain = [new[] { 0, 0, 0 }, new[] { 0, 0 }];
            int[][] nextTerrain = [[0, 1, 1]];

            PathTerrainSnapshotProvider.TryRefreshTerrainData(cache, first, firstTerrain, 10, 1_000,
                out bool[][] original, out _, out _).Should().BeTrue();
            PathTerrainSnapshotProvider.TryRefreshTerrainData(cache, next, invalidTerrain, 20, 2_000,
                out _, out _, out _).Should().BeFalse();
            cache.Walkable.Should().BeSameAs(original);
            cache.DataOwnerAddress.Should().Be(10);

            PathTerrainSnapshotProvider.TryRefreshTerrainData(cache, next, nextTerrain, 20, 2_001,
                out bool[][] replacement, out var dims, out bool fromCache).Should().BeTrue();
            fromCache.Should().BeFalse();
            dims.Should().Be(new PathfindingService.GridPoint(3, 1));
            cache.Walkable.Should().BeSameAs(replacement).And.NotBeSameAs(original);
            replacement[0].Should().Equal(false, true, true);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void TerrainOwnerChange_RequiresContinuousConfirmationAfterAnAddressFlap()
        {
            TerrainOwnerChangeConfirmation confirmation = default;

            confirmation.ShouldWait(currentAddress: 10, observedAddress: 20, now: 1_000).Should().BeTrue();
            confirmation.ShouldWait(currentAddress: 10, observedAddress: 10, now: 1_010).Should().BeFalse();
            confirmation.ShouldWait(currentAddress: 10, observedAddress: 20, now: 1_300).Should().BeTrue();
            confirmation.ShouldWait(currentAddress: 10, observedAddress: 20, now: 1_449).Should().BeTrue();
            confirmation.ShouldWait(currentAddress: 10, observedAddress: 20, now: 1_450).Should().BeFalse();
        }

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
        public void TryGetRawGridDims_ReadsShapeWithoutConverting()
        {
            bool intOk = PathTerrainSnapshotProvider.TryGetRawGridDims(
                new[] { new[] { 1, 0, 1 }, new[] { 0, 1, 0 } }, out int intWidth, out int intHeight);
            intOk.Should().BeTrue();
            intWidth.Should().Be(3);
            intHeight.Should().Be(2);

            bool objOk = PathTerrainSnapshotProvider.TryGetRawGridDims(
                new object[] { new object[] { 1, 0 }, new object[] { 0, 1 } }, out int objWidth, out int objHeight);
            objOk.Should().BeTrue();
            objWidth.Should().Be(2);
            objHeight.Should().Be(2);

            PathTerrainSnapshotProvider.TryGetRawGridDims(null, out _, out _).Should().BeFalse();
            PathTerrainSnapshotProvider.TryGetRawGridDims(new object[0], out _, out _).Should().BeFalse();
            PathTerrainSnapshotProvider.TryGetRawGridDims(new object[] { new object[0] }, out _, out _).Should().BeFalse();
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
            // Two different maps can share the same grid dimensions; a stale cache must not be reused just because the dimensions match.
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
