using System;
using System.Collections.Generic;
using System.Linq;
using ClickIt.Definitions;
using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ItemTypeFilterDefaultsTests
    {
        [TestMethod]
        public void ItemCategoryCatalog_ContainsRequestedWhitelistAndBlacklistDefaults()
        {
            ItemCategoryCatalog.DefaultWhitelistIds.Should().Contain("currency");
            ItemCategoryCatalog.DefaultWhitelistIds.Should().Contain("unique-items");
            ItemCategoryCatalog.DefaultWhitelistIds.Should().Contain("inscribed-ultimatums");
            ItemCategoryCatalog.DefaultWhitelistIds.Should().Contain("scarabs");
            ItemCategoryCatalog.DefaultWhitelistIds.Should().Contain("heist-contracts");
            ItemCategoryCatalog.DefaultWhitelistIds.Should().Contain("wombgifts");
            ItemCategoryCatalog.DefaultWhitelistIds.Should().NotContain("heist-quest-contracts");
            ItemCategoryCatalog.DefaultWhitelistIds.Should().Contain("maps");

            ItemCategoryCatalog.DefaultBlacklistIds.Should().Contain("armour");
            ItemCategoryCatalog.DefaultBlacklistIds.Should().Contain("weapons");
            ItemCategoryCatalog.DefaultBlacklistIds.Should().Contain("heist-quest-contracts");
            ItemCategoryCatalog.DefaultBlacklistIds.Should().Contain("gold");
        }

        [TestMethod]
        public void ClickItSettings_DefaultsExposeMetadataLists_FromCatalog()
        {
            var settings = new ClickItSettings();

            var whitelistMetadata = settings.GetItemTypeWhitelistMetadataIdentifiers();
            var blacklistMetadata = settings.GetItemTypeBlacklistMetadataIdentifiers();

            whitelistMetadata.Should().Contain(x => x.Contains("Items/Currency/"));
            whitelistMetadata.Should().Contain(x => x.Equals("special:unique-items", StringComparison.OrdinalIgnoreCase));
            whitelistMetadata.Should().Contain(x => x.Equals("special:inscribed-ultimatum", StringComparison.OrdinalIgnoreCase));
            whitelistMetadata.Should().Contain(x => x.Contains("Items/Scarabs/"));
            whitelistMetadata.Should().Contain(x => x.Contains("Items/Currency/Scarabs/"));
            whitelistMetadata.Should().Contain(x => x.Equals("special:heist-non-quest-contract", StringComparison.OrdinalIgnoreCase));
            whitelistMetadata.Should().Contain(x => x.Equals("special:mysterious-wombgift-label", StringComparison.OrdinalIgnoreCase));
            whitelistMetadata.Should().Contain(x => x.Contains("Chayula/EquipmentFruit", StringComparison.OrdinalIgnoreCase));
            whitelistMetadata.Should().Contain(x => x.Contains("Chayula/CurrencyFruit", StringComparison.OrdinalIgnoreCase));
            whitelistMetadata.Should().Contain(x => x.Contains("Chayula/UniqueItemFruit", StringComparison.OrdinalIgnoreCase));
            whitelistMetadata.Should().NotContain(x => x.Equals("special:heist-quest-contract", StringComparison.OrdinalIgnoreCase));

            blacklistMetadata.Should().Contain(x => x.Equals("special:heist-quest-contract", StringComparison.OrdinalIgnoreCase));

            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/"));
            blacklistMetadata.Should().Contain(x => x.Contains("Items/Weapons/"));
            blacklistMetadata.Should().NotContain(x => x.Equals("Items/Currency/", StringComparison.OrdinalIgnoreCase));
            whitelistMetadata.Should().NotContain(x => x.Equals("Items/Armours/", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void MetadataFilter_WhitelistAndBlacklistBehavior_IsCorrect()
        {
            var whitelist = new[] { "Items/Currency/", "Items/Scarabs/" };
            var blacklist = new[] { "Items/Scarabs/" };

            LabelFilterService.MatchesMetadataFiltersForTests("Metadata/Items/Currency/CurrencyModValues", whitelist, blacklist).Should().BeTrue();
            LabelFilterService.MatchesMetadataFiltersForTests("Metadata/Items/Scarabs/PolishedScarab", whitelist, blacklist).Should().BeFalse();
            LabelFilterService.MatchesMetadataFiltersForTests("Metadata/Items/Maps/MapTier16", whitelist, blacklist).Should().BeFalse();
        }

        [TestMethod]
        public void MetadataFilter_EmptyWhitelist_AllowsUnlessBlacklisted()
        {
            var whitelist = Enumerable.Empty<string>().ToArray();
            var blacklist = new[] { "Items/Armours/" };

            LabelFilterService.MatchesMetadataFiltersForTests("Metadata/Items/Weapons/OneHandWeapons/Dagger", whitelist, blacklist).Should().BeTrue();
            LabelFilterService.MatchesMetadataFiltersForTests("Metadata/Items/Armours/Helmets/HelmetInt9", whitelist, blacklist).Should().BeFalse();
        }

        [TestMethod]
        public void MetadataFilter_SpecialHeistContractRules_MatchExpectedNames()
        {
            LabelFilterService.MatchesMetadataFiltersForTests(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Trial Run",
                new[] { "special:heist-quest-contract" },
                Array.Empty<string>()).Should().BeTrue();

            LabelFilterService.MatchesMetadataFiltersForTests(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Trial Run",
                new[] { "special:heist-non-quest-contract" },
                Array.Empty<string>()).Should().BeFalse();

            LabelFilterService.MatchesMetadataFiltersForTests(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Bunker",
                new[] { "special:heist-non-quest-contract" },
                Array.Empty<string>()).Should().BeTrue();
        }

        [TestMethod]
        public void MetadataFilter_NameIdentifier_MatchesItemName()
        {
            LabelFilterService.MatchesMetadataFiltersForTests(
                "Metadata/Items/Jewels/JewelAbyss",
                "Ghastly Eye Jewel",
                new[] { "name:Ghastly Eye Jewel" },
                Array.Empty<string>()).Should().BeTrue();
        }

        [TestMethod]
        public void UnifiedJewels_DefaultSubtypeSplit_PreservesClusterWhitelistBehavior()
        {
            var settings = new ClickItSettings();

            var whitelistMetadata = settings.GetItemTypeWhitelistMetadataIdentifiers();
            var blacklistMetadata = settings.GetItemTypeBlacklistMetadataIdentifiers();

            whitelistMetadata.Should().Contain(x => x.Contains("Items/Jewels/JewelPassiveTreeExpansion", StringComparison.OrdinalIgnoreCase));
            blacklistMetadata.Should().Contain(x => x.Contains("Items/Jewels/JewelAbyss", StringComparison.OrdinalIgnoreCase));
            blacklistMetadata.Should().Contain(x => x.Equals("special:jewels-regular", StringComparison.OrdinalIgnoreCase));
            blacklistMetadata.Should().NotContain(x => x.Contains("Items/Jewels/JewelPassiveTreeExpansion", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void MetadataFilter_SpecialInscribedUltimatumRule_MatchesPath()
        {
            LabelFilterService.MatchesMetadataFiltersForTests(
                "Metadata/Items/Currency/ItemisedTrial/InscribedUltimatum",
                string.Empty,
                new[] { "special:inscribed-ultimatum" },
                Array.Empty<string>()).Should().BeTrue();
        }

        [TestMethod]
        public void MetadataFilter_SpecialMysteriousWombgiftRule_MatchesLabelText()
        {
            LabelFilterService.MatchesMetadataFiltersForTests(
                "Metadata/Items/SomeWeirdPath",
                "Mysterious Wombgift",
                "Mysterious Wombgift",
                new[] { "special:mysterious-wombgift-label" },
                Array.Empty<string>()).Should().BeTrue();

            LabelFilterService.MatchesMetadataFiltersForTests(
                "Metadata/Items/SomeWeirdPath",
                "Mysterious Wombgift",
                "Ancient Wombgift",
                new[] { "special:mysterious-wombgift-label" },
                Array.Empty<string>()).Should().BeFalse();
        }

        [TestMethod]
        public void ItemCategoryCatalog_EdgeCaseMetadataIdentifiers_AreExpectedValues()
        {
            ItemCategoryCatalog.TryGet("labyrinth-trinkets", out var labyrinthTrinkets).Should().BeTrue();
            labyrinthTrinkets.Should().NotBeNull();
            labyrinthTrinkets!.MetadataIdentifiers.Should().Contain("Items/Labyrinth/Trinket");

            ItemCategoryCatalog.TryGet("heist-targets", out var heistTargets).Should().BeTrue();
            heistTargets.Should().NotBeNull();
            heistTargets!.MetadataIdentifiers.Should().Contain("Items/Heist/HeistFinalObjective");

            ItemCategoryCatalog.TryGet("scarabs", out var scarabs).Should().BeTrue();
            scarabs.Should().NotBeNull();
            scarabs!.MetadataIdentifiers.Should().Contain("Items/Scarabs/");
            scarabs!.MetadataIdentifiers.Should().Contain("Items/Currency/Scarabs/");

            ItemCategoryCatalog.TryGet("wombgifts", out var wombgifts).Should().BeTrue();
            wombgifts.Should().NotBeNull();
            wombgifts!.MetadataIdentifiers.Should().Contain("special:mysterious-wombgift-label");
            wombgifts!.MetadataIdentifiers.Should().Contain("Chayula/EquipmentFruit");
            wombgifts!.MetadataIdentifiers.Should().Contain("Chayula/CurrencyFruit");
            wombgifts!.MetadataIdentifiers.Should().Contain("Chayula/UniqueItemFruit");
        }

        [TestMethod]
        public void ItemCategoryCatalog_AllCategories_HaveExampleItems()
        {
            ItemCategoryCatalog.All.Should().OnlyContain(x => x.ExampleItems != null && x.ExampleItems.Count > 0);
        }

        [TestMethod]
        public void ExistingSettings_BackfillMissingItemCategories_ToDefaultList()
        {
            var settings = new ClickItSettings();

            settings.ItemTypeWhitelistIds.Remove("wombgifts");
            settings.ItemTypeBlacklistIds.Remove("wombgifts");

            _ = settings.GetItemTypeWhitelistMetadataIdentifiers();

            settings.ItemTypeWhitelistIds.Should().Contain("wombgifts");
            settings.ItemTypeBlacklistIds.Should().NotContain("wombgifts");
        }

        [TestMethod]
        public void BlacklistMetadata_UsesSelectedArmourSubtypes_WhenConfigured()
        {
            var settings = new ClickItSettings();

            settings.ItemTypeBlacklistSubtypeIds["armour"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "helmets",
                "boots"
            };

            var blacklistMetadata = settings.GetItemTypeBlacklistMetadataIdentifiers();

            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/Helmets/"));
            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/Boots/"));
            blacklistMetadata.Should().NotContain(x => x.Contains("Items/Armours/BodyArmours/"));
        }

        [TestMethod]
        public void BlacklistSubtypeSelection_WhitelistsUnselectedSubtypes()
        {
            var settings = new ClickItSettings();

            settings.ItemTypeBlacklistSubtypeIds["armour"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "helmets"
            };

            var whitelistMetadata = settings.GetItemTypeWhitelistMetadataIdentifiers();
            var blacklistMetadata = settings.GetItemTypeBlacklistMetadataIdentifiers();

            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/Helmets/"));
            whitelistMetadata.Should().Contain(x => x.Contains("Items/Armours/BodyArmours/"));
            whitelistMetadata.Should().Contain(x => x.Contains("Items/Armours/Gloves/"));
            whitelistMetadata.Should().Contain(x => x.Contains("Items/Armours/Boots/"));
            whitelistMetadata.Should().Contain(x => x.Contains("Items/Armours/Shields/"));
            whitelistMetadata.Should().NotContain(x => x.Contains("Items/Armours/Helmets/"));
        }

        [TestMethod]
        public void WhitelistSubtypeSelection_BlacklistsUnselectedSubtypes()
        {
            var settings = new ClickItSettings();

            settings.ItemTypeBlacklistIds.Remove("armour");
            settings.ItemTypeWhitelistIds.Add("armour");
            settings.ItemTypeWhitelistSubtypeIds["armour"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "helmets"
            };

            var whitelistMetadata = settings.GetItemTypeWhitelistMetadataIdentifiers();
            var blacklistMetadata = settings.GetItemTypeBlacklistMetadataIdentifiers();

            whitelistMetadata.Should().Contain(x => x.Contains("Items/Armours/Helmets/"));
            whitelistMetadata.Should().NotContain(x => x.Contains("Items/Armours/BodyArmours/"));
            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/BodyArmours/"));
            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/Gloves/"));
            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/Boots/"));
            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/Shields/"));
            blacklistMetadata.Should().NotContain(x => x.Contains("Items/Armours/Helmets/"));
        }

        [TestMethod]
        public void BlacklistMetadata_FallsBackToBaseCategory_WhenNoSubtypeSelected()
        {
            var settings = new ClickItSettings();

            var blacklistMetadata = settings.GetItemTypeBlacklistMetadataIdentifiers();

            blacklistMetadata.Should().Contain(x => x.Contains("Items/Armours/"));
        }

        [TestMethod]
        public void ItemTypeFilters_RoundTrip_PreservesMembership_ForAllCategories()
        {
            var settings = new ClickItSettings();

            var expectedWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expectedBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ItemCategoryDefinition category in ItemCategoryCatalog.All)
            {
                bool defaultIsWhitelist = ItemCategoryCatalog.DefaultWhitelistIds.Contains(category.Id);
                if (defaultIsWhitelist)
                {
                    expectedBlacklist.Add(category.Id);
                }
                else
                {
                    expectedWhitelist.Add(category.Id);
                }
            }

            settings.ItemTypeWhitelistIds = expectedWhitelist;
            settings.ItemTypeBlacklistIds = expectedBlacklist;
            settings.ItemTypeWhitelistSubtypeIds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            settings.ItemTypeBlacklistSubtypeIds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            string json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<ClickItSettings>(json);

            restored.Should().NotBeNull();
            restored!.ItemTypeWhitelistIds.Should().BeEquivalentTo(expectedWhitelist);
            restored.ItemTypeBlacklistIds.Should().BeEquivalentTo(expectedBlacklist);

            restored.ItemTypeWhitelistIds.Should().NotContain("unique-items");
            restored.ItemTypeBlacklistIds.Should().Contain("unique-items");
        }

    }
}
