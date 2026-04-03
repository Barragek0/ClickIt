using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.UserInterface
{
    [TestClass]
    public class LazyModeRendererLogicTests
    {
        [TestMethod]
        public void GetLazyModeRestrictionDisplayReason_UsesGenericFallback_WhenInputIsBlank()
        {
            string result = LazyModeRenderer.GetLazyModeRestrictionDisplayReason("   ");

            result.Should().Be("Lazy mode blocking condition detected.");
        }

        [TestMethod]
        public void GetLazyModeRestrictionDisplayReason_TrimsAndReturnsReason_WhenProvided()
        {
            string result = LazyModeRenderer.GetLazyModeRestrictionDisplayReason("  Something blocked  ");

            result.Should().Be("Something blocked");
        }

        [TestMethod]
        public void WrapOverlayText_WrapsLongTextAndSkipsBlankLines()
        {
            var lines = LazyModeRenderer.WrapOverlayText("first line\n\nthis line should wrap into chunks", 12);

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
            string result = LazyModeRenderer.GetBlockingMouseButtonName(leftBlocks, rightBlocks);

            result.Should().Be(expected);
        }
    }
}