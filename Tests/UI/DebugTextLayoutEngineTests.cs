using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.UI
{
    [TestClass]
    public class DebugTextLayoutEngineTests
    {
        [TestMethod]
        public void WrapOverlayText_WrapsAndSkipsBlankSegments()
        {
            var lines = DebugTextLayoutEngine.WrapOverlayText("first\n\nthis line should wrap", 10);

            lines.Should().NotBeEmpty();
            lines[0].Should().Be("first");
            lines.Should().OnlyContain(x => x.Length <= 10);
        }

        [TestMethod]
        public void WrapDebugText_PreservesLeadingIndentationAcrossWrappedLines()
        {
            var lines = DebugTextLayoutEngine.WrapDebugText("  this debug text should wrap across multiple rows", 20);

            lines.Should().HaveCountGreaterThan(1);
            lines.Should().OnlyContain(x => x.StartsWith("  "));
        }
    }
}
