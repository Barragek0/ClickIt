using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class PathfindingServiceTerrainTests
    {
        [TestMethod]
        public void TryConvertPathfindingData_ReturnsFalse_ForNullAndEmpty()
        {
            bool okNull = InvokeStatic<bool>("TryConvertPathfindingData", (object?)null, (object?)null);

            object?[] outArgs = [Array.Empty<object>(), null];
            bool okEmpty = InvokeStatic<bool>("TryConvertPathfindingData", outArgs);

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

            object?[] args = [raw, null];
            bool ok = InvokeStatic<bool>("TryConvertPathfindingData", args);

            ok.Should().BeTrue();
            args[1].Should().NotBeNull();
            ((int[][])args[1]!).Length.Should().Be(2);
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

            object?[] goodArgs = [good, null];
            object?[] badArgs = [bad, null];

            bool okGood = InvokeStatic<bool>("TryConvertPathfindingData", goodArgs);
            bool okBad = InvokeStatic<bool>("TryConvertPathfindingData", badArgs);

            okGood.Should().BeTrue();
            okBad.Should().BeFalse();
            ((int[][])goodArgs[1]!).Should().BeEquivalentTo(new[]
            {
                new[] { 1, 0, 2 },
                new[] { 3, 4, 5 }
            });
        }

        [TestMethod]
        public void TryConvertRow_ParsesNumericArrays_AndRejectsInvalidValues()
        {
            object?[] intRowArgs = [new[] { 1, 2, 3 }, null];
            object?[] objRowArgs = [new object[] { 4, 5, 6 }, null];
            object?[] badRowArgs = [new object[] { 1, "x" }, null];
            object?[] nullValueArgs = [new object?[] { 1, (object?)null }, null];

            bool intRowOk = InvokeStatic<bool>("TryConvertRow", intRowArgs);
            bool objRowOk = InvokeStatic<bool>("TryConvertRow", objRowArgs);
            bool badRowOk = InvokeStatic<bool>("TryConvertRow", badRowArgs);
            bool nullValueOk = InvokeStatic<bool>("TryConvertRow", nullValueArgs);

            intRowOk.Should().BeTrue();
            objRowOk.Should().BeTrue();
            badRowOk.Should().BeFalse();
            nullValueOk.Should().BeFalse();
            ((int[])objRowArgs[1]!).Should().Equal(4, 5, 6);
        }

        [TestMethod]
        public void ConvertRawGridToWalkable_ConvertsPositiveValuesToTrue()
        {
            int[][] raw =
            [
                [1, 0, -1],
                [2, 3, 0]
            ];

            bool[][] walkable = InvokeStatic<bool[][]>("ConvertRawGridToWalkable", (object)raw);

            walkable.Should().BeEquivalentTo(new[]
            {
                new[] { true, false, false },
                new[] { true, true, false }
            });
        }

        [TestMethod]
        public void ResolveScale_UsesFallbackWhenGridDeltaIsNearZero_AndMinimumWhenTooSmall()
        {
            float nearZeroGrid = InvokeStatic<float>("ResolveScale", 10f, 0f);
            float tooSmallScale = InvokeStatic<float>("ResolveScale", 0.0001f, 1f);
            float regularScale = InvokeStatic<float>("ResolveScale", 20f, 4f);

            nearZeroGrid.Should().Be(2.5f);
            tooSmallScale.Should().Be(2.5f);
            regularScale.Should().Be(5f);
        }

        [TestMethod]
        public void IsFinitePoint_ReturnsExpectedForFiniteAndNonFiniteValues()
        {
            InvokeStatic<bool>("IsFinitePoint", 1f, 2f).Should().BeTrue();
            InvokeStatic<bool>("IsFinitePoint", float.NaN, 2f).Should().BeFalse();
            InvokeStatic<bool>("IsFinitePoint", 1f, float.PositiveInfinity).Should().BeFalse();
            InvokeStatic<bool>("IsFinitePoint", float.NegativeInfinity, float.NaN).Should().BeFalse();
        }

        private static T InvokeStatic<T>(string methodName, params object?[] args)
        {
            MethodInfo method = typeof(PathfindingService).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static)!
                ?? throw new InvalidOperationException("Method not found: " + methodName);

            object? result = method.Invoke(null, args);
            return (T)result!;
        }
    }
}