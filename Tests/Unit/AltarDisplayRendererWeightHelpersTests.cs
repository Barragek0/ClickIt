using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using SharpDX;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererWeightHelpersTests
    {
        private static object CreateRendererWithDefaults()
        {
            var type = typeof(Rendering.AltarDisplayRenderer);
            var inst = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);

            var settings = new ClickItSettings();
            var dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            type.GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, settings);
            type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dtq);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);
            type.GetField("_logMessage", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, new System.Action<string, int>((s, f) => { }));

            return inst!;
        }

        private static MethodInfo GetPrivateMethod(object instance, string name)
        {
            // Allow finding private static helpers too
            return instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!;
        }

        [TestMethod]
        public void HasAnyWeightOverThreshold_DetectsValuesCorrectly()
        {
            var renderer = CreateRendererWithDefaults();

            var aw = new AltarWeights();
            // put a high value into top downside and top upside arrays
            var topDown = new decimal[8] { 0, 0, 0, 0, 0, 0, 0, 95 };
            var bottomDown = new decimal[8] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var topUp = new decimal[8] { 1, 1, 1, 1, 1, 1, 1, 1 };
            var bottomUp = new decimal[8] { 1, 1, 1, 1, 1, 1, 1, 1 };
            aw.InitializeFromArrays(topDown, bottomDown, topUp, bottomUp);

            var mi = GetPrivateMethod(renderer, "HasAnyWeightOverThreshold");

            // threshold 90 -> topDown contains 95 so top downside should be true
            ((bool)mi.Invoke(renderer, new object[] { aw, true, false, 90 })).Should().BeTrue();

            // threshold 100 -> none exceed 100
            ((bool)mi.Invoke(renderer, new object[] { aw, true, false, 100 })).Should().BeFalse();

            // check upside arrays: values equal threshold 1 should be considered "over" (>=)
            ((bool)mi.Invoke(renderer, new object[] { aw, false, true, 1 })).Should().BeTrue();
        }

        [TestMethod]
        public void HasAnyWeightAtOrBelowThreshold_FindsPositiveNonZeroValuesOnly()
        {
            var renderer = CreateRendererWithDefaults();

            var aw = new AltarWeights();
            // create arrays with a mix of zeros and small positive values
            var topDown = new decimal[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var bottomDown = new decimal[8] { 0, 1, 2, 0, 3, 0, 0, 0 };
            var topUp = new decimal[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var bottomUp = new decimal[8] { 0, 0, 0, 2, 0, 0, 0, 0 };
            aw.InitializeFromArrays(topDown, bottomDown, topUp, bottomUp);

            var mi = GetPrivateMethod(renderer, "HasAnyWeightAtOrBelowThreshold");

            // threshold 1 -> bottomDown contains a 1 => true (ignore zeros)
            ((bool)mi.Invoke(renderer, new object[] { aw, false, false, 1 })).Should().BeTrue();

            // threshold 1 on upside bottomUp -> we have a 2 which is >1 -> false
            ((bool)mi.Invoke(renderer, new object[] { aw, false, true, 1 })).Should().BeFalse();

            // threshold 3 on bottomUp -> there is a 2 <= 3 and >0 -> true
            ((bool)mi.Invoke(renderer, new object[] { aw, false, true, 3 })).Should().BeTrue();
        }

        [TestMethod]
        public void HasAnyWeightOverThreshold_AllZeros_ReturnsFalse()
        {
            var renderer = CreateRendererWithDefaults();

            var aw = new AltarWeights();
            var zero = new decimal[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            aw.InitializeFromArrays(zero, zero, zero, zero);

            var mi = GetPrivateMethod(renderer, "HasAnyWeightOverThreshold");

            ((bool)mi.Invoke(renderer, new object[] { aw, true, false, 1 })).Should().BeFalse();
        }

        [TestMethod]
        public void HasAnyWeightAtOrBelowThreshold_AllZeros_ReturnsFalse()
        {
            var renderer = CreateRendererWithDefaults();

            var aw = new AltarWeights();
            var zero = new decimal[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            aw.InitializeFromArrays(zero, zero, zero, zero);

            var mi = GetPrivateMethod(renderer, "HasAnyWeightAtOrBelowThreshold");

            // zeros should be ignored by the 'at or below' helper (requires >0)
            ((bool)mi.Invoke(renderer, new object[] { aw, true, false, 1 })).Should().BeFalse();
        }

        [TestMethod]
        public void DrawUnrecognizedWeightText_Enqueues_WhenGraphicsPresent_AndHasNonEmptyMods()
        {
            var renderer = CreateRendererWithDefaults();

            // make _graphics non-null (uninitialized object is fine because DrawUnrecognizedWeightText only checks for null)
            var type = renderer.GetType();
            var gfx = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.Graphics));
            type.GetField("_graphics", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(renderer, gfx);

            // call private method
            var mi = GetPrivateMethod(renderer, "DrawUnrecognizedWeightText");
            mi.Invoke(renderer, new object[] { "Top upside", new string[] { "modA", "" }, new Vector2(0, 0) });

            // inspect the deferred text queue's private _items list via reflection
            var dtq = type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;
            var items = (System.Collections.ICollection)dtq.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dtq)!;
            items.Count.Should().BeGreaterThan(0);
        }
    }
}
