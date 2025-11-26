using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class CoroutineManagerKeyTests
    {
        [TestMethod]
        [Ignore("Consolidated into CoroutineManagerAdditionalTests.cs")]
        public void IsClickHotkeyPressed_ReturnsActual_WhenNotLazyMode()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = false;
            settings.ClickLabelKey.Value = System.Windows.Forms.Keys.F1;

            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            // Force deterministic key state
            global::ClickIt.Utils.CoroutineManager.KeyStateProvider = (k) => true;

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);
            var mi = cm.GetType().GetMethod("IsClickHotkeyPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var res = (bool)mi!.Invoke(cm, Array.Empty<object>());
            res.Should().BeTrue();
        }

        [TestMethod]
        [Ignore("Consolidated into CoroutineManagerAdditionalTests.cs")]
        public void IsClickHotkeyPressed_Inverts_WhenLazyModeEnabled()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.ClickLabelKey.Value = System.Windows.Forms.Keys.F1;

            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            // Return true from key provider; lazy mode inverts behaviour
            global::ClickIt.Utils.CoroutineManager.KeyStateProvider = (k) => true;

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);
            var mi = cm.GetType().GetMethod("IsClickHotkeyPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var res = (bool)mi!.Invoke(cm, Array.Empty<object>());
            // in lazy mode the hotkey is inverted
            res.Should().BeFalse();
        }

        [TestMethod]
        [Ignore("Consolidated into CoroutineManagerAdditionalTests.cs")]
        public void ExecuteWithElementAccessLock_RunsAction()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);

            bool ran = false;
            var mi = cm.GetType().GetMethod("ExecuteWithElementAccessLock", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            mi!.Invoke(cm, [new Action(() => ran = true)]);
            ran.Should().BeTrue();
        }
    }
}
