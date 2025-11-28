using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarModMatcherTests
    {
        [TestMethod]
        public void GetModTarget_MapsNegativeTypeToTarget()
        {
            AltarModMatcher.GetModTarget("Mapboss").Should().Be("Boss");
            AltarModMatcher.GetModTarget("EldritchMinions").Should().Be("Minion");
            AltarModMatcher.GetModTarget("PlayerSomething").Should().Be("Player");
            AltarModMatcher.GetModTarget("unknown").Should().BeEmpty();
        }

        [TestMethod]
        public void TryMatchMod_ReturnsFalseForUnknown()
        {
            bool isUp;
            string matched;
            var ok = AltarModMatcher.TryMatchMod("nonexistentmod", "", out isUp, out matched);
            ok.Should().BeFalse();
            matched.Should().BeEmpty();
        }
    }
}
