// Disabled: duplicate checks - consolidated into `ConstantsTests.cs`
#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Linq;
using ClickIt.Constants;

namespace ClickIt.Tests.Constants
{
    [TestClass]
    public class ConstantsIntegrityTests
    {
        [TestMethod]
        public void FilterAndAltarTargetDicts_ShouldContainExpectedKeys()
        {
            var expectedFilterKeys = new[] { "Any", "Player", "Minions", "Boss" };
            foreach (var k in expectedFilterKeys)
                AltarModsConstants.FilterTargetDict.Should().ContainKey(k);

            var expectedAltarKeys = new[] { "Player gains:", "Eldritch Minions gain:", "Map boss gains:" };
            foreach (var k in expectedAltarKeys)
                AltarModsConstants.AltarTargetDict.Should().ContainKey(k);
        }

        [TestMethod]
        public void ModCollections_ShouldHaveUniqueIdTypePairs()
        {
            var downsidePairs = AltarModsConstants.DownsideMods.Select(m => new { m.Id, m.Type });
            downsidePairs.Should().OnlyHaveUniqueItems("Downside mod (ID, Type) pairs should be unique");

            var upsidePairs = AltarModsConstants.UpsideMods.Select(m => new { m.Id, m.Type });
            upsidePairs.Should().OnlyHaveUniqueItems("Upside mod (ID, Type) pairs should be unique");
        }

        [TestMethod]
        public void ModCollections_ShouldNotBeEmpty()
        {
            AltarModsConstants.UpsideMods.Should().NotBeEmpty();
            AltarModsConstants.DownsideMods.Should().NotBeEmpty();
        }
    }
}
#endif
