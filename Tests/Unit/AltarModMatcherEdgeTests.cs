using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using ClickIt.Constants;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarModMatcherEdgeTests
    {
        [TestMethod]
        public void TryMatchMod_ReturnsFalse_ForNullOrEmpty()
        {
            bool ok = AltarModMatcher.TryMatchMod(string.Empty, "Player", out _, out _);
            ok.Should().BeFalse();

            ok = AltarModMatcher.TryMatchMod(null, "Player", out _, out _);
            ok.Should().BeFalse();
        }

        [TestMethod]
        public void TryMatchMod_IgnoresNonLetters_WhenMatching()
        {
            // Take a known downside mod id and inject punctuation/spaces
            var entry = AltarModsConstants.DownsideMods[0];
            string noisy = entry.Id + "!!! 123";

            bool matched = AltarModMatcher.TryMatchMod(noisy, "Player gains:", out bool isUpside, out string matchedId);
            matched.Should().BeTrue();
            isUpside.Should().BeFalse();
            matchedId.Should().Be($"{entry.Type}|{entry.Id}");
        }

        [TestMethod]
        public void TryMatchMod_IsCaseInsensitive()
        {
            var entry = AltarModsConstants.UpsideMods[0];
            string mixed = entry.Id.ToUpperInvariant();

            bool matched = AltarModMatcher.TryMatchMod(mixed, "Eldritch Minions gain:", out bool isUpside, out string matchedId);
            matched.Should().BeTrue();
            isUpside.Should().BeTrue();
            matchedId.Should().Be($"{entry.Type}|{entry.Id}");
        }
    }
}
