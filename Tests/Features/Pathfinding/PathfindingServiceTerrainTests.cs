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

    }
}