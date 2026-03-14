using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services;
using ClickIt.Utils;
using ClickIt.Definitions;
using ClickIt.Tests.TestUtils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarMatcherTests
    {
        [TestMethod]
        public void TryMatchMod_FindsKnownUpsideMod()
        {
            var modEntry = AltarModsConstants.UpsideMods[0];
            string modText = modEntry.Id;
            string negativeModType = "Eldritch Minions gain:";

            bool matched = AltarModMatcher.TryMatchMod(modText, negativeModType, out bool isUpside, out string matchedId);

            matched.Should().BeTrue();
            isUpside.Should().BeTrue();
            matchedId.Should().Be($"{modEntry.Type}|{modEntry.Id}");
        }

        [TestMethod]
        public void TryMatchMod_ReturnsFalse_ForWrongTarget()
        {
            var modEntry = AltarModsConstants.UpsideMods[0];
            string modText = modEntry.Id;
            string negativeModType = "Player gains:";

            bool matched = AltarModMatcher.TryMatchMod(modText, negativeModType, out bool isUpside, out string matchedId);

            matched.Should().BeFalse();
            isUpside.Should().BeFalse();
            matchedId.Should().BeEmpty();
        }

        [TestMethod]
        public void CleanAltarModsText_RemovesTagsBracesAndSpaces()
        {
            var matcher = new AltarMatcher();
            string input = "<rgb(255,255,255)>{Gain} gains: Value <enchanted>";
            string cleaned = matcher.CleanAltarModsText(input);
            cleaned.Should().Be("GainValue");
        }

        [DataTestMethod]
        [DataRow("<rgb(255,255,255)>{Gain} gains: Value <enchanted>")]
        [DataRow("<valuedefault>{A} gain: B gains: C")]
        [DataRow("NoFormattingText")]
        [DataRow("")]
        [DataRow(null)]
        public void CleanAltarModsText_MatchesParserCleaner_ForRepresentativeInputs(string? input)
        {
            var matcher = new AltarMatcher();

            string fromMatcher = matcher.CleanAltarModsText(input!);
            string fromParser = AltarParser.CleanAltarModsText_NoCache(input!);

            fromMatcher.Should().Be(fromParser);
        }

        [TestMethod]
        public void TryMatchModCached_ReturnsExpected_ForKnownMod()
        {
            var matcher = new AltarMatcher();
            var modEntry = AltarModsConstants.DownsideMods[0];
            string modText = modEntry.Id;
            string negativeModType = "Player gains:";

            bool cachedMatched = matcher.TryMatchModCached(modText, negativeModType, out bool isUpside, out string matchedId);
            bool directMatched = AltarModMatcher.TryMatchMod(modText, negativeModType, out bool directIsUpside, out string directMatchedId);

            cachedMatched.Should().Be(directMatched);
            isUpside.Should().Be(directIsUpside);
            matchedId.Should().Be(directMatchedId);
        }

        [TestMethod]
        public void TryMatchModCached_AugmentsCachedMatchedId_WhenMissingTarget()
        {
            var matcher = new AltarMatcher();
            string modText = "SomeModId";
            string negativeModType = "Player gains:";

            var cache = PrivateFieldAccessor.Get<System.Collections.IDictionary>(matcher, "_modMatchCache");
            string cacheKey = $"{modText}|{negativeModType}";
            cache[cacheKey] = (true, "SomeModId");

            bool result = matcher.TryMatchModCached(modText, negativeModType, out bool isUpside, out string matchedId);

            result.Should().BeTrue();
            isUpside.Should().BeTrue();
            matchedId.Should().Be("Player|SomeModId");
        }

        [TestMethod]
        public void CleanAltarModsText_CachesResult_AfterFirstCall()
        {
            var matcher = new AltarMatcher();
            string input = "<rgb(255,255,255)>{Gain} gains: Example <enchanted>";

            string first = matcher.CleanAltarModsText(input);
            string second = matcher.CleanAltarModsText(input);

            first.Should().Be(second);

            var textCache = PrivateFieldAccessor.Get<System.Collections.IDictionary>(matcher, "_textCleanCache");
            textCache.Should().NotBeNull();
            textCache.Contains(input).Should().BeTrue();
        }

        [TestMethod]
        public void GetModTarget_RecognizesTypesCorrectly()
        {
            AltarModMatcher.GetModTarget("Mapboss").Should().Be("Boss");
            AltarModMatcher.GetModTarget("EldritchMinions").Should().Be("Minion");
            AltarModMatcher.GetModTarget("Player").Should().Be("Player");
            AltarModMatcher.GetModTarget("unknown").Should().BeEmpty();
        }
    }
}
