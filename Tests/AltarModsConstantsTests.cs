using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Linq;
namespace ClickIt.Tests
{
    [TestClass]
    public class AltarModsConstantsTests
    {
        [TestMethod]
        public void DownsideMods_ShouldNotBeEmpty()
        {
            AltarModsConstants.DownsideMods.Should().NotBeEmpty("Downside mods are required for altar decision-making");
        }
        [TestMethod]
        public void UpsideMods_ShouldNotBeEmpty()
        {
            AltarModsConstants.UpsideMods.Should().NotBeEmpty("Upside mods are required for altar decision-making");
        }
        [TestMethod]
        public void FilterTargetDict_ShouldContainExpectedKeys()
        {
            var expectedKeys = new[] { "Any", "Player", "Minions", "Boss" };
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
            var expectedKeys = new[] { "Player gains:", "Eldritch Minions gain:", "Map boss gains:" };
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
            var idTypePairs = AltarModsConstants.DownsideMods.Select(mod => new { mod.Id, mod.Type });
            idTypePairs.Should().OnlyHaveUniqueItems("Downside mod (ID, Type) pairs should be unique");
        }
        [TestMethod]
        public void UpsideMods_ShouldHaveUniqueIds()
        {
            var idTypePairs = AltarModsConstants.UpsideMods.Select(mod => new { mod.Id, mod.Type });
            idTypePairs.Should().OnlyHaveUniqueItems("Upside mod (ID, Type) pairs should be unique");
        }
        [TestMethod]
        public void ModCollections_ShouldHaveReasonableSize()
        {
            AltarModsConstants.DownsideMods.Should().HaveCountGreaterThan(10, "should have multiple downside mod options");
            AltarModsConstants.UpsideMods.Should().HaveCountGreaterThan(10, "should have multiple upside mod options");
        }
    }
}
