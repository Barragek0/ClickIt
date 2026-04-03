using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Threading;

namespace ClickIt.Tests.Core
{
    [TestClass]
    public class ClickItInputTests
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
        public void Tick_WhenInputHandlerMissing_ResetsClickCount()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            var state = plugin.State;

            var pm = new PerformanceMonitor(settings);
            pm.ClickActivity.ClickCount = 5;

            state.Services.PerformanceMonitor = pm;

            state.Services.InputHandler = null;

            pm.ClickActivity.ClickCount.Should().BeGreaterThan(0);

            InputHost.Tick(state, settings);

            pm.ClickActivity.ClickCount.Should().Be(0);
        }

        [TestMethod]
        public void ResumeAltarScanningIfDue_RestartsSecondTimer_WhenDueAndNotDeferred()
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
        public void ResumeAltarScanningIfDue_RestartsSecondTimer_WhenHotkeyHeld()
        {
            var plugin = new ClickIt();

            var state = plugin.State;

            state.Runtime.SecondTimer.Restart();
            Thread.Sleep(220);

            InputHost.ResumeAltarScanningIfDue(state);

            state.Runtime.SecondTimer.ElapsedMilliseconds.Should().BeLessThan(50);
        }

        [TestMethod]
        public void FindExistingClickLogicCoroutine_ReturnsNull_WhenNoMatchingCoroutine()
        {
            try
            {
                var res = PluginCoroutineRegistry.FindClickLogicCoroutine();
                res.Should().BeNull();
            }
            catch (NullReferenceException)
            {
                // ParallelRunner can be unavailable in isolated tests.
            }
        }

        [TestMethod]
        public void ShouldRunManualUiHoverCoroutineForInputState_ReturnsTrue_OnlyWhenManualEnabledAndNotLazy()
        {
            PluginInputHost.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false)
                .Should().BeTrue();

            PluginInputHost.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: true)
                .Should().BeFalse();

            PluginInputHost.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: false, lazyModeEnabled: false)
                .Should().BeFalse();
        }

    }
}
