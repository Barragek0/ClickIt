using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Linq;

namespace ClickIt.Tests
{
    [TestClass]
    public class ArmourModTests
    {
        [TestMethod]
        public void ArmourMod_ShouldExistForMinions()
        {
            // Verify that the "+# to Armour" mod exists for Minions in DownsideMods
            var armourMods = AltarModsConstants.DownsideMods
                .Where(m => m.Id == "+# to Armour")
                .ToList();

            armourMods.Should().NotBeEmpty("armour mods should exist");

            var minionArmourMod = armourMods.FirstOrDefault(m => m.Type == "Minion");
            minionArmourMod.Should().NotBeNull("minion armour mod should exist");
            minionArmourMod.Name.Should().Be("+(1600-3200) to Armour");
            minionArmourMod.DefaultValue.Should().Be(8);
        }

        [TestMethod]
        public void ArmourMod_ShouldExistForBoss()
        {
            // Verify that the "+# to Armour" mod exists for Boss in DownsideMods
            var armourMods = AltarModsConstants.DownsideMods
                .Where(m => m.Id == "+# to Armour")
                .ToList();

            armourMods.Should().NotBeEmpty("armour mods should exist");

            var bossArmourMod = armourMods.FirstOrDefault(m => m.Type == "Boss");
            bossArmourMod.Should().NotBeNull("boss armour mod should exist");
            bossArmourMod.Name.Should().Be("+50000 to Armour");
            bossArmourMod.DefaultValue.Should().Be(8);
        }

        [TestMethod]
        public void ArmourMod_CleanedStringComparison()
        {
            // Test that the cleaning logic can match the problematic string
            string problematicMod = "+50000toArmour";
            string cleanedMod = new string(problematicMod.Where(char.IsLetter).ToArray());

            cleanedMod.Should().Be("toArmour", "cleaning should remove numbers and symbols");

            // The cleaning logic should be able to match this to our mods
            var testPattern = "+# to Armour";
            string cleanedPattern = new string(testPattern.Where(char.IsLetter).ToArray());

            cleanedPattern.Should().Be("toArmour", "pattern should clean to the same result");
        }

        [TestMethod]
        public void ArmourMod_CaseInsensitiveComparison()
        {
            // Test case insensitive matching
            string[] testVariations = {
                "+50000toarmour",
                "+50000TOARMOUR",
                "+50000ToArmour",
                "+(1600-3200)toarmour"
            };

            foreach (string variation in testVariations)
            {
                string cleaned = new string(variation.Where(char.IsLetter).ToArray()).ToLowerInvariant();
                cleaned.Should().Be("toarmour", $"variation '{variation}' should clean to 'toarmour'");
            }
        }
    }
}