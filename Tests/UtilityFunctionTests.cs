using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Collections.Generic;
using System.Linq;
namespace ClickIt.Tests
{
    [TestClass]
    public class UtilityFunctionTests
    {
        [TestMethod]
        public void AltarModsConstants_UpsideMods_ShouldHaveUniqueNames()
        {
            var names = AltarModsConstants.UpsideMods.Select(m => m.Name).ToList();
            var distinctNames = names.Distinct().ToList();
            distinctNames.Count.Should().Be(names.Count, "all upside mod names should be unique");
        }
        [TestMethod]
        public void AltarModsConstants_DownsideMods_ShouldHaveUniqueNames()
        {
            var modsByType = AltarModsConstants.DownsideMods.GroupBy(m => m.Type);
            foreach (var typeGroup in modsByType)
            {
                var names = typeGroup.Select(m => m.Name).ToList();
                var distinctNames = names.Distinct().ToList();
                distinctNames.Count.Should().Be(names.Count, $"all downside mod names for type '{typeGroup.Key}' should be unique");
            }
        }
        [TestMethod]
        public void AltarModsConstants_AllMods_ShouldHaveValidWeightRanges()
        {
            foreach (var mod in AltarModsConstants.UpsideMods)
            {
                mod.DefaultValue.Should().BeGreaterOrEqualTo(1, $"upside mod '{mod.Id}' should have weight >= 1");
                mod.DefaultValue.Should().BeLessOrEqualTo(100, $"upside mod '{mod.Id}' should have weight <= 100");
            }
            foreach (var mod in AltarModsConstants.DownsideMods)
            {
                mod.DefaultValue.Should().BeGreaterOrEqualTo(1, $"downside mod '{mod.Id}' should have weight >= 1");
                mod.DefaultValue.Should().BeLessOrEqualTo(100, $"downside mod '{mod.Id}' should have weight <= 100");
            }
        }


        [TestMethod]
        public void AltarModsConstants_HighValueMods_ShouldBeIdentifiable()
        {
            var highValueThreshold = 75;
            var highValueUpsides = AltarModsConstants.UpsideMods.Where(m => m.DefaultValue >= highValueThreshold).ToList();
            var highPenaltyDownsides = AltarModsConstants.DownsideMods.Where(m => m.DefaultValue >= highValueThreshold).ToList();
            highValueUpsides.Should().NotBeEmpty("should have some clearly beneficial high-value upside mods");
            highPenaltyDownsides.Should().NotBeEmpty("should have some clearly dangerous high-penalty downside mods");
            foreach (var mod in highValueUpsides)
            {
                mod.Id.Should().NotBeNullOrEmpty($"high-value upside mod should have meaningful ID");
                mod.Name.Should().NotBeNullOrEmpty($"high-value upside mod should have meaningful name");
            }
        }
        [TestMethod]
        public void AltarModsConstants_LowValueMods_ShouldExist()
        {
            var lowValueThreshold = 25;
            var lowValueUpsides = AltarModsConstants.UpsideMods.Where(m => m.DefaultValue <= lowValueThreshold).ToList();
            var lowPenaltyDownsides = AltarModsConstants.DownsideMods.Where(m => m.DefaultValue <= lowValueThreshold).ToList();
            lowValueUpsides.Should().NotBeEmpty("should have some minor benefit upside mods");
            lowPenaltyDownsides.Should().NotBeEmpty("should have some minor penalty downside mods");
        }

        [TestMethod]
        public void ClickItSettings_UpsideMods_HaveAlertEntriesDefaultFalse()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.InitializeDefaultWeights();

            foreach (var mod in AltarModsConstants.UpsideMods)
            {
                string key = $"{mod.Type}|{mod.Id}";
                settings.ModAlerts.Should().ContainKey(key);
                // Divine Orb related upsides are enabled by default
                if ((mod.Type == "Minion" && mod.Id == "#% chance to drop an additional Divine Orb") ||
                    (mod.Type == "Boss" && mod.Id == "Final Boss drops # additional Divine Orbs"))
                {
                    settings.ModAlerts[key].Should().BeTrue();
                }
                else
                {
                    settings.ModAlerts[key].Should().BeFalse();
                }
            }
        }

    }
}
