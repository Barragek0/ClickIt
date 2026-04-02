using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ClickIt.Tests.Services.Pathfinding
{
    [TestClass]
    public class PathfindingServiceTerrainTests
    {
        [TestMethod]
        public void TryConvertPathfindingData_ReturnsFalse_ForNullAndEmpty()
        {
            bool okNull = PathfindingService.TryConvertPathfindingData(null, out _);
            bool okEmpty = PathfindingService.TryConvertPathfindingData(Array.Empty<object>(), out _);

            okNull.Should().BeFalse();
            okEmpty.Should().BeFalse();
        }

        [TestMethod]
        public void TryConvertPathfindingData_AcceptsDirectIntGrid()
        {
            int[][] raw =
            [
                [1, 0],
                [0, 1]
            ];

            bool ok = PathfindingService.TryConvertPathfindingData(raw, out int[][]? converted);

            ok.Should().BeTrue();
            converted.Should().NotBeNull();
            converted!.Length.Should().Be(2);
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

            bool okGood = PathfindingService.TryConvertPathfindingData(good, out int[][]? goodGrid);
            bool okBad = PathfindingService.TryConvertPathfindingData(bad, out _);

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
            bool intRowOk = PathfindingService.TryConvertRow(new[] { 1, 2, 3 }, out int[]? intRow);
            bool objRowOk = PathfindingService.TryConvertRow(new object[] { 4, 5, 6 }, out int[]? objRow);
            bool badRowOk = PathfindingService.TryConvertRow(new object[] { 1, "x" }, out _);
            bool nullValueOk = PathfindingService.TryConvertRow(new object?[] { 1, (object?)null }, out _);

            intRowOk.Should().BeTrue();
            objRowOk.Should().BeTrue();
            badRowOk.Should().BeFalse();
            nullValueOk.Should().BeFalse();
            intRow.Should().Equal(1, 2, 3);
            objRow.Should().Equal(4, 5, 6);
        }

        [TestMethod]
        public void ConvertRawGridToWalkable_ConvertsPositiveValuesToTrue()
        {
            int[][] raw =
            [
                [1, 0, -1],
                [2, 3, 0]
            ];

            bool[][] walkable = PathfindingService.ConvertRawGridToWalkable(raw);

            walkable.Should().BeEquivalentTo(new[]
            {
                new[] { true, false, false },
                new[] { true, true, false }
            });
        }

        [TestMethod]
        public void ResolveScale_UsesFallbackWhenGridDeltaIsNearZero_AndMinimumWhenTooSmall()
        {
            float nearZeroGrid = PathfindingService.ResolveScale(10f, 0f);
            float tooSmallScale = PathfindingService.ResolveScale(0.0001f, 1f);
            float regularScale = PathfindingService.ResolveScale(20f, 4f);

            nearZeroGrid.Should().Be(2.5f);
            tooSmallScale.Should().Be(2.5f);
            regularScale.Should().Be(5f);
        }

        [TestMethod]
        public void IsFinitePoint_ReturnsExpectedForFiniteAndNonFiniteValues()
        {
            PathfindingService.IsFinitePoint(1f, 2f).Should().BeTrue();
            PathfindingService.IsFinitePoint(float.NaN, 2f).Should().BeFalse();
            PathfindingService.IsFinitePoint(1f, float.PositiveInfinity).Should().BeFalse();
            PathfindingService.IsFinitePoint(float.NegativeInfinity, float.NaN).Should().BeFalse();
        }
    }
}