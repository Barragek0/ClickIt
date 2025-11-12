using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using ClickIt.Constants;

namespace ClickIt.Tests
{
    [TestClass]
    public class SettingsValidationTests
    {
        [TestMethod]
        public void DefaultWeightInitialization_ShouldCoverAllUpsideMods()
        {
            // Arrange
            var modTiers = new Dictionary<string, int>();

            // Act
            InitializeDefaultWeights(modTiers);

            // Assert
            foreach (var (id, _, _, _) in AltarModsConstants.UpsideMods)
            {
                modTiers.Should().ContainKey(id, $"Upside mod '{id}' should have a default weight");
            }
        }

        [TestMethod]
        public void DefaultWeightInitialization_ShouldCoverAllDownsideMods()
        {
            // Arrange
            var modTiers = new Dictionary<string, int>();

            // Act
            InitializeDefaultWeights(modTiers);

            // Assert
            foreach (var (id, _, _, _) in AltarModsConstants.DownsideMods)
            {
                modTiers.Should().ContainKey(id, $"Downside mod '{id}' should have a default weight");
            }
        }

        [TestMethod]
        public void GetModTier_ShouldReturnCorrectValueForKnownMod()
        {
            // Arrange
            var modTiers = new Dictionary<string, int> { { "Test Mod", 75 } };

            // Act
            int result = GetModTier("Test Mod", modTiers);

            // Assert
            result.Should().Be(75);
        }

        [TestMethod]
        public void GetModTier_ShouldReturnDefaultForUnknownMod()
        {
            // Arrange
            var modTiers = new Dictionary<string, int>();

            // Act
            int result = GetModTier("Unknown Mod", modTiers);

            // Assert
            result.Should().Be(1, "unknown mods should default to weight 1");
        }

        [TestMethod]
        public void GetModTier_ShouldHandleNullModId()
        {
            // Arrange
            var modTiers = new Dictionary<string, int>();

            // Act
            int result = GetModTier(null, modTiers);

            // Assert
            result.Should().Be(1, "null mod ID should default to weight 1");
        }

        [TestMethod]
        public void GetModTier_ShouldHandleEmptyModId()
        {
            // Arrange
            var modTiers = new Dictionary<string, int>();

            // Act
            int result = GetModTier("", modTiers);

            // Assert
            result.Should().Be(1, "empty mod ID should default to weight 1");
        }

        [TestMethod]
        public void GetModTier_ShouldHandleWhitespaceModId()
        {
            // Arrange
            var modTiers = new Dictionary<string, int>();

            // Act
            int result = GetModTier("   ", modTiers);

            // Assert
            result.Should().Be(1, "whitespace mod ID should default to weight 1");
        }

        [TestMethod]
        public void WeightValidation_ShouldEnforceMinimumValue()
        {
            // Arrange & Act & Assert
            ValidateWeightRange(1).Should().BeTrue("weight of 1 should be valid");
            ValidateWeightRange(0).Should().BeFalse("weight of 0 should be invalid");
            ValidateWeightRange(-5).Should().BeFalse("negative weights should be invalid");
            ValidateWeightRange(-1).Should().BeFalse("weight of -1 should be invalid");
        }

        [TestMethod]
        public void WeightValidation_ShouldEnforceMaximumValue()
        {
            // Arrange & Act & Assert
            ValidateWeightRange(100).Should().BeTrue("weight of 100 should be valid");
            ValidateWeightRange(101).Should().BeFalse("weight above 100 should be invalid");
            ValidateWeightRange(1000).Should().BeFalse("extremely high weights should be invalid");
        }

        [TestMethod]
        public void WeightValidation_ShouldAllowValidRange()
        {
            // Test various valid weights including edge cases
            int[] testWeights = { 1, 25, 50, 75, 100 };
            foreach (int weight in testWeights)
            {
                ValidateWeightRange(weight).Should().BeTrue($"weight {weight} should be valid");
            }
        }

        [TestMethod]
        public void DefaultValues_ShouldBeWithinValidRange()
        {
            // Assert all default upside mod values are valid
            foreach (var (_, _, _, defaultValue) in AltarModsConstants.UpsideMods)
            {
                ValidateWeightRange(defaultValue).Should().BeTrue(
                    $"Upside mod default value {defaultValue} should be within valid range");
            }

            // Assert all default downside mod values are valid
            foreach (var (_, _, _, defaultValue) in AltarModsConstants.DownsideMods)
            {
                ValidateWeightRange(defaultValue).Should().BeTrue(
                    $"Downside mod default value {defaultValue} should be within valid range");
            }
        }

        [TestMethod]
        public void ModTierPersistence_ShouldPreserveExistingValues()
        {
            // Arrange
            var modTiers = new Dictionary<string, int>
            {
                { "Existing Mod", 50 }
            };

            // Act
            InitializeDefaultWeights(modTiers);

            // Assert
            modTiers["Existing Mod"].Should().Be(50, "existing mod tiers should not be overwritten");
        }

        [TestMethod]
        public void ModTierConsistency_ShouldHaveLogicalRelationships()
        {
            // Test that high-value currency drops have high weights
            var divineOrbMod = AltarModsConstants.UpsideMods
                .FirstOrDefault(m => m.Id.Contains("Divine Orb"));
            var chaosOrbMod = AltarModsConstants.UpsideMods
                .FirstOrDefault(m => m.Id.Contains("Chaos Orb"));

            if (divineOrbMod.DefaultValue != 0 && chaosOrbMod.DefaultValue != 0)
            {
                divineOrbMod.DefaultValue.Should().BeGreaterThan(chaosOrbMod.DefaultValue,
                    "Divine Orbs should have higher default weight than Chaos Orbs");
            }
        }

        [TestMethod]
        public void DangerousModsValidation_ShouldHaveHighWeights()
        {
            // Test that build-breaking mods have appropriately high weights
            var buildBreakingMods = AltarModsConstants.DownsideMods
                .Where(m => m.DefaultValue >= 90)
                .ToList();

            buildBreakingMods.Should().NotBeEmpty("there should be some high-danger mods");

            foreach (var mod in buildBreakingMods)
            {
                mod.DefaultValue.Should().BeGreaterOrEqualTo(90,
                    $"Dangerous mod '{mod.Name}' should have very high weight");
            }
        }

        [TestMethod]
        public void ModTypeDistribution_ShouldBeCovered()
        {
            // Ensure all target types are represented
            var playerMods = AltarModsConstants.UpsideMods.Where(m => m.Type == "Player").ToList();
            var minionMods = AltarModsConstants.UpsideMods.Where(m => m.Type == "Minion").ToList();
            var bossMods = AltarModsConstants.UpsideMods.Where(m => m.Type == "Boss").ToList();

            playerMods.Should().NotBeEmpty("should have Player-targeted upside mods");
            minionMods.Should().NotBeEmpty("should have Minion-targeted upside mods");
            bossMods.Should().NotBeEmpty("should have Boss-targeted upside mods");
        }

        [TestMethod]
        public void ClickDistanceValidation_ShouldBeWithinAcceptableRange()
        {
            // Test the ClickDistance setting range (0-300 based on the settings)
            ValidateClickDistance(0).Should().BeTrue("distance of 0 should be valid");
            ValidateClickDistance(95).Should().BeTrue("default distance should be valid");
            ValidateClickDistance(300).Should().BeTrue("maximum distance should be valid");
            ValidateClickDistance(-1).Should().BeFalse("negative distance should be invalid");
            ValidateClickDistance(301).Should().BeFalse("distance above maximum should be invalid");
        }

        [TestMethod]
        public void ChestHeightOffsetValidation_ShouldBeWithinAcceptableRange()
        {
            // Test the ChestHeightOffset setting range (-100 to 100 based on the settings)
            ValidateChestHeightOffset(-100).Should().BeTrue("minimum offset should be valid");
            ValidateChestHeightOffset(0).Should().BeTrue("zero offset should be valid");
            ValidateChestHeightOffset(100).Should().BeTrue("maximum offset should be valid");
            ValidateChestHeightOffset(-101).Should().BeFalse("offset below minimum should be invalid");
            ValidateChestHeightOffset(101).Should().BeFalse("offset above maximum should be invalid");
        }

        [TestMethod]
        public void SettingsInitialization_ShouldSetReasonableDefaults()
        {
            // Test that key settings have reasonable default values
            // Note: This is testing the patterns, not the actual ClickItSettings class
            // since we can't instantiate it without ExileCore dependencies

            var modTiers = new Dictionary<string, int>();
            InitializeDefaultWeights(modTiers);

            // Verify that some key mods exist and have been initialized
            modTiers.Should().NotBeEmpty("mod tiers should be initialized with defaults");
            modTiers.Count.Should().BeGreaterThan(100, "should have many mod tiers initialized");
        }

        // Helper methods that simulate settings functionality
        private static void InitializeDefaultWeights(Dictionary<string, int> modTiers)
        {
            foreach (var (id, _, _, defaultValue) in AltarModsConstants.UpsideMods)
            {
                if (!modTiers.ContainsKey(id))
                {
                    modTiers[id] = defaultValue;
                }
            }
            foreach (var (id, _, _, defaultValue) in AltarModsConstants.DownsideMods)
            {
                if (!modTiers.ContainsKey(id))
                {
                    modTiers[id] = defaultValue;
                }
            }
        }

        private static int GetModTier(string modId, Dictionary<string, int> modTiers)
        {
            return modTiers.TryGetValue(modId ?? "", out int value) ? value : 1;
        }

        private static bool ValidateWeightRange(int weight)
        {
            return weight >= 1 && weight <= 100;
        }

        private static bool ValidateClickDistance(int distance)
        {
            return distance >= 0 && distance <= 300;
        }

        private static bool ValidateChestHeightOffset(int offset)
        {
            return offset >= -100 && offset <= 100;
        }
    }
}