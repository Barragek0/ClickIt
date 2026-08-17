namespace ClickIt.Tests.Features.Altars
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

        [TestMethod]
        public void TryMatchMod_TrarthanFinalBossScarab_Matches_WithMapbossNegativeType()
        {
            bool isUp;
            string matched;
            bool ok = AltarModMatcher.TryMatchMod(
                "Final Boss drops 3 additional Trarthan Scarabs", "Mapboss", out isUp, out matched);

            ok.Should().BeTrue("Trarthan Final Boss scarab mod must be catalogued");
            isUp.Should().BeTrue();
            matched.Should().Be("Boss|Final Boss drops # additional Trarthan Scarabs");
        }

        [TestMethod]
        public void TryMatchMod_TrarthanChanceToDropScarab_Matches_WithEldritchMinionsNegativeType()
        {
            bool isUp;
            string matched;
            bool ok = AltarModMatcher.TryMatchMod(
                "1.6% chance to drop an additional Trarthan Scarab", "EldritchMinions", out isUp, out matched);

            ok.Should().BeTrue("Trarthan chance-to-drop scarab mod must be catalogued");
            isUp.Should().BeTrue();
            matched.Should().Be("Minion|#% chance to drop an additional Trarthan Scarab");
        }
    }
}
