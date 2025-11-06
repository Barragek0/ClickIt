using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt.Tests
{
    /// <summary>
    /// Tests for essential AltarModsConstants validation and data integrity checks.
    /// These tests ensure the plugin has all required data for decision-making.
    /// </summary>
    [TestClass]
    public class ExtendedConstantsTests
    {
        [TestMethod]
        public void UpsideMods_ShouldHaveConsistentDataFormat()
        {
            // Act & Assert - Ensure all upside mods follow consistent format
            foreach (var mod in AltarModsConstants.UpsideMods)
            {
                mod.Id.Should().NotBeNullOrWhiteSpace("mod ID should be valid");
                mod.Name.Should().NotBeNullOrWhiteSpace("mod name should be valid");
                mod.Type.Should().NotBeNullOrWhiteSpace("mod type should be specified");
                mod.DefaultValue.Should().BeInRange(1, 100, "default values should be reasonable weights");

                // Ensure ID and Name are meaningful strings
                mod.Id.Length.Should().BeGreaterThan(5, "mod ID should be descriptive");
                mod.Name.Length.Should().BeGreaterThan(5, "mod name should be descriptive");
            }
        }

        [TestMethod]
        public void DownsideMods_ShouldHaveConsistentDataFormat()
        {
            // Act & Assert - Ensure all downside mods follow consistent format
            foreach (var mod in AltarModsConstants.DownsideMods)
            {
                mod.Id.Should().NotBeNullOrWhiteSpace("mod ID should be valid");
                mod.Name.Should().NotBeNullOrWhiteSpace("mod name should be valid");
                mod.Type.Should().NotBeNullOrWhiteSpace("mod type should be specified");
                mod.DefaultValue.Should().BeInRange(1, 100, "default values should be reasonable weights");

                // Ensure ID and Name are meaningful strings
                mod.Id.Length.Should().BeGreaterThan(5, "mod ID should be descriptive");
                mod.Name.Length.Should().BeGreaterThan(5, "mod name should be descriptive");
            }
        }

        [TestMethod]
        public void UpsideMods_ShouldContainEssentialGameplayMods()
        {
            // Arrange - Key upside mods that are essential for altar decisions
            var essentialUpsides = new[]
            {
                "Currency", // Should contain mods related to currency drops
                "Scarab", // Should contain mods related to scarab drops
                "Map", // Should contain mods related to map drops
                "Gem", // Should contain mods related to gem drops
            };

            // Act
            var allUpsideText = string.Join(" ", AltarModsConstants.UpsideMods.Select(m => m.Id + " " + m.Name));

            // Assert - Check that essential gameplay elements are covered
            foreach (var essential in essentialUpsides)
            {
                allUpsideText.Should().Contain(essential, $"upside mods should include {essential}-related benefits");
            }
        }

        [TestMethod]
        public void DownsideMods_ShouldContainEssentialGameplayMods()
        {
            // Arrange - Key downside mods that are essential for altar decisions
            var essentialDownsides = new[]
            {
                "Resistance", // Should contain resistance penalties
                "Damage", // Should contain damage-related penalties
                "reflected", // Should contain reflection mechanics
            };

            // Act
            var allDownsideText = string.Join(" ", AltarModsConstants.DownsideMods.Select(m => m.Id + " " + m.Name));

            // Assert - Check that essential penalty elements are covered
            foreach (var essential in essentialDownsides)
            {
                allDownsideText.Should().Contain(essential, $"downside mods should include {essential}-related penalties");
            }
        }

        [TestMethod]
        public void FilterTargetDict_ShouldHaveCorrectMappings()
        {
            // Act & Assert - Verify target filtering mappings
            AltarModsConstants.FilterTargetDict["Any"].Should().Be(AffectedTarget.Any);
            AltarModsConstants.FilterTargetDict["Player"].Should().Be(AffectedTarget.Player);
            AltarModsConstants.FilterTargetDict["Minions"].Should().Be(AffectedTarget.Minions);
            AltarModsConstants.FilterTargetDict["Boss"].Should().Be(AffectedTarget.FinalBoss);
        }

        [TestMethod]
        public void AltarTargetDict_ShouldHaveCorrectMappings()
        {
            // Act & Assert - Verify altar target parsing mappings
            AltarModsConstants.AltarTargetDict["Player gains:"].Should().Be(AffectedTarget.Player);
            AltarModsConstants.AltarTargetDict["Eldritch Minions gain:"].Should().Be(AffectedTarget.Minions);
            AltarModsConstants.AltarTargetDict["Map boss gains:"].Should().Be(AffectedTarget.FinalBoss);
        }

        [TestMethod]
        public void ModTypes_ShouldBeValidAndConsistent()
        {
            // Arrange
            var validTypes = new[] { "Player", "Boss", "Minion" };

            // Act & Assert - Verify all mod types are from expected set
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
            // Act
            var upsideWeights = AltarModsConstants.UpsideMods.Select(m => m.DefaultValue);
            var downsideWeights = AltarModsConstants.DownsideMods.Select(m => m.DefaultValue);

            // Assert - Check that weights have reasonable distribution
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
            // Act
            var playerUpsides = AltarModsConstants.UpsideMods.Where(m => m.Type == "Player");
            var playerDownsides = AltarModsConstants.DownsideMods.Where(m => m.Type == "Player");

            // Assert - Player-targeted mods should exist and have reasonable weights
            playerUpsides.Should().NotBeEmpty("should have player-targeted upside mods");
            playerDownsides.Should().NotBeEmpty("should have player-targeted downside mods");

            // Player mods should generally have higher impact since they directly affect the player
            playerUpsides.Average(m => m.DefaultValue).Should().BeGreaterThan(10, "player upsides should have meaningful impact");
            playerDownsides.Average(m => m.DefaultValue).Should().BeGreaterThan(10, "player downsides should have meaningful impact");
        }

        [TestMethod]
        public void BossTargetedMods_ShouldExist()
        {
            // Act
            var bossUpsides = AltarModsConstants.UpsideMods.Where(m => m.Type == "Boss");
            var bossDownsides = AltarModsConstants.DownsideMods.Where(m => m.Type == "Boss");

            // Assert - Boss-targeted mods should exist (they affect map boss difficulty/rewards)
            bossUpsides.Should().NotBeEmpty("should have boss-targeted upside mods for enhanced rewards");
            bossDownsides.Should().NotBeEmpty("should have boss-targeted downside mods for increased difficulty");
        }

        [TestMethod]
        public void ModCollections_ShouldHaveBalancedCounts()
        {
            // Act & Assert - Collections should have reasonable relative sizes
            var upsideCount = AltarModsConstants.UpsideMods.Count;
            var downsideCount = AltarModsConstants.DownsideMods.Count;

            upsideCount.Should().BeGreaterThan(20, "should have substantial variety in upside mods");
            downsideCount.Should().BeGreaterThan(20, "should have substantial variety in downside mods");

            // The counts shouldn't be wildly imbalanced
            var ratio = (double)upsideCount / downsideCount;
            ratio.Should().BeInRange(0.5, 2.0, "upside and downside mod counts should be reasonably balanced");
        }
    }
}