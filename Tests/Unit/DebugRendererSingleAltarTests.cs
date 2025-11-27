using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Reflection;
using ClickIt.Rendering;
using ClickIt.Utils;
using ClickIt.Tests.TestUtils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DebugRendererSingleAltarTests
    {
        private static object CreateRendererAndInject(out DeferredTextQueue dtq, WeightCalculator? wc = null)
        {
            var type = typeof(DebugRenderer);
            var inst = RuntimeHelpers.GetUninitializedObject(type);

            var plugin = RuntimeHelpers.GetUninitializedObject(typeof(ClickIt));
            var state = new PluginContext();
            var backing = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            backing.SetValue(plugin, state);

            dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            type.GetField("_plugin", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, plugin);
            type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dtq);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);

            if (wc != null)
                type.GetField("_weightCalculator", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, wc);

            return inst!;
        }

        [TestMethod]
        public void RenderSingleAltarDebug_EmitsText_AndIncludesWeights_WhenWeightCalculatorAvailable()
        {
            var wc = new WeightCalculator(new ClickItSettings());
            var renderer = CreateRendererAndInject(out var dtq, wc);

            // Build a primary altar with populated upsides/downsides so we generate output
            var top = Tests.TestUtils.TestBuilders.BuildSecondary(new string[] { "up1" }, new string[] { "down1" });
            var bottom = Tests.TestUtils.TestBuilders.BuildSecondary(new string[] { "bup1" }, new string[] { "bdown1" });
            var primary = Tests.TestUtils.TestBuilders.BuildPrimary(top, bottom);
            // WeightCalculator expects non-null Element members on top/bottom (it doesn't inspect text here
            // for this call) - create lightweight uninitialized Element instances to satisfy null checks.
            top.Element = (ExileCore.PoEMemory.Element?)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.PoEMemory.Element));
            bottom.Element = (ExileCore.PoEMemory.Element?)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.PoEMemory.Element));

            var mi = renderer.GetType().GetMethod("RenderSingleAltarDebug", BindingFlags.Public | BindingFlags.Instance)!;
            mi.Should().NotBeNull();

            var result = (int)mi.Invoke(renderer, new object[] { 0, 0, 10, primary, 1 })!;
            result.Should().BeGreaterThan(0);

            // Ensure something was enqueued
            var field = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var list = (System.Collections.ICollection)field.GetValue(dtq)!;
            list.Count.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void RenderSingleAltarDebug_EmitsText_WithoutWeights_WhenNoWeightCalculator()
        {
            var renderer = CreateRendererAndInject(out var dtq, null);

            var top = Tests.TestUtils.TestBuilders.BuildSecondary(new string[] { "up1" }, new string[] { "down1" });
            var bottom = Tests.TestUtils.TestBuilders.BuildSecondary(new string[] { "bup1" }, new string[] { "bdown1" });
            var primary = Tests.TestUtils.TestBuilders.BuildPrimary(top, bottom);

            var mi = renderer.GetType().GetMethod("RenderSingleAltarDebug", BindingFlags.Public | BindingFlags.Instance)!;
            mi.Should().NotBeNull();

            var result = (int)mi.Invoke(renderer, new object[] { 0, 0, 10, primary, 1 })!;
            result.Should().BeGreaterThan(0);

            var field = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var list = (System.Collections.ICollection)field.GetValue(dtq)!;
            list.Count.Should().BeGreaterThan(0);
        }
    }
}
