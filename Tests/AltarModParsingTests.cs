using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using ClickIt.Constants;

namespace ClickIt.Tests
{
    [TestClass]
    public class AltarModParsingTests
    {
        [TestMethod]
        public void ModStringCleaning_ShouldRemoveAllMarkupCorrectly()
        {
            // Arrange
            string dirtyMod = "<valuedefault>{<enchanted>(30-50)% reduced Defences per Frenzy Charge}</enchanted>";

            // Act
            string cleaned = CleanAltarModsText(dirtyMod);

            // Assert: ensure markup removed and key tokens preserved
            cleaned.Should().NotContain("<").And.NotContain(">");
            cleaned.Should().Contain("(30-50)");
            cleaned.Should().Contain("reduced");
            cleaned.Should().Contain("Frenzy");
        }

        [TestMethod]
        public void ModStringCleaning_ShouldHandleRgbMarkup()
        {
            // Arrange
            string dirtyMod = "<rgb(255,128,0)>Final Boss drops # additional Divine Orbs</rgb>";

            // Act
            string cleaned = CleanAltarModsText(dirtyMod);

            // Assert: markup removed and meaningful tokens preserved
            cleaned.Should().NotContain("<").And.NotContain(">");
            cleaned.Should().Contain("FinalBossdrops");
            cleaned.Should().Contain("DivineOrbs");
        }

        [TestMethod]
        public void ModStringCleaning_ShouldHandleEmptyAndNullInput()
        {
            // Act & Assert
            CleanAltarModsText("").Should().Be("");
            CleanAltarModsText(null).Should().Be("");
        }

        [TestMethod]
        public void ModStringCleaning_ShouldHandleComplexNestedMarkup()
        {
            // Arrange
            string complexMod = "<valuedefault>{<enchanted><rgb(100,200,50)>Player gains: (8-12)% increased Experience gain</rgb></enchanted>}";

            // Act
            string cleaned = CleanAltarModsText(complexMod);

            // Assert: markup removed and important pieces present
            cleaned.Should().NotContain("<").And.NotContain(">");
            cleaned.Should().Contain("Player");
            cleaned.Should().Contain("(8-12)");
            cleaned.Should().Contain("Experience");
        }

        [TestMethod]
        public void ModTargetExtraction_ShouldIdentifyPlayerTarget()
        {
            // Arrange
            string negativeModType = "PlayerDropsItemsOnDeath";

            // Act
            string target = GetModTarget(negativeModType);

            // Assert
            target.Should().Be("Player");
        }

        [TestMethod]
        public void ModTargetExtraction_ShouldIdentifyBossTarget()
        {
            // Arrange
            string negativeModType = "MapbossDropsAdditionalItems";

            // Act
            string target = GetModTarget(negativeModType);

            // Assert
            target.Should().Be("Boss");
        }

        [TestMethod]
        public void ModTargetExtraction_ShouldIdentifyMinionTarget()
        {
            // Arrange
            string negativeModType = "EldritchMinionsGainDamage";

            // Act
            string target = GetModTarget(negativeModType);

            // Assert
            target.Should().Be("Minion");
        }

        [TestMethod]
        public void ModTargetExtraction_ShouldReturnEmptyForUnknownTarget()
        {
            // Arrange
            string negativeModType = "UnknownTargetType";

            // Act
            string target = GetModTarget(negativeModType);

            // Assert
            target.Should().Be("");
        }

        [TestMethod]
        public void ModMatching_ShouldMatchExactModWithCorrectTarget()
        {
            // Arrange
            string mod = "FinalBossdrops3additionalDivineOrbs";
            string negativeModType = "MapbossDropsAdditionalItems";

            // Act
            bool matched = TryMatchMod(mod, negativeModType, out bool isUpside, out string matchedId);

            // Assert
            matched.Should().BeTrue();
            isUpside.Should().BeTrue();
            matchedId.Should().Be("Final Boss drops # additional Divine Orbs");
        }

        [TestMethod]
        public void ModMatching_ShouldFailWithWrongTarget()
        {
            // Arrange
            string mod = "FinalBossdrops3additionalDivineOrbs";
            string negativeModType = "PlayerGainsItems"; // Wrong target

            // Act
            bool matched = TryMatchMod(mod, negativeModType, out bool isUpside, out _);

            // Assert
            matched.Should().BeFalse();
            isUpside.Should().BeFalse();
        }

        [TestMethod]
        public void ModMatching_ShouldBeCaseInsensitive()
        {
            // Arrange
            string mod = "finalbossdropsadditionaldivineorbs";
            string negativeModType = "mapbossdropssadditionalitems";

            // Act
            bool matched = TryMatchMod(mod, negativeModType, out bool isUpside, out _);

            // Assert
            matched.Should().BeTrue();
            isUpside.Should().BeTrue();
        }

        [TestMethod]
        public void ModMatching_ShouldMatchWithNumbersAndPunctuation()
        {
            // Arrange: mod string includes numbers and punctuation that should be stripped
            string mod = "Final Boss drops 3 additional Divine Orbs!";
            string negativeModType = "Map boss"; // different formatting but maps to Boss

            // Act
            bool matched = TryMatchMod(mod, negativeModType, out bool isUpside, out string matchedId);

            // Assert
            matched.Should().BeTrue();
            isUpside.Should().BeTrue();
            matchedId.Should().Be("Final Boss drops # additional Divine Orbs");
        }

        [TestMethod]
        public void ModTargetExtraction_ShouldHandleSpacesUnderscoresAndHyphens()
        {
            // Arrange / Act / Assert various formats
            GetModTarget("Map boss").Should().Be("Boss");
            GetModTarget("map_boss").Should().Be("Boss");
            GetModTarget("map-boss").Should().Be("Boss");
            GetModTarget("Eldritch Minions").Should().Be("Minion");
            GetModTarget("player actions").Should().Be("Player");
        }

        [TestMethod]
        public void ModMatching_ShouldNotMatchOnPartialIds()
        {
            // Arrange: partial id should not match exact id
            string mod = "Final Boss drops additional"; // partial
            string negativeModType = "MapbossDropsAdditionalItems";

            // Act
            bool matched = TryMatchMod(mod, negativeModType, out bool isUpside, out _);

            // Assert
            matched.Should().BeFalse();
            isUpside.Should().BeFalse();
        }

        // Helper methods that simulate the actual implementation logic
        private static string CleanAltarModsText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";

            string cleaned = text.Replace("<valuedefault>", "").Replace("{", "")
                .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "")
                .Replace("gain:", "").Replace("gains:", "");

            // Remove any remaining angle-bracket markup (opening or closing tags), including rgb tags
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"<[^>]+>", "");

            // Ensure no stray angle brackets remain
            cleaned = cleaned.Replace("<", "").Replace(">", "");
            return cleaned;
        }

        private static string GetModTarget(string cleanedNegativeModType)
        {
            if (string.IsNullOrEmpty(cleanedNegativeModType)) return "";
            // Normalize: remove non-letters so formats like "Map boss", "map_boss", "map-boss" all map correctly
            string lower = cleanedNegativeModType.ToLowerInvariant();
            string normalized = System.Text.RegularExpressions.Regex.Replace(lower, "[^a-z]", "");
            if (normalized.Contains("mapboss")) return "Boss";
            if (normalized.Contains("eldritchminions")) return "Minion";
            if (normalized.Contains("player")) return "Player";
            return "";
        }

        private static bool TryMatchMod(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            isUpside = false;
            matchedId = string.Empty;

            string cleanedMod = new string(mod.Where(char.IsLetter).ToArray()).ToLowerInvariant();
            string modTarget = GetModTarget(negativeModType);

            // Search in upside mods
            foreach (var (Id, _, Type, _) in AltarModsConstants.UpsideMods)
            {
                string cleanedId = new string(Id.Where(char.IsLetter).ToArray()).ToLowerInvariant();
                if (cleanedId == cleanedMod && Type.Equals(modTarget, System.StringComparison.OrdinalIgnoreCase))
                {
                    isUpside = true;
                    matchedId = Id;
                    return true;
                }
            }

            // Search in downside mods
            foreach (var (Id, _, Type, _) in AltarModsConstants.DownsideMods)
            {
                string cleanedId = new string(Id.Where(char.IsLetter).ToArray()).ToLowerInvariant();
                if (cleanedId == cleanedMod && Type.Equals(modTarget, System.StringComparison.OrdinalIgnoreCase))
                {
                    isUpside = false;
                    matchedId = Id;
                    return true;
                }
            }

            return false;
        }
    }
}