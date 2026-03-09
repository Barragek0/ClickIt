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
            clickMetadata.Should().Contain("StrongBoxes/Operative");
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

        [TestMethod]
        public void StrongboxFilters_EnsureInitialization_RemovesUnknownIds()
        {
            var settings = new ClickItSettings
            {
                StrongboxClickIds = new System.Collections.Generic.HashSet<string>(new[] { "arcanist", "not-a-real-id" }, System.StringComparer.OrdinalIgnoreCase),
                StrongboxDontClickIds = new System.Collections.Generic.HashSet<string>(new[] { "another-bad-id" }, System.StringComparer.OrdinalIgnoreCase)
            };

            var clickMetadata = settings.GetStrongboxClickMetadataIdentifiers();
            var dontClickMetadata = settings.GetStrongboxDontClickMetadataIdentifiers();

            clickMetadata.Should().Contain("StrongBoxes/Arcanist");
            clickMetadata.Should().NotContain(x => x.Contains("not-a-real-id", System.StringComparison.OrdinalIgnoreCase));
            dontClickMetadata.Should().NotContain(x => x.Contains("another-bad-id", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
