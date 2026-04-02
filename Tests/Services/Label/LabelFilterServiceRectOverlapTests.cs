using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using SharpDX;
using ClickIt.Utils;

namespace ClickIt.Tests.Label
{
    [TestClass]
    public class LabelFilterServiceRectOverlapTests
    {
        [TestMethod]
        public void DoRectanglesOverlap_ReturnsTrue_ForIntersectingRects()
        {
            var a = new RectangleF(0, 0, 10, 10);
            var b = new RectangleF(5, 5, 20, 20);
            var res = GeometryHelpers.RectanglesOverlapExclusive(a, b);
            res.Should().BeTrue();
        }

        [TestMethod]
        public void DoRectanglesOverlap_ReturnsFalse_ForAdjacentNonOverlappingRects()
        {
            var a = new RectangleF(0, 0, 10, 10);
            var b = new RectangleF(10, 0, 20, 10);
            var res = GeometryHelpers.RectanglesOverlapExclusive(a, b);
            res.Should().BeFalse();
        }
    }
}
