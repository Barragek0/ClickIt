using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Rendering
{
    [TestClass]
    public class LazyModeRendererLogicTests
    {
        [TestMethod]
        public void GetLazyModeRestrictionDisplayReason_UsesGenericFallback_WhenInputIsBlank()
        {
            string result = global::ClickIt.Rendering.LazyModeRenderer.GetLazyModeRestrictionDisplayReason("   ");

            result.Should().Be("Lazy mode blocking condition detected.");
        }

        [TestMethod]
        public void GetLazyModeRestrictionDisplayReason_TrimsAndReturnsReason_WhenProvided()
        {
            string result = global::ClickIt.Rendering.LazyModeRenderer.GetLazyModeRestrictionDisplayReason("  Something blocked  ");

            result.Should().Be("Something blocked");
        }

        [TestMethod]
        public void WrapOverlayText_WrapsLongTextAndSkipsBlankLines()
        {
            var lines = global::ClickIt.Rendering.LazyModeRenderer.WrapOverlayText("first line\n\nthis line should wrap into chunks", 12);

            lines.Should().NotBeEmpty();
            lines[0].Should().Be("first line");
            lines.Should().OnlyContain(x => x.Length <= 12);
        }

        [DataTestMethod]
        [DataRow(true, false, "Left mouse button")]
        [DataRow(false, true, "Right mouse button")]
        [DataRow(true, true, "both mouse buttons")]
        public void GetBlockingMouseButtonName_ReturnsExpectedText(bool leftBlocks, bool rightBlocks, string expected)
        {
            string result = global::ClickIt.Rendering.LazyModeRenderer.GetBlockingMouseButtonName(leftBlocks, rightBlocks);

            result.Should().Be(expected);
        }
    }
}