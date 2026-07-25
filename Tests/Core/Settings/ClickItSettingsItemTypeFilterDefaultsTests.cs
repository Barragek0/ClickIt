namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ItemTypeFilterDefaultsTests
    {
        [TestMethod]
        public void MetadataFilter_WhitelistAndBlacklistBehavior_IsCorrect()
        {
            var whitelist = new[] { "Items/Currency/", "Items/Scarabs/" };
            var blacklist = new[] { "Items/Scarabs/" };

            MetadataFilterMatcher.Matches("Metadata/Items/Currency/CurrencyModValues", whitelist, blacklist).Should().BeTrue();
            MetadataFilterMatcher.Matches("Metadata/Items/Scarabs/PolishedScarab", whitelist, blacklist).Should().BeFalse();
            MetadataFilterMatcher.Matches("Metadata/Items/Maps/MapTier16", whitelist, blacklist).Should().BeFalse();
        }

        [TestMethod]
        public void MetadataFilter_SpecialHeistContractRules_MatchExpectedNames()
        {
            MetadataFilterMatcher.Matches(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Trial Run",
                new[] { "special:heist-quest-contract" },
                Array.Empty<string>()).Should().BeTrue();

            MetadataFilterMatcher.Matches(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Bunker",
                new[] { "special:heist-non-quest-contract" },
                Array.Empty<string>()).Should().BeTrue();
        }

        [TestMethod]
        public void MetadataFilter_StoneOfPassageMetadataPath_MatchesExpectedIdentifier()
        {
            MetadataFilterMatcher.Matches(
                "Metadata/Items/QuestItems/Incursion/IncursionKey",
                string.Empty,
                "Stone of Passage",
                new[] { "Incursion/IncursionKey" },
                Array.Empty<string>()).Should().BeTrue();

            MetadataFilterMatcher.Matches(
                "Metadata/Items/QuestItems/Incursion/SomeOtherQuestItem",
                string.Empty,
                "Stone of Passage",
                new[] { "Incursion/IncursionKey" },
                Array.Empty<string>()).Should().BeFalse();
        }

        [TestMethod]
        public void ItemTypeCatalog_ContainsStoneOfPassageCategory_AsWhitelist()
        {
            ItemCategoryCatalog.TryGet("stone-of-passage", out ItemCategoryDefinition category).Should().BeTrue();
            category.DefaultList.Should().Be(ItemListKind.Whitelist);
            category.MetadataIdentifiers.Should().ContainSingle(x => x.Equals("Incursion/IncursionKey", StringComparison.OrdinalIgnoreCase));
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
        public void ItemTypeMetadataSnapshots_RemoveIdentifiers_WhenSelectionChanges()
        {
            var settings = new ClickItSettings();

            settings.ItemTypeWhitelistIds = ["gold"];
            settings.ItemTypeBlacklistIds = ["currency"];
            settings.GetItemTypeWhitelistMetadataIdentifiers().Should().Contain("Items/Gold/");

            settings.ItemTypeWhitelistIds = ["currency"];
            settings.ItemTypeBlacklistIds = ["gold"];

            settings.GetItemTypeWhitelistMetadataIdentifiers().Should().NotContain("Items/Gold/");
        }

        [TestMethod]
        public void StrongboxMetadataSnapshots_RemoveIdentifiers_WhenSelectionChanges()
        {
            var settings = new ClickItSettings();

            settings.StrongboxClickIds = ["arcanist"];
            settings.StrongboxDontClickIds = ["artisan"];
            settings.GetStrongboxClickMetadataIdentifiers().Should().Contain(x => x.Contains("Arcanist", StringComparison.OrdinalIgnoreCase));

            settings.StrongboxClickIds = ["artisan"];
            settings.StrongboxDontClickIds = ["arcanist"];

            settings.GetStrongboxClickMetadataIdentifiers().Should().NotContain(x => x.Contains("Arcanist", StringComparison.OrdinalIgnoreCase));
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
                    expectedBlacklist.Add(category.Id);

                else
                    expectedWhitelist.Add(category.Id);

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
        }
    }
}