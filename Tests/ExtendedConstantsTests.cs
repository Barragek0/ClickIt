using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Collections.Generic;
using System.Linq;
namespace ClickIt.Tests
{
    [TestClass]
    public class ExtendedConstantsTests
    {
        [TestMethod]
        public void UpsideMods_ShouldHaveConsistentDataFormat()
        {
            foreach (var mod in AltarModsConstants.UpsideMods)
            {
                mod.Id.Should().NotBeNullOrWhiteSpace("mod ID should be valid");
                mod.Name.Should().NotBeNullOrWhiteSpace("mod name should be valid");
                mod.Type.Should().NotBeNullOrWhiteSpace("mod type should be specified");
                mod.DefaultValue.Should().BeInRange(1, 100, "default values should be reasonable weights");
                mod.Id.Length.Should().BeGreaterThan(5, "mod ID should be descriptive");
                mod.Name.Length.Should().BeGreaterThan(5, "mod name should be descriptive");
            }
        }
        [TestMethod]
        public void DownsideMods_ShouldHaveConsistentDataFormat()
        {
            foreach (var mod in AltarModsConstants.DownsideMods)
            {
                mod.Id.Should().NotBeNullOrWhiteSpace("mod ID should be valid");
                mod.Name.Should().NotBeNullOrWhiteSpace("mod name should be valid");
                mod.Type.Should().NotBeNullOrWhiteSpace("mod type should be specified");
                mod.DefaultValue.Should().BeInRange(1, 100, "default values should be reasonable weights");
                mod.Id.Length.Should().BeGreaterThan(5, "mod ID should be descriptive");
                mod.Name.Length.Should().BeGreaterThan(5, "mod name should be descriptive");
            }
        }
        [TestMethod]
        public void UpsideMods_ShouldContainEssentialGameplayMods()
        {
            var essentialUpsides = new[]
            {
                "Currency",
                "Scarab",
                "Map",
                "Gem",
            };
            var allUpsideText = string.Join(" ", AltarModsConstants.UpsideMods.Select(m => m.Id + " " + m.Name));
            foreach (var essential in essentialUpsides)
            {
                allUpsideText.Should().Contain(essential, $"upside mods should include {essential}-related benefits");
            }
        }
        [TestMethod]
        public void DownsideMods_ShouldContainEssentialGameplayMods()
        {
            var essentialDownsides = new[]
            {
                "Resistance",
                "Damage",
                "reflected",
            };
            var allDownsideText = string.Join(" ", AltarModsConstants.DownsideMods.Select(m => m.Id + " " + m.Name));
            foreach (var essential in essentialDownsides)
            {
                allDownsideText.Should().Contain(essential, $"downside mods should include {essential}-related penalties");
            }
        }
        [TestMethod]
        public void FilterTargetDict_ShouldHaveCorrectMappings()
        {
            AltarModsConstants.FilterTargetDict["Any"].Should().Be(AffectedTarget.Any);
            AltarModsConstants.FilterTargetDict["Player"].Should().Be(AffectedTarget.Player);
            AltarModsConstants.FilterTargetDict["Minions"].Should().Be(AffectedTarget.Minions);
            AltarModsConstants.FilterTargetDict["Boss"].Should().Be(AffectedTarget.FinalBoss);
        }
        [TestMethod]
        public void AltarTargetDict_ShouldHaveCorrectMappings()
        {
            AltarModsConstants.AltarTargetDict["Player gains:"].Should().Be(AffectedTarget.Player);
            AltarModsConstants.AltarTargetDict["Eldritch Minions gain:"].Should().Be(AffectedTarget.Minions);
            AltarModsConstants.AltarTargetDict["Map boss gains:"].Should().Be(AffectedTarget.FinalBoss);
        }
        [TestMethod]
        public void ModTypes_ShouldBeValidAndConsistent()
        {
            var validTypes = new[] { "Player", "Boss", "Minion" };
            var allUpsideTypes = AltarModsConstants.UpsideMods.Select(m => m.Type).Distinct();
            var allDownsideTypes = AltarModsConstants.DownsideMods.Select(m => m.Type).Distinct();
            foreach (var type in allUpsideTypes)
            {
                validTypes.Should().Contain(type, $"upside mod type '{type}' should be from valid set");
            }
            foreach (var type in allDownsideTypes)
            {
                validTypes.Should().Contain(type, $"downside mod type '{type}' should be from valid set");
            }
        }
        [TestMethod]
        public void DefaultWeights_ShouldFollowReasonableDistribution()
        {
            var upsideWeights = AltarModsConstants.UpsideMods.Select(m => m.DefaultValue);
            var downsideWeights = AltarModsConstants.DownsideMods.Select(m => m.DefaultValue);
            upsideWeights.Should().Contain(w => w >= 80, "should have some high-value upside mods");
            upsideWeights.Should().Contain(w => w <= 20, "should have some low-value upside mods");
            upsideWeights.Average().Should().BeInRange(20, 70, "average upside weight should be reasonable");
            downsideWeights.Should().Contain(w => w >= 80, "should have some high-penalty downside mods");
            downsideWeights.Should().Contain(w => w <= 20, "should have some low-penalty downside mods");
            downsideWeights.Average().Should().BeInRange(30, 70, "average downside weight should be reasonable");
        }
        [TestMethod]
        public void PlayerTargetedMods_ShouldHaveAppropriateWeights()
        {
            var playerUpsides = AltarModsConstants.UpsideMods.Where(m => m.Type == "Player");
            var playerDownsides = AltarModsConstants.DownsideMods.Where(m => m.Type == "Player");
            playerUpsides.Should().NotBeEmpty("should have player-targeted upside mods");
            playerDownsides.Should().NotBeEmpty("should have player-targeted downside mods");
            playerUpsides.Average(m => m.DefaultValue).Should().BeGreaterThan(10, "player upsides should have meaningful impact");
            playerDownsides.Average(m => m.DefaultValue).Should().BeGreaterThan(10, "player downsides should have meaningful impact");
        }
        [TestMethod]
        public void BossTargetedMods_ShouldExist()
        {
            var bossUpsides = AltarModsConstants.UpsideMods.Where(m => m.Type == "Boss");
            var bossDownsides = AltarModsConstants.DownsideMods.Where(m => m.Type == "Boss");
            bossUpsides.Should().NotBeEmpty("should have boss-targeted upside mods for enhanced rewards");
            bossDownsides.Should().NotBeEmpty("should have boss-targeted downside mods for increased difficulty");
        }
        [TestMethod]
        public void ModCollections_ShouldHaveBalancedCounts()
        {
            var upsideCount = AltarModsConstants.UpsideMods.Count;
            var downsideCount = AltarModsConstants.DownsideMods.Count;
            upsideCount.Should().BeGreaterThan(20, "should have substantial variety in upside mods");
            downsideCount.Should().BeGreaterThan(20, "should have substantial variety in downside mods");
            var ratio = (double)upsideCount / downsideCount;
            ratio.Should().BeInRange(0.5, 2.0, "upside and downside mod counts should be reasonably balanced");
        }
    }
}
