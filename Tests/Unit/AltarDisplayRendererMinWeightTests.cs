using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using ExileCore.PoEMemory.Elements;
using ClickIt.Rendering;
using ClickIt.Utils;
using ClickIt.Tests.TestUtils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererMinWeightTests
    {

        [TestMethod]
        public void When_MinThreshold_Disabled_Normal_weight_selection_occurs()
        {
            var settings = new ClickItSettings(); // MinWeightThresholdEnabled defaults to false
            var (renderer, _, dfq) = AltarDisplayRendererTestHelper.CreateRendererWithQueues(settings);

            var primary = TestBuilders.BuildPrimary();

            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 50, bottomWeight: 20);
            var topRect = new RectangleF(0, 0, 100, 50);
            var bottomRect = new RectangleF(0, 60, 100, 50);

            renderer.DetermineAltarChoice(primary, weights, topRect, bottomRect, new Vector2(0, 0));

            AltarDisplayRendererTestHelper.FrameExists(dfq, topRect, Color.LawnGreen, 3).Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, bottomRect, Color.OrangeRed, 2).Should().BeTrue();
        }

        [TestMethod]
        public void When_Top_below_min_threshold_Bottom_is_chosen()
        {
            var settings = new ClickItSettings();
            settings.MinWeightThresholdEnabled.Value = true;
            settings.MinWeightThreshold.Value = 25;
            var (renderer, dtq, dfq) = AltarDisplayRendererTestHelper.CreateRendererWithQueues(settings);

            var primary = TestBuilders.BuildPrimary();

            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 1m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 10, bottomWeight: 50);
            var topRect = new RectangleF(0, 0, 100, 50);
            var bottomRect = new RectangleF(0, 60, 100, 50);

            renderer.DetermineAltarChoice(primary, weights, topRect, bottomRect, new Vector2(0, 0));
            // Assert message and frames indicate bottom chosen because top below threshold
            AltarDisplayRendererTestHelper.AnyTextContains(dtq, "Bottom has been chosen").Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, topRect, Color.OrangeRed, 3).Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, bottomRect, Color.LawnGreen, 2).Should().BeTrue();
        }

        [TestMethod]
        public void When_Both_below_min_threshold_Neither_chosen_and_yellow_frames_drawn()
        {
            var settings = new ClickItSettings();
            settings.MinWeightThresholdEnabled.Value = true;
            settings.MinWeightThreshold.Value = 25;
            var (renderer, _, dfq) = AltarDisplayRendererTestHelper.CreateRendererWithQueues(settings);

            var primary = TestBuilders.BuildPrimary();

            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 1m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 1m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 10, bottomWeight: 20);
            var topRect = new RectangleF(0, 0, 100, 50);
            var bottomRect = new RectangleF(0, 60, 100, 50);

            renderer.DetermineAltarChoice(primary, weights, topRect, bottomRect, new Vector2(0, 0));
            AltarDisplayRendererTestHelper.FrameExists(dfq, topRect, Color.Yellow).Should().BeTrue();
            AltarDisplayRendererTestHelper.FrameExists(dfq, bottomRect, Color.Yellow).Should().BeTrue();
        }
    }
}
