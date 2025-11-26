using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt.Constants;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarModMatcherIntegrationTests
    {
        [TestMethod]
        public void TryMatchMod_FindsKnownUpsideMod()
        {
            var entry = AltarModsConstants.UpsideMods[0];
            bool isUp;
            string matched;
            var ok = AltarModMatcher.TryMatchMod(entry.Id, "EldritchMinions", out isUp, out matched);
            ok.Should().BeTrue();
            isUp.Should().BeTrue();
            matched.Should().Contain("|");
        }
    }
}
