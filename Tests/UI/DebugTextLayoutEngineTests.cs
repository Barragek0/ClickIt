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
        public void WrapOverlayText_DoesNotThrow_WhenMaxLengthIsZeroOrNegative()
        {
            var lines = DebugTextLayoutEngine.WrapOverlayText("this text must not crash", 0);
            lines.Should().NotBeEmpty("a zero max length is clamped so wrapping never throws");

            var negative = DebugTextLayoutEngine.WrapOverlayText("this text must not crash", -5);
            negative.Should().NotBeEmpty();
        }
    }
}
