using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Linq;

namespace ClickIt.Tests.Constants
{
    [TestClass]
    public class AltarModsConstantsIntegrityTests
    {
        [TestMethod]
        public void UpsideAndDownside_ShouldHaveNoDuplicateCompositeIdTypePairs()
        {
            var all = AltarModsConstants.UpsideMods.Concat(AltarModsConstants.DownsideMods).ToList();
            var compositeKeys = all.Select(t => ($"{t.Type}|{t.Id}").ToLower()).ToList();
            compositeKeys.Should().OnlyHaveUniqueItems();
        }

        [TestMethod]
        public void ModEntries_ShouldHaveNonEmptyNamesAndValidTypes()
        {
            var validTypes = new[] { "Minion", "Boss", "Player" };
            foreach (var (Id, Name, Type, _) in AltarModsConstants.UpsideMods.Concat(AltarModsConstants.DownsideMods))
            {
                Id.Should().NotBeNullOrWhiteSpace();
                Name.Should().NotBeNullOrWhiteSpace();
                Type.Should().NotBeNullOrWhiteSpace();
                validTypes.Should().Contain(Type);
            }
        }

        [TestMethod]
        public void Downside_DefaultWeights_ShouldBeWithin1To100()
        {
            foreach (var (_, _, _, defaultWeight) in AltarModsConstants.DownsideMods)
            {
                defaultWeight.Should().BeInRange(1, 100);
            }
        }

        [DataTestMethod]
        [DataRow("Any")]
        [DataRow("Player")]
        [DataRow("Minions")]
        [DataRow("Boss")]
        public void FilterTargetDict_ShouldContainKey(string key)
        {
            AltarModsConstants.FilterTargetDict.Should().ContainKey(key);
        }

        [DataTestMethod]
        [DataRow("Player gains:")]
        [DataRow("Eldritch Minions gain:")]
        [DataRow("Map boss gains:")]
        public void AltarTargetDict_ShouldContainKey(string key)
        {
            AltarModsConstants.AltarTargetDict.Should().ContainKey(key);
        }

        [TestMethod]
        public void ModCollections_ShouldHaveUniqueIdTypePairs_AndNotBeEmpty()
        {
            // Ensure collections are populated
            AltarModsConstants.UpsideMods.Should().NotBeEmpty();
            AltarModsConstants.DownsideMods.Should().NotBeEmpty();

            // Unique (Id, Type) per side
            var downsidePairs = AltarModsConstants.DownsideMods.Select(m => new { m.Id, m.Type });
            downsidePairs.Should().OnlyHaveUniqueItems("Downside mod (ID, Type) pairs should be unique");

            var upsidePairs = AltarModsConstants.UpsideMods.Select(m => new { m.Id, m.Type });
            upsidePairs.Should().OnlyHaveUniqueItems("Upside mod (ID, Type) pairs should be unique");
        }
    }
}
