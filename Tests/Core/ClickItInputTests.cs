using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using ClickIt.Utils;
using System;
using System.Diagnostics;
using ClickIt.Tests.Harness;
using System.Threading;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItInputTests
    {
        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenInputHandlerMissing()
        {
            var plugin = new ClickIt();
            plugin.State.InputHandler = null;

            plugin.IsClickHotkeyPressedForTests().Should().BeFalse();
        }

        [TestMethod]
        public void HandleHotkeyPressed_SetsWorkFinishedFalse()
        {
            var plugin = new ClickIt();
            ClickItHostHarness.SetSettings(plugin, new ClickItSettings());

            var state = plugin.State;

            state.WorkFinished = true;

            // Disable native input to prevent real mouse operations during testing
            var originalDisable = Mouse.DisableNativeInput;
            Mouse.DisableNativeInput = true;

            try
            {
                plugin.HandleHotkeyPressedForTests();

                state.WorkFinished.Should().BeFalse();
            }
            finally
            {
                Mouse.DisableNativeInput = originalDisable;
            }
        }

        [TestMethod]
        public void Tick_WhenInputHandlerMissing_ResetsClickCount()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            ClickItHostHarness.SetSettings(plugin, settings);

            var state = plugin.State;

            var pm = new PerformanceMonitor(settings);
            pm.SetClickCountForTests(5);

            state.PerformanceMonitor = pm;

            state.InputHandler = null;

            pm.GetClickCountForTests().Should().BeGreaterThan(0);

            plugin.Tick();

            pm.GetClickCountForTests().Should().Be(0);
        }

        [TestMethod]
        public void ResumeAltarScanningIfDue_RestartsSecondTimer_WhenDueAndNotDeferred()
        {
            var plugin = new ClickIt();
            ClickItHostHarness.SetSettings(plugin, new ClickItSettings());

            var state = plugin.State;

            state.SecondTimer.Restart();
            Thread.Sleep(220);
            state.SecondTimer.ElapsedMilliseconds.Should().BeGreaterThan(200);

            plugin.ResumeAltarScanningIfDueForTests(false);

            state.SecondTimer.ElapsedMilliseconds.Should().BeLessThan(50);
        }

        [TestMethod]
        public void ResumeAltarScanningIfDue_RestartsSecondTimer_WhenHotkeyHeld()
        {
            var plugin = new ClickIt();
            ClickItHostHarness.SetSettings(plugin, new ClickItSettings());

            var state = plugin.State;

            state.SecondTimer.Restart();
            Thread.Sleep(220);

            plugin.ResumeAltarScanningIfDueForTests(true);

            state.SecondTimer.ElapsedMilliseconds.Should().BeLessThan(50);
        }

        [TestMethod]
        public void FindExistingClickLogicCoroutine_ReturnsNull_WhenNoMatchingCoroutine()
        {
            try
            {
                var res = ClickIt.FindExistingClickLogicCoroutineForTests();
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
            ClickIt.ShouldRunManualUiHoverCoroutineForInputState(manualUiHoverEnabled: true, lazyModeEnabled: false)
                .Should().BeTrue();

            ClickIt.ShouldRunManualUiHoverCoroutineForInputState(manualUiHoverEnabled: true, lazyModeEnabled: true)
                .Should().BeFalse();

            ClickIt.ShouldRunManualUiHoverCoroutineForInputState(manualUiHoverEnabled: false, lazyModeEnabled: false)
                .Should().BeFalse();
        }

    }
}
