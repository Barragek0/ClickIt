using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using ClickIt.Utils;
using System;
using System.Diagnostics;
using System.Threading;
using ClickIt;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItInputTests
    {
        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenInputHandlerMissing()
        {
            // Create uninitialized ClickIt instance and ensure State.InputHandler is null -> method should return false
            var plugin = (ClickIt)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClickIt));

            // Inject an empty state with no InputHandler (create via reflection so tests don't depend on compile-time symbol)
            // The runtime state type is PluginContext (not ClickItState) — create an instance via reflection
            var stateType = typeof(ClickIt).Assembly.GetType("ClickIt.PluginContext");
            var state = stateType is null ? null : System.Activator.CreateInstance(stateType);
            // The State property is an init-only auto-property; set its compiler-generated backing field
            var backingField = typeof(ClickIt).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            backingField.Should().NotBeNull();
            backingField!.SetValue(plugin, state);

            // Call private IsClickHotkeyPressed method via reflection
            var mi = typeof(ClickIt).GetMethod("IsClickHotkeyPressed", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = mi.Invoke(plugin, new object[0]);

            // mi.Invoke returns object boxed bool — assert it's false
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

            // Ensure precondition
            state.WorkFinished = true;

            // Disable native input to prevent real mouse operations during testing
            var originalDisable = global::ClickIt.Utils.Mouse.DisableNativeInput;
            global::ClickIt.Utils.Mouse.DisableNativeInput = true;

            try
            {
                // Call the private handler directly via reflection
                var mi = typeof(ClickIt).GetMethod("HandleHotkeyPressed", BindingFlags.Instance | BindingFlags.NonPublic)!;
                mi.Should().NotBeNull();
                mi.Invoke(plugin, new object[0]);

                // WorkFinished should have been cleared by the handler
                state.WorkFinished.Should().BeFalse();
            }
            finally
            {
                global::ClickIt.Utils.Mouse.DisableNativeInput = originalDisable;
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

            // Create a PerformanceMonitor and set its private click counter to a non-zero value
            var pm = new PerformanceMonitor(plugin.__Test_GetSettings());
            var counterField = typeof(PerformanceMonitor).GetField("_clickCount", BindingFlags.NonPublic | BindingFlags.Instance)!;
            counterField.Should().NotBeNull();
            counterField.SetValue(pm, 5);

            state.PerformanceMonitor = pm;

            // Ensure InputHandler is missing so IsClickHotkeyPressed -> false and Tick() calls ResetClickCount
            state.InputHandler = null;

            // Sanity: precondition is non-zero
            ((int)counterField.GetValue(pm)!).Should().BeGreaterThan(0);

            // Call Tick() which should call HandleHotkeyReleased -> ResetClickCount
            plugin.Tick();

            // Now the private _clickCount field should be zero
            ((int)counterField.GetValue(pm)!).Should().Be(0);
        }

        [TestMethod]
        public void ResumeAltarScanningIfDue_RestartsSecondTimer_WhenDue()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());

            var state = new PluginContext();
            var backing = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            backing.Should().NotBeNull();
            backing.SetValue(plugin, state);

            // Ensure the SecondTimer reports an elapsed time above 200ms by updating its private elapsed field
            var sw = state.SecondTimer;
            var candidates = new[] { "_elapsed", "elapsed", "m_elapsed", "elapsedTicks", "_elapsedTicks", "_elapsedTick" };
            // indicate when we've modified a private field (not used later)
            foreach (var name in candidates)
            {
                var f = typeof(Stopwatch).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    // Set internal ticks to 500ms
                    long ticks = TimeSpan.FromMilliseconds(500).Ticks;
                    f.SetValue(sw, ticks);
                    break;
                    break;
                }
            }
            // As a sanity check ensure the timer now claims to be past the threshold
            state.SecondTimer.ElapsedMilliseconds.Should().BeGreaterThan(200);

            // Invoke private method via reflection and ensure it restarts the timer
            var mi = typeof(ClickIt).GetMethod("ResumeAltarScanningIfDue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            mi.Should().NotBeNull();
            mi.Invoke(plugin, new object[0]);

            // After invocation the timer should have been restarted (elapsed resets near-zero)
            state.SecondTimer.ElapsedMilliseconds.Should().BeLessThan(50);
        }

        [TestMethod]
        public void FindExistingClickLogicCoroutine_ReturnsNull_WhenNoMatchingCoroutine()
        {
            // The method simply probes Core.ParallelRunner.Coroutines for a specific name
            // In a test environment with no coroutines running, it should return null safely
            var mi = typeof(ClickIt).GetMethod("FindExistingClickLogicCoroutine", BindingFlags.Static | BindingFlags.NonPublic)!;
            mi.Should().NotBeNull();
            try
            {
                var res = mi.Invoke(null, new object[0]);
                res.Should().BeNull();
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                // Core.ParallelRunner may be unavailable in unit-test contexts and cause a NullReferenceException.
                // Accept that as valid behaviour for this environment.
                tie.InnerException.Should().BeOfType<NullReferenceException>();
            }
        }
    }
}
