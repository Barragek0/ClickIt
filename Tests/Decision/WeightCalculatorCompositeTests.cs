using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Utils;
using ClickIt.Components;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class WeightCalculatorCompositeTests
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
        public void CalculateAltarWeights_UsesCompositeKeyPerType()
        {
            _settings.ModTiers["Boss|compmod"] = 90;

            var top = new SecondaryAltarComponent(new List<string> { "Boss|compmod" }, new List<string> { "" });
            var bottom = new SecondaryAltarComponent(new List<string> { "" }, new List<string> { "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(90m);
        }

        [TestMethod]
        public void CalculateAltarWeights_CompositeWinsOverIdOnly()
        {
            // id-only set to 50, composite set to 80 -> composite should be used when a composite key is present in mod string
            _settings.ModTiers["modz"] = 50;
            _settings.ModTiers["Boss|modz"] = 80;

            var top = new SecondaryAltarComponent(new List<string> { "Boss|modz" }, new List<string> { "" });
            var bottom = new SecondaryAltarComponent(new List<string> { "" }, new List<string> { "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(80m);
        }

        [TestMethod]
        public void CalculateAltarWeights_MissingComposite_ReturnsFallbackOne()
        {
            // No entry set for the composite key -> fallback to 1 per ClickItSettings.GetModTier behavior
            var top = new SecondaryAltarComponent(new List<string> { "Boss|nonexistent" }, new List<string> { "" });
            var bottom = new SecondaryAltarComponent(new List<string> { "" }, new List<string> { "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(1m);
        }
    }
}
