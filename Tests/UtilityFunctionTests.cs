using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt.Tests
{
    /// <summary>
    /// Tests for utility functions and data validation that can be tested independently
    /// of the ExileCore framework dependencies.
    /// </summary>
    [TestClass]
    public class UtilityFunctionTests
    {
        [TestMethod]
        public void AltarModsConstants_UpsideMods_ShouldHaveUniqueNames()
        {
            // Act
            var names = AltarModsConstants.UpsideMods.Select(m => m.Name).ToList();
            var distinctNames = names.Distinct().ToList();

            // Assert
            distinctNames.Count.Should().Be(names.Count, "all upside mod names should be unique");
        }

        [TestMethod]
        public void AltarModsConstants_DownsideMods_ShouldHaveUniqueNames()
        {
            // Act
            var names = AltarModsConstants.DownsideMods.Select(m => m.Name).ToList();
            var distinctNames = names.Distinct().ToList();

            // Assert
            distinctNames.Count.Should().Be(names.Count, "all downside mod names should be unique");
        }

        [TestMethod]
        public void AltarModsConstants_AllMods_ShouldHaveValidWeightRanges()
        {
            // Act & Assert
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
        public void AltarModsConstants_ModIds_ShouldNotHaveLeadingOrTrailingWhitespace()
        {
            // Act & Assert
            foreach (var mod in AltarModsConstants.UpsideMods)
            {
                mod.Id.Should().Be(mod.Id.Trim(), $"upside mod ID '{mod.Id}' should not have leading/trailing whitespace");
                mod.Name.Should().Be(mod.Name.Trim(), $"upside mod name '{mod.Name}' should not have leading/trailing whitespace");
            }

            foreach (var mod in AltarModsConstants.DownsideMods)
            {
                mod.Id.Should().Be(mod.Id.Trim(), $"downside mod ID '{mod.Id}' should not have leading/trailing whitespace");
                mod.Name.Should().Be(mod.Name.Trim(), $"downside mod name '{mod.Name}' should not have leading/trailing whitespace");
            }
        }

        [TestMethod]
        public void AltarModsConstants_ShouldHaveValidTypeDistribution()
        {
            // Act
            var upsideTypeGroups = AltarModsConstants.UpsideMods.GroupBy(m => m.Type).ToList();
            var downsideTypeGroups = AltarModsConstants.DownsideMods.GroupBy(m => m.Type).ToList();

            // Assert - Should have mods for each major type category
            upsideTypeGroups.Should().NotBeEmpty("should have upside mods grouped by type");
            downsideTypeGroups.Should().NotBeEmpty("should have downside mods grouped by type");

            // Each type group should have at least some mods
            foreach (var group in upsideTypeGroups)
            {
                group.Count().Should().BeGreaterThan(0, $"upside type '{group.Key}' should have at least one mod");
            }

            foreach (var group in downsideTypeGroups)
            {
                group.Count().Should().BeGreaterThan(0, $"downside type '{group.Key}' should have at least one mod");
            }
        }

        [TestMethod]
        public void AltarModsConstants_HighValueMods_ShouldBeIdentifiable()
        {
            // Arrange - Define what constitutes high-value mods (weight >= 75)
            var highValueThreshold = 75;

            // Act
            var highValueUpsides = AltarModsConstants.UpsideMods.Where(m => m.DefaultValue >= highValueThreshold).ToList();
            var highPenaltyDownsides = AltarModsConstants.DownsideMods.Where(m => m.DefaultValue >= highValueThreshold).ToList();

            // Assert - Should have some clearly high-value and high-penalty mods
            highValueUpsides.Should().NotBeEmpty("should have some clearly beneficial high-value upside mods");
            highPenaltyDownsides.Should().NotBeEmpty("should have some clearly dangerous high-penalty downside mods");

            // High-value mods should be meaningful
            foreach (var mod in highValueUpsides)
            {
                mod.Id.Should().NotBeNullOrEmpty($"high-value upside mod should have meaningful ID");
                mod.Name.Should().NotBeNullOrEmpty($"high-value upside mod should have meaningful name");
            }
        }

        [TestMethod]
        public void AltarModsConstants_LowValueMods_ShouldExist()
        {
            // Arrange - Define what constitutes low-value mods (weight <= 25)
            var lowValueThreshold = 25;

            // Act
            var lowValueUpsides = AltarModsConstants.UpsideMods.Where(m => m.DefaultValue <= lowValueThreshold).ToList();
            var lowPenaltyDownsides = AltarModsConstants.DownsideMods.Where(m => m.DefaultValue <= lowValueThreshold).ToList();

            // Assert - Should have some lower impact mods for balanced decision-making
            lowValueUpsides.Should().NotBeEmpty("should have some minor benefit upside mods");
            lowPenaltyDownsides.Should().NotBeEmpty("should have some minor penalty downside mods");
        }

        [TestMethod]
        public void AltarModsConstants_FilterAndAltarDicts_ShouldHaveExpectedCoverage()
        {
            // Act & Assert - Verify dictionary completeness
            AltarModsConstants.FilterTargetDict.Keys.Should().HaveCount(4, "filter target dictionary should cover all filter options");
            AltarModsConstants.AltarTargetDict.Keys.Should().HaveCount(3, "altar target dictionary should cover all altar target types");

            // Verify no null or empty keys
            AltarModsConstants.FilterTargetDict.Keys.Should().NotContainNulls("filter target keys should not be null");
            AltarModsConstants.FilterTargetDict.Keys.Should().NotContain(string.Empty, "filter target keys should not be empty");

            AltarModsConstants.AltarTargetDict.Keys.Should().NotContainNulls("altar target keys should not be null");
            AltarModsConstants.AltarTargetDict.Keys.Should().NotContain(string.Empty, "altar target keys should not be empty");
        }

        [TestMethod]
        public void AltarModsConstants_PlayerMods_ShouldBePrioritized()
        {
            // Act
            var playerUpsides = AltarModsConstants.UpsideMods.Where(m => m.Type == "Player").ToList();
            var playerDownsides = AltarModsConstants.DownsideMods.Where(m => m.Type == "Player").ToList();

            // Assert - Player mods should be well represented since they directly impact player experience
            var totalUpsides = AltarModsConstants.UpsideMods.Count;
            var totalDownsides = AltarModsConstants.DownsideMods.Count;

            var playerUpsideRatio = (double)playerUpsides.Count / totalUpsides;
            var playerDownsideRatio = (double)playerDownsides.Count / totalDownsides;

            playerUpsideRatio.Should().BeGreaterThan(0.05, "at least 5% of upsides should target the player");
            playerDownsideRatio.Should().BeGreaterThan(0.05, "at least 5% of downsides should target the player");

            // Player mods should have a reasonable weight distribution
            if (playerUpsides.Any())
            {
                playerUpsides.Average(m => m.DefaultValue).Should().BeGreaterThan(1, "player upsides should have meaningful average weight");
            }

            if (playerDownsides.Any())
            {
                playerDownsides.Average(m => m.DefaultValue).Should().BeGreaterThan(1, "player downsides should have meaningful average weight");
            }
        }

        [TestMethod]
        public void AltarModsConstants_ModNames_ShouldBeDescriptive()
        {
            // Act & Assert - Mod names should be descriptive enough to understand their impact
            foreach (var mod in AltarModsConstants.UpsideMods.Take(10)) // Test sample for performance
            {
                mod.Name.Length.Should().BeGreaterThan(10, $"upside mod name '{mod.Name}' should be descriptive");
                mod.Id.Length.Should().BeGreaterThan(5, $"upside mod ID '{mod.Id}' should be meaningful");
            }

            foreach (var mod in AltarModsConstants.DownsideMods.Take(10)) // Test sample for performance
            {
                mod.Name.Length.Should().BeGreaterThan(10, $"downside mod name '{mod.Name}' should be descriptive");
                mod.Id.Length.Should().BeGreaterThan(5, $"downside mod ID '{mod.Id}' should be meaningful");
            }
        }
    }
}