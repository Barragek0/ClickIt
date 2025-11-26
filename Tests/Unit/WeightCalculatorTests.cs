using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt;
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
            calc.CalculateDownsideWeight(null).Should().Be(1m);
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
    }
}
