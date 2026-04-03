namespace ClickIt.Tests.Shared.Input
{
    [TestClass]
    public class InputHandlerCanClickTests
    {
        [TestMethod]
        public void CanClick_ReturnsFalse_WhenGameControllerIsNull()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            handler.CanClick(null!).Should().BeFalse();
        }

        [TestMethod]
        public void GetCanClickFailureReason_ReturnsDefault_WhenStateUnavailable()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            string reason = handler.GetCanClickFailureReason(null!);

            reason.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        public void GetSuccessfulClickSequence_StartsAtZero()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            handler.GetSuccessfulClickSequence().Should().Be(0);
        }

        [TestMethod]
        public void ShouldSkipClickWhenNotLazyAndHotkeyInactive_ReturnsExpectedValues()
        {
            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: false,
                clickHotkeyActive: false).Should().BeTrue();

            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: false,
                clickHotkeyActive: true).Should().BeFalse();

            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: true,
                clickHotkeyActive: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSkipClickWhenNotLazyAndHotkeyInactive_ReturnsFalse_WhenExplicitOverrideEnabled()
        {
            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: false,
                clickHotkeyActive: false,
                allowWhenHotkeyInactive: true).Should().BeFalse();

            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: true,
                clickHotkeyActive: false,
                allowWhenHotkeyInactive: true).Should().BeFalse();
        }

    }
}
