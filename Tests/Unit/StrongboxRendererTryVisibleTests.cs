using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Rendering;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class StrongboxRendererTryVisibleTests
    {
        [DataTestMethod]
        // Valid case
        [DataRow("Some/Path/StrongBoxes/Strongbox/foo", true, 2f, 3f, 4f, 5f, 10f, 20f, 100f, 100f, true)]
        // missing path
        [DataRow(null, true, 0f, 0f, 1f, 1f, 0f, 0f, 50f, 50f, false)]
        // not a strongbox path
        [DataRow("not/related/path", true, 0f, 0f, 1f, 1f, 0f, 0f, 50f, 50f, false)]
        // element invalid
        [DataRow("abc/strongbox/there", false, 0f, 0f, 1f, 1f, 0f, 0f, 50f, 50f, false)]
        // rect outside window
        [DataRow("abc/strongbox/x", true, 1000f, 1000f, 1f, 1f, 0f, 0f, 50f, 50f, false)]
        public void TryGetVisibleLabelRect_ForTests_VariousCases(string? itemPath, bool elementIsValid, float rX, float rY, float rW, float rH, float wX, float wY, float wW, float wH, bool expect)
        {
            var window = new RectangleF(wX, wY, wW, wH);

            RectangleF? maybeRect = (rW <= 0 || rH <= 0) ? (RectangleF?)null : new RectangleF(rX, rY, rW, rH);

            var ok = StrongboxRenderer.TryGetVisibleLabelRect_ForTests(itemPath, elementIsValid, maybeRect, window, out var rect, out var pathOut);

            ok.Should().Be(expect);
            if (expect)
            {
                pathOut.Should().Be(itemPath);
                rect.X.Should().Be(rX);
                rect.Y.Should().Be(rY);
                rect.Width.Should().Be(rW);
                rect.Height.Should().Be(rH);
            }
        }

        [TestMethod]
        public void TryGetVisibleLabelRect_PrivateMethod_NullLabel_ReturnsFalse()
        {
            // This covers the private, non-seam TryGetVisibleLabelRect(LabelOnGround?, RectangleF, out rect, out path)
            var method = typeof(Rendering.StrongboxRenderer).GetMethod("TryGetVisibleLabelRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var args = new object?[] { null, SharpDX.RectangleF.Empty, null, null };
            var ret = (bool?)method.Invoke(null, args);
            ret.Should().BeFalse();
        }
    }
}
