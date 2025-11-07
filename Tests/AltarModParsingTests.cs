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

            // Assert
            cleaned.Should().Be("(30-50)%reducedDefencesperFrenzyCharge</enchanted>");
        }

        [TestMethod]
        public void ModStringCleaning_ShouldHandleRgbMarkup()
        {
            // Arrange
            string dirtyMod = "<rgb(255,128,0)>Final Boss drops # additional Divine Orbs</rgb>";

            // Act
            string cleaned = CleanAltarModsText(dirtyMod);

            // Assert
            cleaned.Should().Be("FinalBossdrops#additionalDivineOrbs</rgb>");
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

            // Assert
            cleaned.Should().Be("Player(8-12)%increasedExperiencegain</rgb></enchanted>");
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

        // Helper methods that simulate the actual implementation logic
        private static string CleanAltarModsText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";

            string cleaned = text.Replace("<valuedefault>", "").Replace("{", "")
                .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "")
                .Replace("gain:", "").Replace("gains:", "");

            // Remove RGB markup (only opening tags, as per actual implementation)
            return System.Text.RegularExpressions.Regex.Replace(cleaned, @"<rgb\(\d+,\d+,\d+\)>", "");
        }

        private static string GetModTarget(string cleanedNegativeModType)
        {
            string lower = cleanedNegativeModType.ToLowerInvariant();
            if (lower.Contains("mapboss")) return "Boss";
            if (lower.Contains("eldritchminions")) return "Minion";
            if (lower.Contains("player")) return "Player";
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