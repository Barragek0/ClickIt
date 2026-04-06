namespace ClickIt.Tests.Features.Click.Interaction
{
    [TestClass]
    public class InteractionExecutorTests
    {
        [TestMethod]
        public void PerformClick_ReturnsWithoutIncrementingSequence_WhenHotkeyInactiveAndNotLazy()
        {
            var settings = new ClickItSettings();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => false);

            executor.PerformClick(new Vector2(10f, 20f));

            executor.GetSuccessfulClickSequence().Should().Be(0);
        }

        [TestMethod]
        public void PerformClick_LogsAndReturns_WhenPointOutsideVirtualScreen()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            executor.PerformClick(new Vector2(-5000f, -5000f));

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("Skipping click at", StringComparison.Ordinal)
                && message.Contains("outside virtual screen bounds", StringComparison.Ordinal));
        }

        [TestMethod]
        public void PerformClickAndHold_ReturnsWithoutIncrementingSequence_WhenKeyBindingIsNone()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.None)
            };
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);

            executor.PerformClickAndHold(new Vector2(10f, 20f), holdDurationMs: 100);

            executor.GetSuccessfulClickSequence().Should().Be(0);
        }

        [TestMethod]
        public void PerformClickAndHold_ReturnsWithoutIncrementingSequence_WhenHotkeyInactiveAndNotLazy()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.F)
            };
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => false);

            executor.PerformClickAndHold(new Vector2(10f, 20f), holdDurationMs: 100);

            executor.GetSuccessfulClickSequence().Should().Be(0);
        }

        [TestMethod]
        public void PerformClickAndHold_LogsAndReturns_WhenLazyModeLimiterIsActive()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.F)
            };
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            settings.LazyMode.Value = true;
            settings.LazyModeClickLimiting.Value = 500;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            RuntimeMemberAccessor.SetRequiredMember(executor, "_lastClickTimestampMs", Environment.TickCount64);

            executor.PerformClickAndHold(new Vector2(10f, 20f), holdDurationMs: 100);

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("LazyMode limiter", StringComparison.Ordinal));
        }

        [TestMethod]
        public void PerformClickAndHold_LogsAndReturns_WhenPointOutsideVirtualScreen()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.F)
            };
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            executor.PerformClickAndHold(new Vector2(-5000f, -5000f), holdDurationMs: 100);

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("Skipping hold click at", StringComparison.Ordinal)
                && message.Contains("outside virtual screen bounds", StringComparison.Ordinal));
        }

        [TestMethod]
        public void HoverAndGetUIHover_ReturnsNull_WhenPointIsOutsideVirtualScreen()
        {
            var settings = new ClickItSettings();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));

            Element? hover = executor.HoverAndGetUIHover(new Vector2(-5000f, -5000f), gameController);

            hover.Should().BeNull();
        }

    }
}