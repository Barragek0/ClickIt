using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Rendering;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class StrongboxRendererTryVisibleTests
    {
        [TestMethod]
        public void TryGetVisibleLabelRect_ForTests_ValidStrongbox_ReturnsTrueAndRectAdjusted()
        {
            var window = new RectangleF(10, 20, 100, 100);
            var itemPath = "Some/Path/StrongBoxes/Strongbox/foo";
            var maybeRect = new RectangleF(2, 3, 4, 5);

            var ok = StrongboxRenderer.TryGetVisibleLabelRect_ForTests(itemPath, true, maybeRect, window, out var rect, out var pathOut);

            ok.Should().BeTrue();
            pathOut.Should().Be(itemPath);
            // seam returns the raw client rect (tests exercise the seam behaviour)
            rect.X.Should().Be(2);
            rect.Y.Should().Be(3);
            rect.Width.Should().Be(4);
            rect.Height.Should().Be(5);
        }

        [TestMethod]
        public void TryGetVisibleLabelRect_ForTests_InvalidUnderVariousFailures_ReturnsFalse()
        {
            var window = new RectangleF(0, 0, 50, 50);

            // missing path
            StrongboxRenderer.TryGetVisibleLabelRect_ForTests(null, true, new RectangleF(0,0,1,1), window, out _, out _).Should().BeFalse();

            // not a strongbox path
            StrongboxRenderer.TryGetVisibleLabelRect_ForTests("not/related/path", true, new RectangleF(0,0,1,1), window, out _, out _).Should().BeFalse();

            // element invalid
            StrongboxRenderer.TryGetVisibleLabelRect_ForTests("abc/strongbox/there", false, new RectangleF(0,0,1,1), window, out _, out _).Should().BeFalse();

            // null rectangle
            StrongboxRenderer.TryGetVisibleLabelRect_ForTests("abc/strongbox/there", true, null, window, out _, out _).Should().BeFalse();

            // rect outside window
            StrongboxRenderer.TryGetVisibleLabelRect_ForTests("abc/strongbox/x", true, new RectangleF(1000, 1000, 1, 1), window, out _, out _).Should().BeFalse();
        }
    }
}
