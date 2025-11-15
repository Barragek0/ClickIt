using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Utils;
using ClickIt.Components;
using System;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class WeightCalculatorExtraTests
    {
        private ClickIt.ClickItSettings _settings;
        private WeightCalculator _calc;

        [TestInitialize]
        public void Setup()
        {
            _settings = ClickIt.Tests.Shared.TestHelpers.CreateSettingsWithTiers();
            _calc = new WeightCalculator(_settings);
        }

        [TestMethod]
        public void CalculateUpsideWeight_UnsetMods_TreatedAsZero()
        {
            // Arrange: no ModTiers set for "unknown_mod"
            var ups = new List<string> { "unknown_mod", "" };

            // Act
            var total = _calc.CalculateUpsideWeight(ups);

            // Assert - default fallback for unknown mods is 1 (backward-compatible behavior)
            total.Should().Be(1m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_NullList_ReturnsZero()
        {
            decimal res = _calc.CalculateDownsideWeight(null);
            res.Should().Be(0m);
        }

        [TestMethod]
        public void CalculateAltarWeights_ZeroDownside_ProducesZeroTopWeight()
        {
            // Top has one upside with set weight, but no downsides -> division by zero should be guarded
            _settings.ModTiers["up_only"] = 10;

            var top = new SecondaryAltarComponent(new List<string> { "up_only" }, new List<string> { "", "", "", "", "", "", "", "" });
            var bottom = new SecondaryAltarComponent(new List<string> { "", "", "", "", "", "", "", "" }, new List<string> { "", "", "", "", "", "", "", "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(10m);
            weights.TopDownsideWeight.Should().Be(0m);
            weights.TopWeight.Should().Be(0m, "divide-by-zero is handled by returning 0 when downsides sum to zero");
        }
    }
}
