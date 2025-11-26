using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt;
using System.Collections.Generic;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class WeightCalculatorTests
    {
        [TestMethod]
        public void CalculateUpsideWeight_SumsModsAndTreatsUnknownAsDefault()
        {
            var settings = new ClickItSettings();
            // set explicit tiers for two mods
            settings.ModTiers["a"] = 5;
            settings.ModTiers["b"] = 10;

            var calc = new WeightCalculator(settings);

            var upsides = new List<string> { "a", "b", "unknown" };
            // unknown will fall back to 1 per ClickItSettings.GetModTier
            var total = calc.CalculateUpsideWeight(upsides);
            total.Should().Be(5 + 10 + 1);
        }

        [TestMethod]
        public void CalculateDownsideWeight_SumsAndAddsOneForMinimum()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["x"] = 2;
            settings.ModTiers["y"] = 3;

            var calc = new WeightCalculator(settings);

            var downs = new List<string> { "x", "y" };
            var total = calc.CalculateDownsideWeight(downs);
            // Downside calculation returns 1 + sum
            total.Should().Be(1 + 2 + 3);
        }

        [TestMethod]
        public void CalculateUpsideWeight_EmptyOrNull_ReturnsZero()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            calc.CalculateUpsideWeight(new List<string>()).Should().Be(0m);
            calc.CalculateUpsideWeight(null).Should().Be(0m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_EmptyOrNull_ReturnsOne()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            calc.CalculateDownsideWeight(new List<string>()).Should().Be(1m);
            calc.CalculateDownsideWeight(null).Should().Be(1m);
        }
    }
}
