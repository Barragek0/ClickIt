namespace ClickIt.Tests.Features.Labels
{
    [TestClass]
    public class ClickHotkeyToggleOverlayTests
    {
        [TestMethod]
        public void ResolveTopY_OffsetsBelowLazyModeTitle_WhenLazyModeIsEnabled()
        {
            ClickHotkeyToggleOverlay.ResolveTopY(lazyModeEnabled: false).Should().Be(60f);
            ClickHotkeyToggleOverlay.ResolveTopY(lazyModeEnabled: true).Should().Be(130f);
        }

        [TestMethod]
        public void BuildStatus_ReturnsClickingState_WhenActive()
        {
            (Color color, string statusText) = ClickHotkeyToggleOverlay.BuildStatus(clicking: true);

            color.Should().Be(Color.LawnGreen);
            statusText.Should().Be("Clicking");
        }

        [TestMethod]
        public void BuildStatus_ReturnsNotClickingState_WhenInactive()
        {
            (Color color, string statusText) = ClickHotkeyToggleOverlay.BuildStatus(clicking: false);

            color.Should().Be(Color.Red);
            statusText.Should().Be("Not Clicking");
        }
    }
}
