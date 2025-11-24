using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class AltarModMatcherTests
    {
        [TestMethod]
        public void GetModTarget_IdentifiesPlayerBossMinion()
        {
            AltarModMatcher.GetModTarget("PlayerDropsItems").Should().Be("Player");
            AltarModMatcher.GetModTarget("MapbossSomething").Should().Be("Boss");
            AltarModMatcher.GetModTarget("EldritchMinionsGiveDamage").Should().Be("Minion");
            AltarModMatcher.GetModTarget("unknown").Should().Be(string.Empty);
        }

        [TestMethod]
        public void TryMatchMod_MatchesKnownUpsideAndDownside()
        {
            // Use a known mod from AltarModsConstants (existing constants in project)
            bool matched = AltarModMatcher.TryMatchMod("FinalBossdropsadditionalDivineOrbs", "MapbossDropsAdditionalItems", out bool isUpside, out string matchedId);
            matched.Should().BeTrue();
            isUpside.Should().BeTrue();
            matchedId.Should().Contain("Divine Orbs");

            // Negative case - different target
            bool fails = AltarModMatcher.TryMatchMod("FinalBossdropsadditionalDivineOrbs", "PlayerDoesSomething", out _, out _);
            fails.Should().BeFalse();
        }
    }
}
