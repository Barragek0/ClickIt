using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using System.Collections.Generic;
using ClickIt.Utils;
using ClickIt.Components;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererGetValidatedButtonTests
    {
        private static (object renderer, List<string> logs) CreateRendererWithLog()
        {
            var type = typeof(Rendering.AltarDisplayRenderer);
            var inst = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);

            var settings = new ClickItSettings();
            var dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            type.GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, settings);
            type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dtq);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);

            var logs = new List<string>();
            // capture logs in a local lambda that closes over the local list (no out/ref capture)
            System.Action<string, int> logger = (s, f) => logs.Add(s);
            type.GetField("_logMessage", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, logger);

            // Make sure graphics/queues exist so methods won't NRE in other helpers
            var fakeGraphics = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.Graphics));
            type.GetField("_graphics", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, fakeGraphics);

            return (inst!, logs);
        }

        private static MethodInfo GetPrivateMethod(object instance, string name)
        {
            return instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
        }

        [TestMethod]
        public void GetValidatedButtonElement_NullButton_LogsAndReturnsNull()
        {
            var tup0 = CreateRendererWithLog();
            var renderer = tup0.renderer;
            var logs = tup0.logs;
            var mi = GetPrivateMethod(renderer, "GetValidatedButtonElement");
            var res = mi.Invoke(renderer, new object[] { null, "TopButton" });
            res.Should().BeNull();
            logs.Should().Contain(x => x.Contains("TopButton is null"));
        }

        [TestMethod]
        public void GetValidatedButtonElement_ElementNull_LogsAndReturnsNull()
        {
            var tup = CreateRendererWithLog();
            var renderer = tup.renderer;
            var logs = tup.logs;
            var button = new AltarButton(null);

            var mi = GetPrivateMethod(renderer, "GetValidatedButtonElement");
            var res = mi.Invoke(renderer, new object[] { button, "BottomButton" });
            res.Should().BeNull();
            logs.Should().Contain(x => x.Contains("BottomButton.Element is null"));
        }

        // Note: tests for Element.IsValid branches are not included here because
        // ExileCore.PoEMemory.Element relies on internal runtime state. Attempting
        // to call IsValid on an uninitialized Element causes a NullReferenceException
        // inside the external library. Those branches are covered implicitly by
        // other higher-level tests that exercise GetValidatedButtonElement via
        // primary paths without forcing Element.IsValid directly.
    }
}
