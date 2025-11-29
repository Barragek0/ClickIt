using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using SharpDX;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceRectOverlapTests
    {
        [TestMethod]
        public void DoRectanglesOverlap_ReturnsTrue_ForIntersectingRects()
        {
            var mi = typeof(Services.LabelFilterService).GetMethod("DoRectanglesOverlap", BindingFlags.NonPublic | BindingFlags.Static)!;
            var a = new RectangleF(0, 0, 10, 10);
            var b = new RectangleF(5, 5, 20, 20);
            var res = (bool)mi.Invoke(null, new object[] { a, b })!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void DoRectanglesOverlap_ReturnsFalse_ForAdjacentNonOverlappingRects()
        {
            var mi = typeof(Services.LabelFilterService).GetMethod("DoRectanglesOverlap", BindingFlags.NonPublic | BindingFlags.Static)!;
            var a = new RectangleF(0, 0, 10, 10);
            // Right edge == Left edge should be non-overlapping per implementation
            var b = new RectangleF(10, 0, 20, 10);
            var res = (bool)mi.Invoke(null, new object[] { a, b })!;
            res.Should().BeFalse();
        }
    }
}
