using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt;
using System.Linq;

namespace ClickIt.Tests
{
    /// <summary>
    /// Tests for the AltarModsConstants class to ensure all altar mod collections are properly initialized
    /// and contain valid data for plugin decision-making.
    /// </summary>
    [TestClass]
    public class AltarModsConstantsTests
    {
        [TestMethod]
        public void DownsideMods_ShouldNotBeEmpty()
        {
            // Act & Assert
            AltarModsConstants.DownsideMods.Should().NotBeEmpty("Downside mods are required for altar decision-making");
        }

        [TestMethod]
        public void UpsideMods_ShouldNotBeEmpty()
        {
            // Act & Assert
            AltarModsConstants.UpsideMods.Should().NotBeEmpty("Upside mods are required for altar decision-making");
        }

        [TestMethod]
        public void FilterTargetDict_ShouldContainExpectedKeys()
        {
            // Arrange
            var expectedKeys = new[] { "Any", "Player", "Minions", "Boss" };

            // Act & Assert
            AltarModsConstants.FilterTargetDict.Should().NotBeEmpty("Filter target dictionary is required for target filtering");

            foreach (var expectedKey in expectedKeys)
            {
                AltarModsConstants.FilterTargetDict.Should().ContainKey(expectedKey,
                    $"'{expectedKey}' should be present in filter target dictionary");
            }
        }

        [TestMethod]
        public void AltarTargetDict_ShouldContainExpectedKeys()
        {
            // Arrange
            var expectedKeys = new[] { "Player gains:", "Eldritch Minions gain:", "Map boss gains:" };

            // Act & Assert
            AltarModsConstants.AltarTargetDict.Should().NotBeEmpty("Altar target dictionary is required for altar parsing");

            foreach (var expectedKey in expectedKeys)
            {
                AltarModsConstants.AltarTargetDict.Should().ContainKey(expectedKey,
                    $"'{expectedKey}' should be present in altar target dictionary");
            }
        }

        [TestMethod]
        public void DownsideMods_ShouldHaveValidStructure()
        {
            // Act & Assert
            AltarModsConstants.DownsideMods.Should().AllSatisfy(mod =>
            {
                mod.Id.Should().NotBeNullOrWhiteSpace("Mod ID should be valid");
                mod.Name.Should().NotBeNullOrWhiteSpace("Mod name should be valid");
                mod.Type.Should().NotBeNullOrWhiteSpace("Mod type should be valid");
                mod.DefaultValue.Should().BeInRange(0, 100, "Default value should be a reasonable weight between 0-100");
            });
        }

        [TestMethod]
        public void UpsideMods_ShouldHaveValidStructure()
        {
            // Act & Assert
            AltarModsConstants.UpsideMods.Should().AllSatisfy(mod =>
            {
                mod.Id.Should().NotBeNullOrWhiteSpace("Mod ID should be valid");
                mod.Name.Should().NotBeNullOrWhiteSpace("Mod name should be valid");
                mod.Type.Should().NotBeNullOrWhiteSpace("Mod type should be valid");
                mod.DefaultValue.Should().BeInRange(0, 100, "Default value should be a reasonable weight between 0-100");
            });
        }

        [TestMethod]
        public void DownsideMods_ShouldHaveUniqueIds()
        {
            // Act
            var idTypePairs = AltarModsConstants.DownsideMods.Select(mod => new { mod.Id, mod.Type });

            // Assert
            idTypePairs.Should().OnlyHaveUniqueItems("Downside mod (ID, Type) pairs should be unique");
        }

        [TestMethod]
        public void UpsideMods_ShouldHaveUniqueIds()
        {
            // Act
            var idTypePairs = AltarModsConstants.UpsideMods.Select(mod => new { mod.Id, mod.Type });

            // Assert
            idTypePairs.Should().OnlyHaveUniqueItems("Upside mod (ID, Type) pairs should be unique");
        }

        [TestMethod]
        public void ModCollections_ShouldHaveReasonableSize()
        {
            // Act & Assert - Ensure collections have a reasonable number of mods
            AltarModsConstants.DownsideMods.Should().HaveCountGreaterThan(10, "should have multiple downside mod options");
            AltarModsConstants.UpsideMods.Should().HaveCountGreaterThan(10, "should have multiple upside mod options");
        }
    }
}