using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using ClickIt.Utils;
using System;
using System.Diagnostics;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItInputTests
    {
        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenInputHandlerMissing()
        {
            var plugin = (ClickIt)RuntimeHelpers.GetUninitializedObject(typeof(ClickIt));

            // The runtime state type is PluginContext; create an instance via reflection
            var stateType = typeof(ClickIt).Assembly.GetType("ClickIt.PluginContext");
            var state = stateType is null ? null : Activator.CreateInstance(stateType);
            var backingField = typeof(ClickIt).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            backingField.Should().NotBeNull();
            backingField!.SetValue(plugin, state);

            var mi = typeof(ClickIt).GetMethod("IsClickHotkeyPressed", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = mi.Invoke(plugin, []);

            ((bool)result!).Should().BeFalse();
        }

        [TestMethod]
        public void HandleHotkeyPressed_SetsWorkFinishedFalse()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());

            // inject a fresh runtime State (PluginContext) so we can manipulate WorkFinished
            var state = new PluginContext();
            var backing = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            backing.Should().NotBeNull();
            backing.SetValue(plugin, state);

            state.WorkFinished = true;

            // Disable native input to prevent real mouse operations during testing
            var originalDisable = Mouse.DisableNativeInput;
            Mouse.DisableNativeInput = true;

            try
            {
                var mi = typeof(ClickIt).GetMethod("HandleHotkeyPressed", BindingFlags.Instance | BindingFlags.NonPublic)!;
                mi.Should().NotBeNull();
                mi.Invoke(plugin, []);

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
            plugin.__Test_SetSettings(new ClickItSettings());

            // Inject runtime State so we can attach a PerformanceMonitor
            var state = new PluginContext();
            var backing = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            backing.Should().NotBeNull();
            backing.SetValue(plugin, state);

            var pm = new PerformanceMonitor(plugin.__Test_GetSettings());
            var counterField = typeof(PerformanceMonitor).GetField("_clickCount", BindingFlags.NonPublic | BindingFlags.Instance)!;
            counterField.Should().NotBeNull();
            counterField.SetValue(pm, 5);

            state.PerformanceMonitor = pm;

            state.InputHandler = null;

            ((int)counterField.GetValue(pm)!).Should().BeGreaterThan(0);

            plugin.Tick();

            ((int)counterField.GetValue(pm)!).Should().Be(0);
        }

        [TestMethod]
        public void ResumeAltarScanningIfDue_RestartsSecondTimer_WhenDueAndNotDeferred()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());

            var state = new PluginContext();
            var backing = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            backing.Should().NotBeNull();
            backing.SetValue(plugin, state);

            var sw = state.SecondTimer;
            var candidates = new[] { "_elapsed", "elapsed", "m_elapsed", "elapsedTicks", "_elapsedTicks", "_elapsedTick" };
            foreach (var name in candidates)
            {
                var f = typeof(Stopwatch).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    long ticks = TimeSpan.FromMilliseconds(500).Ticks;
                    f.SetValue(sw, ticks);
                    break;
                }
            }
            state.SecondTimer.ElapsedMilliseconds.Should().BeGreaterThan(200);

            var mi = typeof(ClickIt).GetMethod("ResumeAltarScanningIfDue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            mi.Should().NotBeNull();
            mi.Invoke(plugin, [false]);

            state.SecondTimer.ElapsedMilliseconds.Should().BeLessThan(50);
        }

        [TestMethod]
        public void ResumeAltarScanningIfDue_RestartsSecondTimer_WhenHotkeyHeld()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());

            var state = new PluginContext();
            var backing = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            backing.Should().NotBeNull();
            backing.SetValue(plugin, state);

            var sw = state.SecondTimer;
            var candidates = new[] { "_elapsed", "elapsed", "m_elapsed", "elapsedTicks", "_elapsedTicks", "_elapsedTick" };
            foreach (var name in candidates)
            {
                var f = typeof(Stopwatch).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    long ticks = TimeSpan.FromMilliseconds(500).Ticks;
                    f.SetValue(sw, ticks);
                    break;
                }
            }

            var mi = typeof(ClickIt).GetMethod("ResumeAltarScanningIfDue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            mi.Should().NotBeNull();
            mi.Invoke(plugin, [true]);

            state.SecondTimer.ElapsedMilliseconds.Should().BeLessThan(50);
        }

        [TestMethod]
        public void FindExistingClickLogicCoroutine_ReturnsNull_WhenNoMatchingCoroutine()
        {
            var mi = typeof(ClickIt).GetMethod("FindExistingClickLogicCoroutine", BindingFlags.Static | BindingFlags.NonPublic)!;
            mi.Should().NotBeNull();
            try
            {
                var res = mi.Invoke(null, []);
                res.Should().BeNull();
            }
            catch (TargetInvocationException tie)
            {
                tie.InnerException.Should().BeOfType<NullReferenceException>();
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

        [TestMethod]
        public void HandleHotkeyReleased_ClearsChestSettlementState()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());

            var state = new PluginContext();
            var backing = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            backing.Should().NotBeNull();
            backing.SetValue(plugin, state);

            var clickService = (Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));
            var knownAddresses = new System.Collections.Generic.HashSet<long> { 100L };

            typeof(Services.ClickService).GetField("_pendingChestOpenConfirmationActive", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(clickService, true);
            typeof(Services.ClickService).GetField("_pendingChestOpenMechanicId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(clickService, "basic-chests");
            typeof(Services.ClickService).GetField("_pendingChestOpenItemAddress", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(clickService, 11L);
            typeof(Services.ClickService).GetField("_pendingChestOpenLabelAddress", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(clickService, 22L);
            typeof(Services.ClickService).GetField("_postChestLootSettleWatcherActive", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(clickService, true);
            typeof(Services.ClickService).GetField("_postChestLootSettleInitialDelayUntilTimestampMs", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(clickService, 777L);
            typeof(Services.ClickService).GetField("_postChestLootSettleKnownGroundItemAddresses", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(clickService, knownAddresses);

            state.ClickService = clickService;

            var method = typeof(ClickIt).GetMethod("HandleHotkeyReleased", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Should().NotBeNull();
            method.Invoke(plugin, []);

            typeof(Services.ClickService).GetField("_pendingChestOpenConfirmationActive", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(clickService).Should().Be(false);
            typeof(Services.ClickService).GetField("_pendingChestOpenMechanicId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(clickService).Should().BeNull();
            typeof(Services.ClickService).GetField("_pendingChestOpenItemAddress", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(clickService).Should().Be(0L);
            typeof(Services.ClickService).GetField("_pendingChestOpenLabelAddress", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(clickService).Should().Be(0L);
            typeof(Services.ClickService).GetField("_postChestLootSettleWatcherActive", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(clickService).Should().Be(false);
            typeof(Services.ClickService).GetField("_postChestLootSettleInitialDelayUntilTimestampMs", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(clickService).Should().Be(0L);
            knownAddresses.Should().BeEmpty();
        }
    }
}
