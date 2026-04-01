using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services.Area;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AreaServiceRectsDifferTests
    {
        [TestMethod]
        public void RectsDiffer_ReturnsFalse_WhenDifferencesWithinEps()
        {
            var a = new RectangleF(0, 0, 100f, 80f);
            var b = new RectangleF(0.3f, 0.4f, 100.2f, 80.1f); // all diffs less than eps=0.5
            bool res = BlockedAreaGeometryEngine.RectsDiffer(a, b, 0.5f);
            res.Should().BeFalse();
        }

        [TestMethod]
        public void RectsDiffer_ReturnsTrue_WhenWidthDiffersMoreThanEps()
        {
            var a = new RectangleF(0, 0, 100f, 80f);
            var b = new RectangleF(0, 0, 101.0f, 80f); // width diff 1.0 (> 0.5)
            bool res = BlockedAreaGeometryEngine.RectsDiffer(a, b, 0.5f);
            res.Should().BeTrue();
        }

        [TestMethod]
        public void RectsDiffer_ReturnsTrue_WhenPositionDiffersMoreThanEps()
        {
            var a = new RectangleF(0, 0, 100f, 80f);
            var b = new RectangleF(1.0f, 0, 100f, 80f); // X diff 1.0 (> 0.5)
            bool res = BlockedAreaGeometryEngine.RectsDiffer(a, b, 0.5f);
            res.Should().BeTrue();
        }
    }
}
