using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class StrongboxSettingsTableTests
    {
        [TestMethod]
        public void StrongboxFilters_Defaults_IncludeBaseTypesInClickList()
        {
            var settings = new ClickItSettings();

            var clickMetadata = settings.GetStrongboxClickMetadataIdentifiers();

            clickMetadata.Should().Contain("StrongBoxes/Arcanist");
            clickMetadata.Should().Contain("StrongBoxes/Strongbox");
            clickMetadata.Should().Contain("StrongBoxes/Ornate");
        }

        [TestMethod]
        public void StrongboxFilters_Defaults_IncludeUniqueStrongboxesInDontClickList()
        {
            var settings = new ClickItSettings();

            var dontClickMetadata = settings.GetStrongboxDontClickMetadataIdentifiers();

            dontClickMetadata.Should().Contain("name:Perandus Bank");
            dontClickMetadata.Should().Contain("name:Weylam's War Chest");
            dontClickMetadata.Should().Contain("name:Empyrean Apparatus");
        }

        [TestMethod]
        public void StrongboxFilters_EnsureInitialization_RemovesDuplicateIdsAcrossLists()
        {
            var settings = new ClickItSettings
            {
                StrongboxClickIds = new System.Collections.Generic.HashSet<string>(new[] { "arcanist" }, System.StringComparer.OrdinalIgnoreCase),
                StrongboxDontClickIds = new System.Collections.Generic.HashSet<string>(new[] { "arcanist" }, System.StringComparer.OrdinalIgnoreCase)
            };

            var clickMetadata = settings.GetStrongboxClickMetadataIdentifiers();
            var dontClickMetadata = settings.GetStrongboxDontClickMetadataIdentifiers();

            clickMetadata.Should().Contain("StrongBoxes/Arcanist");
            dontClickMetadata.Should().NotContain("StrongBoxes/Arcanist");
        }
    }
}
