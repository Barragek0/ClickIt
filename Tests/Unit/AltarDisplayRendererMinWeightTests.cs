using System;
using System.Runtime.CompilerServices;
using System.Collections;
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
            // Arrange
            var renderer = (AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(AltarDisplayRenderer));
            var settings = new ClickItSettings(); // MinWeightThresholdEnabled defaults to false
            var dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            // inject fields
            var settingsField = typeof(AltarDisplayRenderer).GetField("_settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dtqField = typeof(AltarDisplayRenderer).GetField("_deferredTextQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dfqField = typeof(AltarDisplayRenderer).GetField("_deferredFrameQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            settingsField.SetValue(renderer, settings);
            dtqField.SetValue(renderer, dtq);
            dfqField.SetValue(renderer, dfq);

            var primary = TestBuilders.BuildPrimary();

            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 50, bottomWeight: 20);
            var topRect = new RectangleF(0, 0, 100, 50);
            var bottomRect = new RectangleF(0, 60, 100, 50);

            // Act
            renderer.DetermineAltarChoice(primary, weights, topRect, bottomRect, new Vector2(0, 0));

            // Assert: top weight > bottom weight -> top highlighted (LawnGreen, thickness 3)
            var itemsObj = dfq.GetType().GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(dfq);
            var items = (System.Collections.IEnumerable)itemsObj;
            items.Should().NotBeNull();
            bool hasTopGreen = false;
            bool hasBottomOrange = false;
            foreach (var entry in items)
            {
                var tuple = (ValueTuple<RectangleF, SharpDX.Color, int>)entry;
                if (tuple.Item1.Equals(topRect) && tuple.Item2 == Color.LawnGreen && tuple.Item3 == 3) hasTopGreen = true;
                if (tuple.Item1.Equals(bottomRect) && tuple.Item2 == Color.OrangeRed && tuple.Item3 == 2) hasBottomOrange = true;
            }

            hasTopGreen.Should().BeTrue();
            hasBottomOrange.Should().BeTrue();
        }

        [TestMethod]
        public void When_Top_below_min_threshold_Bottom_is_chosen()
        {
            // Arrange
            var renderer = (AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(AltarDisplayRenderer));
            var settings = new ClickItSettings();
            settings.MinWeightThresholdEnabled.Value = true;
            settings.MinWeightThreshold.Value = 25;
            var dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            var settingsField = typeof(AltarDisplayRenderer).GetField("_settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dtqField = typeof(AltarDisplayRenderer).GetField("_deferredTextQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dfqField = typeof(AltarDisplayRenderer).GetField("_deferredFrameQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            settingsField.SetValue(renderer, settings);
            dtqField.SetValue(renderer, dtq);
            dfqField.SetValue(renderer, dfq);

            var primary = TestBuilders.BuildPrimary();

            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 1m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 5m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 10, bottomWeight: 50);
            var topRect = new RectangleF(0, 0, 100, 50);
            var bottomRect = new RectangleF(0, 60, 100, 50);

            // Act
            renderer.DetermineAltarChoice(primary, weights, topRect, bottomRect, new Vector2(0, 0));
            // Assert message and frames indicate bottom chosen because top below threshold
            var textItemsObj = dtq.GetType().GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(dtq);
            var textItems = (System.Collections.IEnumerable)textItemsObj;
            textItems.Should().NotBeNull();
            bool foundMsg = false;
            foreach (var entry in textItems)
            {
                var tuple = (ValueTuple<string, Vector2, SharpDX.Color, int, ExileCore.Shared.Enums.FontAlign>)entry;
                if (tuple.Item1.Contains("Bottom has been chosen") || tuple.Item1.Contains("Bottom has been chosen because")) foundMsg = true;
            }
            foundMsg.Should().BeTrue();

            var frameItemsObj = dfq.GetType().GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(dfq);
            var frameItems = (System.Collections.IEnumerable)frameItemsObj;
            bool topOrange = false;
            bool bottomGreen = false;
            foreach (var entry in frameItems)
            {
                var tuple = (ValueTuple<RectangleF, SharpDX.Color, int>)entry;
                if (tuple.Item1.Equals(topRect) && tuple.Item2 == Color.OrangeRed && tuple.Item3 == 3) topOrange = true;
                if (tuple.Item1.Equals(bottomRect) && tuple.Item2 == Color.LawnGreen && tuple.Item3 == 2) bottomGreen = true;
            }

            topOrange.Should().BeTrue();
            bottomGreen.Should().BeTrue();
        }

        [TestMethod]
        public void When_Both_below_min_threshold_Neither_chosen_and_yellow_frames_drawn()
        {
            // Arrange
            var renderer = (AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(AltarDisplayRenderer));
            var settings = new ClickItSettings();
            settings.MinWeightThresholdEnabled.Value = true;
            settings.MinWeightThreshold.Value = 25;
            var dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            var settingsField = typeof(AltarDisplayRenderer).GetField("_settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dtqField = typeof(AltarDisplayRenderer).GetField("_deferredTextQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dfqField = typeof(AltarDisplayRenderer).GetField("_deferredFrameQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            settingsField.SetValue(renderer, settings);
            dtqField.SetValue(renderer, dtq);
            dfqField.SetValue(renderer, dfq);

            var primary = TestBuilders.BuildPrimary();

            var weights = TestBuilders.BuildAltarWeights(
                topDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                bottomDown: new decimal[] { 2m, 0, 0, 0, 0, 0, 0, 0 },
                topUp: new decimal[] { 1m, 0, 0, 0, 0, 0, 0, 0 },
                bottomUp: new decimal[] { 1m, 0, 0, 0, 0, 0, 0, 0 },
                topWeight: 10, bottomWeight: 20);
            var topRect = new RectangleF(0, 0, 100, 50);
            var bottomRect = new RectangleF(0, 60, 100, 50);

            // Act
            renderer.DetermineAltarChoice(primary, weights, topRect, bottomRect, new Vector2(0, 0));
            // Assert: both frames are yellow and no green/orange decision
            var frameItemsObj = dfq.GetType().GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(dfq);
            var frameItems = (System.Collections.IEnumerable)frameItemsObj;
            bool topYellow = false;
            bool bottomYellow = false;
            foreach (var entry in frameItems)
            {
                var tuple = (ValueTuple<RectangleF, SharpDX.Color, int>)entry;
                if (tuple.Item1.Equals(topRect) && tuple.Item2 == Color.Yellow) topYellow = true;
                if (tuple.Item1.Equals(bottomRect) && tuple.Item2 == Color.Yellow) bottomYellow = true;
            }

            topYellow.Should().BeTrue();
            bottomYellow.Should().BeTrue();
        }
    }
}
