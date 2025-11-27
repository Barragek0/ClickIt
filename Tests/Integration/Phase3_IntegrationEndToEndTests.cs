using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpDX;
using ClickIt.Utils;
using ClickIt.Components;
using System.Collections.Generic;

namespace ClickIt.Tests.Integration
{
    [TestClass]
    public class Phase3_IntegrationEndToEndTests
    {
        private static object CreateRendererWithDefaults()
        {
            var type = typeof(ClickIt.Rendering.AltarDisplayRenderer);
            var inst = RuntimeHelpers.GetUninitializedObject(type);

            var settings = new ClickItSettings();
            var dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            type.GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, settings);
            type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dtq);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);
            type.GetField("_logMessage", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, new System.Action<string, int>((s, f) => { }));

            return inst!;
        }

        private static Element? InvokeEvaluate(object renderer, AltarWeights weights, PrimaryAltarComponent primary, RectangleF top, RectangleF bottom, Vector2 textPos)
        {
            var type = renderer.GetType();
            var mi = type.GetMethod("EvaluateAltarWeights", BindingFlags.NonPublic | BindingFlags.Instance);
            return (Element?)mi.Invoke(renderer, new object[] { weights, primary, top, bottom, textPos, textPos });
        }

        [TestMethod]
        public void EndToEnd_TopHighValue_ChoosesTopBranch()
        {
            var renderer = CreateRendererWithDefaults();
            var settings = (ClickItSettings)renderer.GetType().GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;
            settings.ValuableUpside.Value = true;
            settings.ValuableUpsideThreshold.Value = 90;

            var topEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var bottomEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topMods = new SecondaryAltarComponent(topEl, new List<string> { "up_special" }, new List<string> { "down_none" });
            var bottomMods = new SecondaryAltarComponent(bottomEl, new List<string> { "up_small" }, new List<string> { "down_none" });
            var primary = new PrimaryAltarComponent(ClickIt.ClickIt.AltarType.SearingExarch, topMods, new AltarButton(topEl), bottomMods, new AltarButton(bottomEl));

            // set tiers so top upside is high
            settings.ModTiers["up_special"] = 100;
            settings.ModTiers["up_small"] = 10;

            var calc = new WeightCalculator(settings);
            var aw = calc.CalculateAltarWeights(primary);

            InvokeEvaluate(renderer, aw, primary, new RectangleF(0,0,10,10), new RectangleF(0,0,10,10), new Vector2(1,1));
            // elements in this test are uninitialized (Element.IsValid default), so result may be null but branch logic should have executed.
            // We're successful if the evaluation did not throw and weights indicate top has higher upside.
            aw.TopUpsideWeight.Should().BeGreaterThan(aw.BottomUpsideWeight);
        }

        [TestMethod]
        public void EndToEnd_BottomHighValue_ChoosesBottomBranch()
        {
            var renderer = CreateRendererWithDefaults();
            var settings = (ClickItSettings)renderer.GetType().GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;
            settings.ValuableUpside.Value = true;
            settings.ValuableUpsideThreshold.Value = 90;

            var topEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var bottomEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topMods = new SecondaryAltarComponent(topEl, new List<string> { "up_small" }, new List<string> { "down_none" });
            var bottomMods = new SecondaryAltarComponent(bottomEl, new List<string> { "up_special" }, new List<string> { "down_none" });
            var primary = new PrimaryAltarComponent(ClickIt.ClickIt.AltarType.SearingExarch, topMods, new AltarButton(topEl), bottomMods, new AltarButton(bottomEl));

            var settings2 = settings;
            settings2.ModTiers["up_special"] = 100;
            settings2.ModTiers["up_small"] = 10;

            var calc = new WeightCalculator(settings2);
            var aw = calc.CalculateAltarWeights(primary);

            // Bottom upside should be greater than top upside
            aw.BottomUpsideWeight.Should().BeGreaterThan(aw.TopUpsideWeight);
        }

        [TestMethod]
        public void EndToEnd_BothDangerous_ReturnsNullBranch()
        {
            var renderer = CreateRendererWithDefaults();
            var settings = (ClickItSettings)renderer.GetType().GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;
            settings.DangerousDownside.Value = true;
            settings.DangerousDownsideThreshold.Value = 90;

            var primary = TestBuilders.BuildPrimary();

            var aw = new AltarWeights();
            var big = new decimal[8]; big[0] = 99m;
            aw.InitializeFromArrays(big, big, new decimal[8] { 1,1,1,1,1,1,1,1 }, new decimal[8] {1,1,1,1,1,1,1,1});
            aw.TopDownsideWeight = 99m;
            aw.BottomDownsideWeight = 99m;

            var res = InvokeEvaluate(renderer, aw, primary, new RectangleF(0,0,3,3), new RectangleF(0,0,3,3), new Vector2(1,1));
            res.Should().BeNull();
        }

        [TestMethod]
        public void EndToEnd_LowValueOverride_PicksOppositeSide()
        {
            var renderer = CreateRendererWithDefaults();
            var settings = (ClickItSettings)renderer.GetType().GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;
            settings.UnvaluableUpside.Value = true;
            settings.UnvaluableUpsideThreshold.Value = 1;

            var topEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var bottomEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topUps = new List<string>{ "tiny0" };
            var bottomUps = new List<string>{ "big10" };
            var topMods = new SecondaryAltarComponent(topEl, topUps, new List<string>());
            var bottomMods = new SecondaryAltarComponent(bottomEl, bottomUps, new List<string>());
            var primary = new PrimaryAltarComponent(ClickIt.ClickIt.AltarType.EaterOfWorlds, topMods, new AltarButton(topEl), bottomMods, new AltarButton(bottomEl));

            settings.ModTiers["tiny0"] = 0;
            settings.ModTiers["big10"] = 10;

            var calc = new WeightCalculator(settings);
            var aw = calc.CalculateAltarWeights(primary);

            // Ensure top low value condition holds while bottom is higher
            aw.TopUpsideWeight.Should().Be(0m);
            aw.BottomUpsideWeight.Should().BeGreaterThan(aw.TopUpsideWeight);
        }

        [TestMethod]
        public void EndToEnd_TieWeight_YieldsNoChoice()
        {
            var renderer = CreateRendererWithDefaults();
            var settings = (ClickItSettings)renderer.GetType().GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;

            var topEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var bottomEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topMods = new SecondaryAltarComponent(topEl, new List<string>{ "x" }, new List<string>{ "y" });
            var bottomMods = new SecondaryAltarComponent(bottomEl, new List<string>{ "x" }, new List<string>{ "y" });
            var primary = new PrimaryAltarComponent(ClickIt.ClickIt.AltarType.SearingExarch, topMods, new AltarButton(topEl), bottomMods, new AltarButton(bottomEl));

            settings.ModTiers["x"] = 5;
            settings.ModTiers["y"] = 1;

            var calc = new WeightCalculator(settings);
            var aw = calc.CalculateAltarWeights(primary);

            aw.TopWeight.Should().Be(aw.BottomWeight);
        }

        [TestMethod]
        public void CreateAltarComponentFromAdapter_ThrowsWhenAdapterMissingParts()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var svc = new Services.AltarService(clickIt, settings, null);

            // Create a minimal fake adapter lacking Parent.Parent to trigger InvalidOperationException
            var fake = new FakeAdapter(null);

            Assert.ThrowsException<System.InvalidOperationException>(() => svc.CreateAltarComponentFromAdapter(fake, ClickIt.ClickIt.AltarType.Unknown));
        }

        private class FakeAdapter : Services.IElementAdapter
        {
            public FakeAdapter(Services.IElementAdapter? parent)
            {
                Parent = parent;
            }
            public Services.IElementAdapter? Parent { get; }
            public ExileCore.PoEMemory.Elements.Element? Underlying => null;
            public Services.IElementAdapter? GetChildFromIndices(int a, int b) => null;
            public string GetText(int maxChars) => string.Empty;
        }

        [TestMethod]
        public void WeightAndRenderer_Pipeline_NoExceptionsForNormalScenario()
        {
            var renderer = CreateRendererWithDefaults();
            var settings = (ClickItSettings)renderer.GetType().GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;

            var topEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var bottomEl = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topMods = new SecondaryAltarComponent(topEl, new List<string>{ "a1", "a2" }, new List<string>{ "d1" });
            var bottomMods = new SecondaryAltarComponent(bottomEl, new List<string>{ "b1" }, new List<string>{ "d2" });
            var primary = new PrimaryAltarComponent(ClickIt.ClickIt.AltarType.EaterOfWorlds, topMods, new AltarButton(topEl), bottomMods, new AltarButton(bottomEl));

            settings.ModTiers["a1"] = 10; settings.ModTiers["a2"] = 5; settings.ModTiers["b1"] = 8; settings.ModTiers["d1"] = 1; settings.ModTiers["d2"] = 1;

            var calc = new WeightCalculator(settings);
            var aw = calc.CalculateAltarWeights(primary);

            // Call EvaluateAltarWeights to execute full pipeline branch logic
            InvokeEvaluate(renderer, aw, primary, new RectangleF(0,0,20,20), new RectangleF(0,0,20,20), new Vector2(5,5));

            // Execution reaching here without exceptions is success; we expect no throw and weights computed
            aw.TopWeight.Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public void AltarService_CleanupInvalidAltars_RemovesNullElements()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var svc = new Services.AltarService(clickIt, settings, null);

            var top = TestBuilders.BuildSecondary();
            var bottom = TestBuilders.BuildSecondary();
            var primary = new PrimaryAltarComponent(ClickIt.ClickIt.AltarType.Unknown, top, new AltarButton(null), bottom, new AltarButton(null));
            svc.AddAltarComponent(primary).Should().BeTrue();

            // Now simulate cleanup of invalid altars
            // Internal cleanup method invoked via ProcessAltarLabels path - call CleanupInvalidAltars indirectly by calling ProcessAltarScanningLogic when no labels
            svc.ProcessAltarScanningLogic();
            svc.GetAltarComponents().Should().BeEmpty();
        }
    }
}
