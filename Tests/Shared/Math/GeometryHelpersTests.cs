using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Shared.Math
{
    [TestClass]
    public class GeometryHelpersTests
    {
        [TestMethod]
        public void RectanglesOverlapExclusive_ReturnsTrue_ForIntersectingRects()
        {
            var a = new RectangleF(0, 0, 10, 10);
            var b = new RectangleF(5, 5, 20, 20);

            GeometryHelpers.RectanglesOverlapExclusive(a, b).Should().BeTrue();
        }

        [TestMethod]
        public void RectanglesOverlapExclusive_ReturnsFalse_ForAdjacentNonOverlappingRects()
        {
            var a = new RectangleF(0, 0, 10, 10);
            var b = new RectangleF(10, 0, 20, 10);

            GeometryHelpers.RectanglesOverlapExclusive(a, b).Should().BeFalse();
        }
    }
}