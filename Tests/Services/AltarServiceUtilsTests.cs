using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class AltarServiceUtilsTests
    {
        // This file previously tested private helpers that were moved to utils/services.
        // Keep a small set of integration-style tests to validate those helpers are wired correctly.



        [TestMethod]
        public void TextHelpers_GetLine_And_CountLines_WorkAsExpected()
        {
            string text = "first\nsecond\nthird";
            ClickIt.Utils.TextHelpers.GetLine(text, 0).Should().Be("first");
            ClickIt.Utils.TextHelpers.GetLine(text, 1).Should().Be("second");
            ClickIt.Utils.TextHelpers.GetLine(text, 2).Should().Be("third");
            ClickIt.Utils.TextHelpers.CountLines(text).Should().Be(3);
        }

        [TestMethod]
        public void AltarModMatcher_GetModTarget_ReturnsExpectedStrings()
        {
            ClickIt.Utils.AltarModMatcher.GetModTarget("Mapboss").Should().Be("Boss");
            ClickIt.Utils.AltarModMatcher.GetModTarget("EldritchMinions").Should().Be("Minion");
            ClickIt.Utils.AltarModMatcher.GetModTarget("Player").Should().Be("Player");
            ClickIt.Utils.AltarModMatcher.GetModTarget("SomethingElse").Should().Be(string.Empty);
        }

        [TestMethod]
        public void AltarModMatcher_TryMatchMod_FindsKnownMod()
        {
            // Use a known downside mod string and negativeModType that maps to Player
            string mod = "Projectiles are fired in random directions";
            string negativeModType = "Player";

            bool matched = ClickIt.Utils.AltarModMatcher.TryMatchMod(mod, negativeModType, out bool isUpside, out string matchedId);
            matched.Should().BeTrue();
            isUpside.Should().BeFalse();
            matchedId.Should().NotBeNullOrEmpty();
            matchedId.ToLowerInvariant().Should().Contain("projectiles");
        }

        [TestMethod]
        public void AltarModMatcher_TryMatchMod_ReturnsFalse_ForUnknownMod()
        {
            string mod = "some unknown mod text";
            string negativeModType = "Player";
            bool matched = ClickIt.Utils.AltarModMatcher.TryMatchMod(mod, negativeModType, out _, out string matchedId);
            matched.Should().BeFalse();
            matchedId.Should().BeEmpty();
        }

        // Cleaning behaviour is tested in AltarModParsingTests which exercises
        // the functionally-equivalent cleaning logic; no direct test against
        // the AltarMatcher instance is required here to keep tests dependency-light.
    }
}
