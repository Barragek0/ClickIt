using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Utils;
using ClickIt.Components;
using ClickIt.Tests.Shared;
using System;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    [TestCategory("Unit")]
    public class WeightCalculatorParameterizedTests
    {
        private ClickIt.ClickItSettings _settings;
        private WeightCalculator _calc;

        [TestInitialize]
        public void Setup()
        {
            _settings = TestHelpers.CreateSettingsWithTiers();
            _calc = new WeightCalculator(_settings);
        }

        [DataTestMethod]
        [DataRow(new[] { "mod1", "mod2", "" }, new[] { 5, 10 }, 15)]
        [DataRow(new[] { "mod3" }, new[] { 7 }, 7)]
        [DataRow(new string[] { }, new int[] { }, 0)]
        public void CalculateUpsideWeight_VariousInputs(string[] mods, int[] weights, int expected)
        {
            for (int i = 0; i < mods.Length && i < weights.Length; i++)
            {
                if (!string.IsNullOrEmpty(mods[i]))
                    _settings.ModTiers[mods[i]] = weights[i];
            }
            var upsides = new List<string>(mods);
            _calc.CalculateUpsideWeight(upsides).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(new[] { "mod1", "mod2" }, new[] { 2, 3 }, 5)]
        [DataRow(new[] { "mod3" }, new[] { 8 }, 8)]
        [DataRow(new string[] { }, new int[] { }, 0)]
        public void CalculateDownsideWeight_VariousInputs(string[] mods, int[] weights, int expected)
        {
            for (int i = 0; i < mods.Length && i < weights.Length; i++)
            {
                _settings.ModTiers[mods[i]] = weights[i];
            }
            var downsides = new List<string>(mods);
            _calc.CalculateDownsideWeight(downsides).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow("Top|up1", 10, "Top|down1", 5, 2.0)]
        [DataRow("Top|up2", 6, "Top|down2", 3, 2.0)]
        public void CalculateAltarWeights_TopWeight(string topUp, int topUpWeight, string topDown, int topDownWeight, double expectedTop)
        {
            _settings.ModTiers[topUp] = topUpWeight;
            _settings.ModTiers[topDown] = topDownWeight;
            _settings.ModTiers["Bottom|up1"] = 1;
            _settings.ModTiers["Bottom|down1"] = 1;

            var top = new SecondaryAltarComponent(new List<string> { topUp }, new List<string> { topDown });
            var bottom = new SecondaryAltarComponent(new List<string> { "Bottom|up1" }, new List<string> { "Bottom|down1" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopWeight.Should().Be((decimal)expectedTop);
        }

        [TestMethod]
        public void CalculateAltarWeights_NullPrimary_ThrowsArgumentException()
        {
            Action act = () => _calc.CalculateAltarWeights(null);
            act.Should().Throw<ArgumentException>();
        }

        // Additional edge cases can be added here
    }
}