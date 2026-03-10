using System;
using ClickIt.Rendering;
using ClickIt.Tests.TestUtils;
using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererDecisionTreeTests
    {
        [TestMethod]
        public void HighValueOverride_ChoosesTop_WhenTopHasValuableUpside()
        {
            var settings = CreateBaseOverrideSettings();
            settings.ValuableUpside.Value = true;
            settings.ValuableUpsideThreshold.Value = 30;

            var (renderer, dtq, dfq) = AltarDisplayRendererTestHelper.CreateRendererWithQueues(settings);
            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 35m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 10,
                bottomWeight: 90);

            renderer.DetermineAltarChoice(TestBuilders.BuildPrimary(), weights, TopRect, BottomRect, Vector2.Zero);

            AltarDisplayRendererTestHelper.FrameExists(dfq, TopRect, Color.LawnGreen, 3).Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, BottomRect, Color.OrangeRed, 2).Should().BeTrue();
            AltarDisplayRendererTestHelper.AnyTextContains(dtq, "Top has been chosen because one of the top upsides").Should().BeTrue();
        }

        [TestMethod]
        public void DangerousOverride_ChoosesBottom_WhenTopHasDangerousDownside()
        {
            var settings = CreateBaseOverrideSettings();
            settings.DangerousDownside.Value = true;
            settings.DangerousDownsideThreshold.Value = 20;

            var (renderer, dtq, dfq) = AltarDisplayRendererTestHelper.CreateRendererWithQueues(settings);
            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 25m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 10m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 10m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 90,
                bottomWeight: 10);

            renderer.DetermineAltarChoice(TestBuilders.BuildPrimary(), weights, TopRect, BottomRect, Vector2.Zero);

            AltarDisplayRendererTestHelper.FrameExists(dfq, TopRect, Color.OrangeRed, 3).Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, BottomRect, Color.LawnGreen, 2).Should().BeTrue();
            AltarDisplayRendererTestHelper.AnyTextContains(dtq, "Bottom chosen due to top downside").Should().BeTrue();
        }

        [TestMethod]
        public void DangerousOverride_BothDangerous_LeavesChoiceToUser()
        {
            var settings = CreateBaseOverrideSettings();
            settings.DangerousDownside.Value = true;
            settings.DangerousDownsideThreshold.Value = 20;

            var (renderer, dtq, dfq) = AltarDisplayRendererTestHelper.CreateRendererWithQueues(settings);
            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 25m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 30m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 10m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 10m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 50,
                bottomWeight: 50);

            var result = renderer.DetermineAltarChoice(TestBuilders.BuildPrimary(), weights, TopRect, BottomRect, Vector2.Zero);

            result.Should().BeNull();
            AltarDisplayRendererTestHelper.FrameExists(dfq, TopRect, Color.OrangeRed, 2).Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, BottomRect, Color.OrangeRed, 2).Should().BeTrue();
            AltarDisplayRendererTestHelper.AnyTextContains(dtq, "Both options have downsides").Should().BeTrue();
        }

        [TestMethod]
        public void LowValueOverride_ChoosesBottom_WhenTopHasLowValueUpside()
        {
            var settings = CreateBaseOverrideSettings();
            settings.UnvaluableUpside.Value = true;
            settings.UnvaluableUpsideThreshold.Value = 5;

            var (renderer, dtq, dfq) = AltarDisplayRendererTestHelper.CreateRendererWithQueues(settings);
            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 1m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 15m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 90,
                bottomWeight: 10);

            renderer.DetermineAltarChoice(TestBuilders.BuildPrimary(), weights, TopRect, BottomRect, Vector2.Zero);

            AltarDisplayRendererTestHelper.FrameExists(dfq, TopRect, Color.OrangeRed, 3).Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, BottomRect, Color.LawnGreen, 2).Should().BeTrue();
            AltarDisplayRendererTestHelper.AnyTextContains(dtq, "Bottom has been chosen because top has a modifier").Should().BeTrue();
        }

        [TestMethod]
        public void LowValueOverride_BothLowValue_LeavesChoiceToUser()
        {
            var settings = CreateBaseOverrideSettings();
            settings.UnvaluableUpside.Value = true;
            settings.UnvaluableUpsideThreshold.Value = 5;

            var (renderer, dtq, dfq) = AltarDisplayRendererTestHelper.CreateRendererWithQueues(settings);
            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 1m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 90,
                bottomWeight: 10);

            var result = renderer.DetermineAltarChoice(TestBuilders.BuildPrimary(), weights, TopRect, BottomRect, Vector2.Zero);

            result.Should().BeNull();
            AltarDisplayRendererTestHelper.FrameExists(dfq, TopRect, Color.Yellow, 2).Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, BottomRect, Color.Yellow, 2).Should().BeTrue();
            AltarDisplayRendererTestHelper.AnyTextContains(dtq, "Both options have low value modifiers").Should().BeTrue();
        }

        private static ClickItSettings CreateBaseOverrideSettings()
        {
            var settings = new ClickItSettings();
            settings.DangerousDownside.Value = false;
            settings.ValuableUpside.Value = false;
            settings.UnvaluableUpside.Value = false;
            settings.MinWeightThresholdEnabled.Value = false;
            return settings;
        }

        private static readonly RectangleF TopRect = new(0, 0, 100, 50);
        private static readonly RectangleF BottomRect = new(0, 60, 100, 50);
    }
}
