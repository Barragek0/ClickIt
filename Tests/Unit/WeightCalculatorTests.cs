using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt;
using ClickIt.Components;
using ClickIt.Utils;
using ClickIt.Tests.TestUtils;
using System;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class WeightCalculatorTests
    {
        [TestMethod]
        public void CalculateUpsideWeight_ReturnsSumOfModTiers()
        {
            // Arrange
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            var upsides = new List<string> { "mod_a", "mod_b" };

            // Act
            var result = calc.CalculateUpsideWeight(upsides);

            // Assert
            // Default GetModTier returns 1 for unknown mods, so sum should be 2
            result.Should().Be(2m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_ReturnsOnePlusSumWhenNullOrEmpty()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            // null
            calc.CalculateDownsideWeight((System.Collections.Generic.List<string>?)null!).Should().Be(1m);
            // empty
            calc.CalculateDownsideWeight(new List<string>()).Should().Be(1m);
        }

        [TestMethod]
        public void CalculateAltarWeights_ThrowsWhenElementsNull()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            var primary = TestBuilders.BuildPrimary();
            Action act = () => calc.CalculateAltarWeights(primary);
            act.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void CalculateAltarWeights_ReturnsExpectedWeights()
        {
            var settings = new ClickItSettings();
            // Set explicit tiers for composite keys used by GetModWeightFromString
            settings.ModTiers["Player|upA"] = 50;
            settings.ModTiers["Boss|downA"] = 25;

            var calc = new WeightCalculator(settings);

            // Build secondary components with one upside and one downside each
            var top = TestBuilders.BuildSecondary(["Player|upA"], ["Boss|downA"]);
            var bottom = TestBuilders.BuildSecondary(["Player|upA"], ["Boss|downA"]);

            // Provide non-null Element instances for both components by creating uninitialized objects
            var elemType = typeof(ExileCore.PoEMemory.Element);
            var topElem = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(elemType) as ExileCore.PoEMemory.Element;
            var bottomElem = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(elemType) as ExileCore.PoEMemory.Element;
            top.Element = topElem;
            bottom.Element = bottomElem;

            var primary = TestBuilders.BuildPrimary(top, bottom);

            var weights = calc.CalculateAltarWeights(primary);

            // Expect sum weights equal configured tiers
            weights.TopUpsideWeight.Should().Be(50m);
            weights.TopDownsideWeight.Should().Be(25m);
            // Ratio rounded to 2 decimals: 50 / 25 = 2.00
            weights.TopWeight.Should().Be(2.00m);
        }

        [TestMethod]
        public void GetModWeightFromString_UsesCompositeKeyWhenPresent_AndReturnsOneWhenMissing()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["Player|upA"] = 99;
            var calc = new WeightCalculator(settings);

            var ups = new System.Collections.Generic.List<string> { "Player|upA" };
            calc.CalculateUpsideWeight(ups).Should().Be(99m);

            var settings2 = new ClickItSettings();
            settings2.ModTiers["upA"] = 20;
            var calc2 = new WeightCalculator(settings2);
            calc2.CalculateUpsideWeight(new System.Collections.Generic.List<string> { "Player|upA" }).Should().Be(1m);
        }

        [TestMethod]
        public void CalculateWeightFromList_IgnoresNullOrWhitespaceEntries()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["a"] = 5;
            var calc = new WeightCalculator(settings);

            var list = new System.Collections.Generic.List<string?> { "a", "", "   ", null };
            calc.CalculateUpsideWeight(list!).Should().Be(5m);
            calc.CalculateDownsideWeight(list!).Should().Be(1 + 5m);
        }

        [TestMethod]
        public void Private_GetModString_HandlesBoundsAndNullViaReflection()
        {
            var sec = new SecondaryAltarComponent(null, new System.Collections.Generic.List<string> { "up0", "up1" }, new System.Collections.Generic.List<string> { "down0", "down1" }, false);

            var m = typeof(WeightCalculator).GetMethod("GetModString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            m.Should().NotBeNull();

            var valUp = (string)m!.Invoke(null, new object[] { sec, 1, false });
            valUp.Should().Be("up1");

            var valDown = (string)m.Invoke(null, new object[] { sec, 0, true });
            valDown.Should().Be("down0");

            var outVal = (string)m.Invoke(null, new object[] { sec, 10, false });
            outVal.Should().Be("");

            var nullVal = (string)m.Invoke(null, new object?[] { null!, 0, true });
            nullVal.Should().Be("");
        }
    }
}
