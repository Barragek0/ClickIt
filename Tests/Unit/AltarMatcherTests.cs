using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services;
using ClickIt.Utils;
using ClickIt.Constants;

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

            // Pre-populate private cache with an entry lacking the 'Type|' prefix
            var cacheField = typeof(AltarMatcher).GetField("_modMatchCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(matcher) as System.Collections.IDictionary;
            string cacheKey = $"{modText}|{negativeModType}";
            if (cache != null)
                cache[cacheKey] = (true, "SomeModId");

            bool result = matcher.TryMatchModCached(modText, negativeModType, out bool isUpside, out string matchedId);

            result.Should().BeTrue();
            isUpside.Should().BeTrue();
            // cached matchedId lacked a pipe; TryMatchModCached should prefix the mod target (Player)
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

            // inspect private cache to ensure the entry was stored
            var textCacheField = typeof(AltarMatcher).GetField("_textCleanCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var textCache = textCacheField?.GetValue(matcher) as System.Collections.IDictionary;
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