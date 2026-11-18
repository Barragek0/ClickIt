namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarModMatcherTests
    {
        [TestMethod]
        public void TryMatchMod_ReturnsFalseForUnknown()
        {
            bool isUp;
            string matched;
            var ok = AltarModMatcher.TryMatchMod("nonexistentmod", "", out isUp, out matched);
            ok.Should().BeFalse();
            matched.Should().BeEmpty();
        }

        [DataTestMethod]
        [DataRow("Inflict 1 Grasping Vine on Hit")]
        [DataRow("Inflict1GraspingVineonHit")]
        [DataRow("Inflict1GraspingVinesonHit")]
        public void TryMatchModCached_GraspingVineMinionMod_MatchesRawAndParserCleanedText(string modText)
        {
            var matcher = new AltarMatcher();
            bool matched = matcher.TryMatchModCached(modText, "Eldritch Minions gain:", out bool isUpside, out string matchedId);

            matched.Should().BeTrue("the UI can render singular while the parser strips spaces and the catalog uses plural");
            isUpside.Should().BeFalse();
            matchedId.Should().Be("Minion|Inflict # Grasping Vines on Hit");
        }

        [TestMethod]
        public void NormalizeLetters_StripsNonLetters_AndPreservesLetterOrder()
        {
            AltarModMatcher.NormalizeLetters("Map boss gains: 25% increased Pack Size!")
                .Should().Be("MapbossgainsincreasedPackSize");

            AltarModMatcher.NormalizeLetters("1234 !!!")
                .Should().BeEmpty();
        }

        [TestMethod]
        public void GetModTarget_MapsKnownTargets_AndReturnsEmptyOtherwise()
        {
            AltarModMatcher.GetModTarget("Mapbossgains")
                .Should().Be("Boss");

            AltarModMatcher.GetModTarget("EldritchMinionsgain")
                .Should().Be("Minion");

            AltarModMatcher.GetModTarget("Playergains")
                .Should().Be("Player");

            AltarModMatcher.GetModTarget("Shrinegains")
                .Should().BeEmpty();
        }

        [TestMethod]
        public void TryMatchMod_ReturnsFalse_WhenModNormalizesToEmpty()
        {
            bool matched = AltarModMatcher.TryMatchMod("123 !!!", "Player gains:", out bool isUpside, out string matchedId);

            matched.Should().BeFalse();
            isUpside.Should().BeFalse();
            matchedId.Should().BeEmpty();
        }
    }
}
