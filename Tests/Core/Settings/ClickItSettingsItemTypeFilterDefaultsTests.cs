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

            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier("Metadata/Items/Currency/CurrencyModValues", string.Empty, whitelist).Should().BeTrue();
            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier("Metadata/Items/Currency/CurrencyModValues", string.Empty, blacklist).Should().BeFalse();
            // Scarabs match BOTH lists — the blacklist rejection is what makes the combined filter reject them.
            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier("Metadata/Items/Scarabs/PolishedScarab", string.Empty, whitelist).Should().BeTrue();
            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier("Metadata/Items/Scarabs/PolishedScarab", string.Empty, blacklist).Should().BeTrue();
            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier("Metadata/Items/Maps/MapTier16", string.Empty, whitelist).Should().BeFalse();
        }

        [TestMethod]
        public void MetadataFilter_SpecialHeistContractRules_MatchExpectedNames()
        {
            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Trial Run",
                new[] { "special:heist-quest-contract" }).Should().BeTrue();

            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Bunker",
                new[] { "special:heist-non-quest-contract" }).Should().BeTrue();
        }

        [TestMethod]
        public void MetadataFilter_StoneOfPassageMetadataPath_MatchesExpectedIdentifier()
        {
            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/QuestItems/Incursion/IncursionKey",
                string.Empty,
                item: null,
                "Stone of Passage",
                new[] { "Incursion/IncursionKey" }).Should().BeTrue();

            MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/QuestItems/Incursion/SomeOtherQuestItem",
                string.Empty,
                item: null,
                "Stone of Passage",
                new[] { "Incursion/IncursionKey" }).Should().BeFalse();
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
