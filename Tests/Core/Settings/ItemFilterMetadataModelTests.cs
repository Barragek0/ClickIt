namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ItemFilterMetadataModelTests
    {
        private static IEnumerable<string> GetEffectiveMetadataIdentifiers(ClickItSettings settings, string categoryId, bool isWhitelist, bool includeOppositeSubtypeSelections)
        {
            object? result = typeof(ClickItSettings)
                .GetMethod("GetEffectiveMetadataIdentifiers", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [categoryId, isWhitelist, includeOppositeSubtypeSelections]);
            return (IEnumerable<string>)result!;
        }

        [TestMethod]
        public void GetEffectiveMetadataIdentifiers_SelectedSubtypes_ReturnsOnlySelectedSubtypeMetadata()
        {
            var settings = new ClickItSettings();
            settings.ItemTypeWhitelistSubtypeIds["jewels"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "regular-jewels" };

            var metadata = GetEffectiveMetadataIdentifiers(settings, "jewels", isWhitelist: true, includeOppositeSubtypeSelections: false);

            metadata.Should().Contain("special:jewels-regular");
            metadata.Should().NotContain("Items/Jewels/JewelAbyss");
            metadata.Should().NotContain("Items/Jewels/JewelPassiveTreeExpansion");
        }

        [TestMethod]
        public void GetEffectiveMetadataIdentifiers_OppositeSubtypeSelections_ReturnsNonSelectedSubtypes()
        {
            var settings = new ClickItSettings();
            settings.ItemTypeWhitelistSubtypeIds["jewels"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "regular-jewels" };

            var metadata = GetEffectiveMetadataIdentifiers(settings, "jewels", isWhitelist: true, includeOppositeSubtypeSelections: true);

            metadata.Should().Contain("Items/Jewels/JewelAbyss");
            metadata.Should().Contain("Items/Jewels/JewelPassiveTreeExpansion");
            metadata.Should().NotContain("special:jewels-regular");
        }

        [TestMethod]
        public void GetEffectiveMetadataIdentifiers_NoSubtypeSelection_ReturnsCategoryMetadata()
        {
            var settings = new ClickItSettings();
            settings.ItemTypeWhitelistSubtypeIds.Clear();

            var metadata = GetEffectiveMetadataIdentifiers(settings, "jewels", isWhitelist: true, includeOppositeSubtypeSelections: false);

            ItemCategoryCatalog.TryGet("jewels", out ItemCategoryDefinition category).Should().BeTrue();
            metadata.Should().BeEquivalentTo(category.MetadataIdentifiers);
        }

        [TestMethod]
        public void GetEffectiveMetadataIdentifiers_NoSubtypeSelection_OppositeReturnsEmpty()
        {
            var settings = new ClickItSettings();
            settings.ItemTypeWhitelistSubtypeIds.Clear();

            GetEffectiveMetadataIdentifiers(settings, "jewels", isWhitelist: true, includeOppositeSubtypeSelections: true).Should().BeEmpty();
        }

        [TestMethod]
        public void GetEffectiveMetadataIdentifiers_UnknownCategory_ReturnsEmpty()
        {
            var settings = new ClickItSettings();

            GetEffectiveMetadataIdentifiers(settings, "does-not-exist", isWhitelist: true, includeOppositeSubtypeSelections: false).Should().BeEmpty();
        }

        [TestMethod]
        public void GetItemTypeWhitelistMetadataIdentifiers_WithSelectedSubtypes_ExcludesOppositeSubtypeMetadata()
        {
            var settings = new ClickItSettings();
            settings.ItemTypeWhitelistIds = ["jewels"];
            settings.ItemTypeBlacklistIds = [];
            settings.ItemTypeWhitelistSubtypeIds["jewels"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "regular-jewels" };

            var metadata = settings.GetItemTypeWhitelistMetadataIdentifiers();

            metadata.Should().Contain("special:jewels-regular");
            metadata.Should().NotContain("Items/Jewels/JewelAbyss");
            metadata.Should().NotContain("Items/Jewels/JewelPassiveTreeExpansion");
        }
    }
}
