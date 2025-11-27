using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using ClickIt;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class WeightCalculatorParameterizedTests
    {
        [DataTestMethod]
        [DataRow(null, 0.0d)]
        [DataRow("", 0.0d)]
        public void CalculateUpsideWeight_NullOrEmpty_ReturnsZero(object listObj, double expected)
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            List<string> list = listObj as List<string>;
            var result = calc.CalculateUpsideWeight(list);
            ((double)result).Should().Be(expected);
        }

        [TestMethod]
        public void CalculateUpsideWeight_UsesModTiersAndCompositeKeys()
        {
            var settings = new ClickItSettings();
            // set id-only tier
            settings.ModTiers["mod_a"] = 5;
            // set composite tier
            settings.ModTiers["Player|special"] = 7;

            var calc = new WeightCalculator(settings);

            var upsides = new List<string> { "mod_a", "Player|special" };
            var result = calc.CalculateUpsideWeight(upsides);

            result.Should().Be(12m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_NullOrEmpty_ReturnsOne()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            calc.CalculateDownsideWeight(null).Should().Be(1m);
            calc.CalculateDownsideWeight(new List<string>()).Should().Be(1m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_IncludesConfiguredTiers()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["d1"] = 2; // downside tier

            var calc = new WeightCalculator(settings);
            var downsides = new List<string> { "d1" };

            // downside weight should be 1 + sum(tiers)
            calc.CalculateDownsideWeight(downsides).Should().Be(3m);
        }
    }
}
