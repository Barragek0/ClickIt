using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using SharpDX;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererPrivateTests
    {
        [TestMethod]
        public void IsValidRectangle_ReturnsExpected_ForEdgeCases()
        {
            var mi = typeof(Rendering.AltarDisplayRenderer).GetMethod("IsValidRectangle", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            // Positive
            var rect = new RectangleF(0, 0, 10, 10);
            ((bool)mi.Invoke(null, [rect])).Should().BeTrue();

            // Zero width/height
            ((bool)mi.Invoke(null, [new RectangleF(1, 2, 0, 3)])).Should().BeFalse();
            ((bool)mi.Invoke(null, [new RectangleF(1, 2, 3, 0)])).Should().BeFalse();

            // NaN/Infinity
            ((bool)mi.Invoke(null, [new RectangleF(float.NaN, 1, 10, 10)])).Should().BeFalse();
            ((bool)mi.Invoke(null, [new RectangleF(float.PositiveInfinity, 1, 10, 10)])).Should().BeFalse();
        }

        [TestMethod]
        public void GetWeightColor_PicksCorrectColor_ForComparisons()
        {
            var mi = typeof(Rendering.AltarDisplayRenderer).GetMethod("GetWeightColor", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var win = Color.LawnGreen;
            var lose = Color.OrangeRed;
            var tie = Color.Yellow;

            ((Color)mi.Invoke(null, [10m, 1m, win, lose, tie])).Should().Be(win);
            ((Color)mi.Invoke(null, [1m, 10m, win, lose, tie])).Should().Be(lose);
            ((Color)mi.Invoke(null, [5m, 5m, win, lose, tie])).Should().Be(tie);
        }

        [TestMethod]
        public void GetWeightArray_ReturnsCorrect_ArrayForAllCombinations()
        {
            var mi = typeof(Rendering.AltarDisplayRenderer).GetMethod("GetWeightArray", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var aw = new AltarWeights();
            var topDown = new decimal[] { 1, 1, 1, 1, 1, 1, 1, 1 };
            var bottomDown = new decimal[] { 2, 2, 2, 2, 2, 2, 2, 2 };
            var topUp = new decimal[] { 3, 3, 3, 3, 3, 3, 3, 3 };
            var bottomUp = new decimal[] { 4, 4, 4, 4, 4, 4, 4, 4 };
            aw.InitializeFromArrays(topDown, bottomDown, topUp, bottomUp);

            // top/downside
            var arr1 = (decimal[])mi.Invoke(null, [aw, true, false]);
            arr1.Should().Equal(topDown);

            // bottom/downside
            var arr2 = (decimal[])mi.Invoke(null, [aw, false, false]);
            arr2.Should().Equal(bottomDown);

            // top/upside
            var arr3 = (decimal[])mi.Invoke(null, [aw, true, true]);
            arr3.Should().Equal(topUp);

            // bottom/upside
            var arr4 = (decimal[])mi.Invoke(null, [aw, false, true]);
            arr4.Should().Equal(bottomUp);
        }
    }
}
