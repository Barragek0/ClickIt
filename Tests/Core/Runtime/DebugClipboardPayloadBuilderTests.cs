namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class DebugClipboardPayloadBuilderTests
    {
        [TestMethod]
        public void BuildDebugClipboardPayload_SkipsBlankLines_AndPreservesDebugContent()
        {
            string payload = DebugClipboardPayloadBuilder.BuildDebugClipboardPayload([
                "Line A",
                string.Empty,
                "   ",
                "Line B"
            ]);

            payload.Should().Contain("=== ClickIt Additional Debug Information ===");
            payload.Should().Contain("Line A");
            payload.Should().Contain("Line B");
            payload.Should().NotContain("\r\n\r\n\r\n");
        }
    }
}