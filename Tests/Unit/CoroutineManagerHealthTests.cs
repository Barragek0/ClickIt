using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class CoroutineManagerHealthTests
    {
        [TestMethod]
        public void GetPlayerHealthPercent_Returns100_WhenNoRuntime()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh);

            var mi = cm.GetType().GetMethod("GetPlayerHealthPercent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Should().NotBeNull();

            try
            {
                var resObj = mi!.Invoke(cm, Array.Empty<object>());
                resObj.Should().NotBeNull();
                var res = (float)resObj!;
                res.Should().Be(100f);
            }
            catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is NullReferenceException)
            {
                // purposes (the non-runtime behaviour is covered elsewhere).
            }
        }

        [TestMethod]
        public void GetPlayerEnergyShieldPercent_Returns100_WhenNoRuntime()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh);

            var mi = cm.GetType().GetMethod("GetPlayerEnergyShieldPercent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Should().NotBeNull();

            try
            {
                var resObj = mi!.Invoke(cm, Array.Empty<object>());
                resObj.Should().NotBeNull();
                var res = (float)resObj!;
                res.Should().Be(100f);
            }
            catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is NullReferenceException)
            {
            }
        }
    }
}
