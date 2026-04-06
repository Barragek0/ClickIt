namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    [DoNotParallelize]
    public class PluginInputHostTests
    {
        private static readonly PluginInputHost InputHost = new();

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenInputHandlerMissing()
        {
            var plugin = new ClickIt();
            plugin.State.Services.InputHandler = null;

            InputHost.IsClickHotkeyPressed(plugin.State).Should().BeFalse();
        }

        [TestMethod]
        public void HandleHotkeyPressed_SetsWorkFinishedFalse()
        {
            var plugin = new ClickIt();
            var state = plugin.State;

            state.Runtime.WorkFinished = true;

            InputHost.HandleHotkeyPressed(plugin.State);

            state.Runtime.WorkFinished.Should().BeFalse();
        }

        [TestMethod]
        public void HandleHotkeyPressed_ReplacesCompletedClickCoroutine_FromRegistryWhenNoActiveMatchExists()
        {
            var plugin = new ClickIt();
            var state = plugin.State;

            state.Runtime.ClickLabelCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.ClickLogic", isDone: true);

            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines(
            [
                CoroutineTestHarness.CreateCoroutine("Other.Coroutine", isDone: false),
            ]);

            InputHost.HandleHotkeyPressed(state);

            state.Runtime.ClickLabelCoroutine.Should().BeNull();
            state.Runtime.WorkFinished.Should().BeFalse();
        }

        [TestMethod]
        public void HandleHotkeyReleased_ReplacesCompletedManualHoverCoroutine_FromRegistryWhenEnabled()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            var state = plugin.State;
            var performanceMonitor = new PerformanceMonitor(settings);

            settings.ClickOnManualUiHoverOnly.Value = true;
            settings.LazyMode.Value = false;
            performanceMonitor.ClickActivity.ClickCount = 3;

            state.Services.PerformanceMonitor = performanceMonitor;
            state.Runtime.ManualUiHoverCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.ManualUiHoverLogic", isDone: true);

            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines(
            [
                CoroutineTestHarness.CreateCoroutine("Other.Coroutine", isDone: false),
            ]);

            InputHost.HandleHotkeyReleased(state, settings);

            state.Runtime.ManualUiHoverCoroutine.Should().BeNull();
            performanceMonitor.ClickActivity.ClickCount.Should().Be(0);
        }

        [TestMethod]
        public void Tick_WhenInputHandlerMissing_ResetsClickCount()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            var state = plugin.State;

            var performanceMonitor = new PerformanceMonitor(settings);
            performanceMonitor.ClickActivity.ClickCount = 5;

            state.Services.PerformanceMonitor = performanceMonitor;
            state.Services.InputHandler = null;

            performanceMonitor.ClickActivity.ClickCount.Should().BeGreaterThan(0);

            InputHost.Tick(state, settings);

            performanceMonitor.ClickActivity.ClickCount.Should().Be(0);
        }

        [TestMethod]
        public void ResumeAltarScanningIfDue_RestartsSecondTimer_WhenDue()
        {
            var plugin = new ClickIt();
            var state = plugin.State;

            state.Runtime.SecondTimer.Restart();
            Thread.Sleep(220);
            state.Runtime.SecondTimer.ElapsedMilliseconds.Should().BeGreaterThan(200);

            InputHost.ResumeAltarScanningIfDue(state);

            state.Runtime.SecondTimer.ElapsedMilliseconds.Should().BeLessThan(50);
        }

        [TestMethod]
        public void ResumeAltarScanningIfDue_DoesNothing_WhenNotDue()
        {
            var plugin = new ClickIt();
            var state = plugin.State;

            state.Runtime.SecondTimer.Restart();
            Thread.Sleep(20);

            long before = state.Runtime.SecondTimer.ElapsedMilliseconds;
            InputHost.ResumeAltarScanningIfDue(state);

            state.Runtime.SecondTimer.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(before);
        }

    }
}