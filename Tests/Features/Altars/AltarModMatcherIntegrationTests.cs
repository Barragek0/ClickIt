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
        public void TryMatchMod_BreachThreadFinalBoss_Matches_WithMapbossNegativeType()
        {
            bool isUp;
            string matched;
            bool ok = AltarModMatcher.TryMatchMod(
                "Final Boss drops 3 additional Breach Threads", "Mapboss", out isUp, out matched);

            ok.Should().BeTrue("Breach Thread Final Boss mod must be catalogued");
            isUp.Should().BeTrue();
            matched.Should().Be("Boss|Final Boss drops # additional Breach Threads");
        }

        [TestMethod]
        public void TryMatchMod_BreachThreadDropChance_Matches_WithEldritchMinionsNegativeType()
        {
            bool isUp;
            string matched;
            bool ok = AltarModMatcher.TryMatchMod(
                "1.6% chance to drop an additional Breach Thread", "EldritchMinions", out isUp, out matched);

            ok.Should().BeTrue("Breach Thread drop chance mod must be catalogued");
            isUp.Should().BeTrue();
            matched.Should().Be("Minion|#% chance to drop an additional Breach Thread");
        }

        [TestMethod]
        public void TryMatchMod_ThreadDuplication_ExpandsGameSingularPluralMarkup()
        {
            bool ok = AltarModMatcher.TryMatchMod(
                "[Thread|Threads] dropped by slain Enemies have 15% chance to be Duplicated",
                "Player gains:",
                out bool isUpside,
                out string matched);

            ok.Should().BeTrue("the game exposes singular/plural markup literally in this altar option");
            isUpside.Should().BeTrue();
            matched.Should().Be("Player|Threads dropped by slain Enemies have #% chance to be Duplicated");
        }

        [TestMethod]
        public void TryMatchMod_BasicCurrencyDuplicateChance_UsesTheDisplayedRange()
        {
            bool lowRangeMatched = AltarModMatcher.TryMatchMod(
                "Basic Currency Items dropped by slain Enemies have 12% chance to be Duplicated",
                "Player gains:",
                out _,
                out string lowRangeId);
            bool highRangeMatched = AltarModMatcher.TryMatchMod(
                "Basic Currency Items dropped by slain Enemies have 20% chance to be Duplicated",
                "Player gains:",
                out _,
                out string highRangeId);

            lowRangeMatched.Should().BeTrue();
            lowRangeId.Should().Be("Player|Basic Currency Items dropped by slain Enemies have #% chance to be Duplicated (10-15)");
            highRangeMatched.Should().BeTrue();
            highRangeId.Should().Be("Player|Basic Currency Items dropped by slain Enemies have #% chance to be Duplicated (15-30)");
        }

        [TestMethod]
        public void TryMatchMod_MultipurposeAltarRolls_MatchesBothRollRanges()
        {
            foreach (string value in new[] { "1.2", "1.6", "2.4", "3.2" })
            {
                bool matched = AltarModMatcher.TryMatchMod(
                    $"{value}% chance to drop an additional Grand Eldritch Ichor",
                    "Eldritch Minions gain:",
                    out bool isUpside,
                    out string matchedId);

                matched.Should().BeTrue($"the game can roll {value}% for this modifier");
                isUpside.Should().BeTrue();
                matchedId.Should().Be("Minion|#% chance to drop an additional Grand Eldritch Ichor");
            }

            foreach (string value in new[] { "50", "70", "80", "130" })
            {
                bool matched = AltarModMatcher.TryMatchMod(
                    $"Gain {value}% of Physical Damage as Extra Cold Damage",
                    "Map boss gains:",
                    out bool isUpside,
                    out string matchedId);

                matched.Should().BeTrue($"the game can roll {value}% for this modifier");
                isUpside.Should().BeFalse();
                matchedId.Should().Be("Boss|Gain #% of Physical Damage as Extra Cold Damage");
            }
        }
    }
}
