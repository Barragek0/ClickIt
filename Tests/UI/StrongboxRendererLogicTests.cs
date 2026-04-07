namespace ClickIt.Tests.UI
{
    [TestClass]
    public class StrongboxRendererLogicTests
    {
        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsTrue_WhenSpecialIdentifierExists()
        {
            bool contains = StrongboxRenderer.ContainsStrongboxUniqueIdentifier(
                ["metadata/a", "special:strongbox-unique", "metadata/b"]);

            contains.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsFalse_WhenMissingOrEmpty()
        {
            StrongboxRenderer.ContainsStrongboxUniqueIdentifier([]).Should().BeFalse();
            StrongboxRenderer.ContainsStrongboxUniqueIdentifier(["metadata/a", "metadata/b"]).Should().BeFalse();
            StrongboxRenderer.ContainsStrongboxUniqueIdentifier(null).Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_UniqueStrongbox_RespectsUniqueIdentifierLists()
        {
            string path = "Metadata/Chests/StrongBoxes/Arcanist";
            string name = "Arcanist's Strongbox";

            bool clickable = StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "special:strongbox-unique" },
                new List<string>(),
                true);

            bool blocked = StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "special:strongbox-unique" },
                new List<string> { "special:strongbox-unique" },
                true);

            clickable.Should().BeTrue();
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_NonUnique_UsesDontClickPrecedence()
        {
            string path = "Metadata/Chests/StrongBoxes/Arcanist";
            string name = "Arcanist's Strongbox";

            bool clickable = StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "StrongBoxes/Arcanist" },
                new List<string>(),
                false);

            bool blocked = StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "StrongBoxes/Arcanist" },
                new List<string> { "StrongBoxes/Arcanist" },
                false);

            clickable.Should().BeTrue();
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_ReturnsFalse_WhenPathOrClickMetadataIsMissing()
        {
            StrongboxRenderer.IsStrongboxClickableBySettings(
                string.Empty,
                "Arcanist's Strongbox",
                ["StrongBoxes/Arcanist"],
                [],
                false).Should().BeFalse();

            StrongboxRenderer.IsStrongboxClickableBySettings(
                "Metadata/Chests/StrongBoxes/Arcanist",
                "Arcanist's Strongbox",
                [],
                [],
                false).Should().BeFalse();
        }

        [TestMethod]
        public void HasMatchingSnapshot_ReturnsExpected_ForNullAndEquivalentSets()
        {
            InvokeHasMatchingSnapshot(currentIds: null, snapshot: null).Should().BeTrue();
            InvokeHasMatchingSnapshot(currentIds: ["arcanist"], snapshot: null).Should().BeFalse();
            InvokeHasMatchingSnapshot(currentIds: ["arcanist", "artisan"], snapshot: ["artisan", "arcanist"]).Should().BeTrue();
            InvokeHasMatchingSnapshot(currentIds: ["arcanist"], snapshot: ["artisan"]).Should().BeFalse();
        }

        [TestMethod]
        public void EnsureStrongboxMetadataCache_RefreshesCachedMetadata_WhenSettingsChange()
        {
            var settings = new ClickItSettings
            {
                StrongboxClickIds = ["arcanist"],
                StrongboxDontClickIds = ["artisan"]
            };
            var renderer = new StrongboxRenderer(settings, new DeferredFrameQueue());

            InvokeEnsureStrongboxMetadataCache(renderer);

            IReadOnlyList<string> initialClickMetadata = GetCachedMetadata(renderer, "_cachedClickMetadata");
            IReadOnlyList<string> initialDontClickMetadata = GetCachedMetadata(renderer, "_cachedDontClickMetadata");

            initialClickMetadata.Should().Contain(metadata => metadata.Contains("Arcanist", StringComparison.OrdinalIgnoreCase));
            initialDontClickMetadata.Should().Contain(metadata => metadata.Contains("Artisan", StringComparison.OrdinalIgnoreCase));

            settings.StrongboxClickIds = ["artisan"];
            settings.StrongboxDontClickIds = ["arcanist"];

            InvokeEnsureStrongboxMetadataCache(renderer);

            IReadOnlyList<string> updatedClickMetadata = GetCachedMetadata(renderer, "_cachedClickMetadata");
            IReadOnlyList<string> updatedDontClickMetadata = GetCachedMetadata(renderer, "_cachedDontClickMetadata");

            updatedClickMetadata.Should().Contain(metadata => metadata.Contains("Artisan", StringComparison.OrdinalIgnoreCase));
            updatedClickMetadata.Should().NotContain(metadata => metadata.Contains("Arcanist", StringComparison.OrdinalIgnoreCase));
            updatedDontClickMetadata.Should().Contain(metadata => metadata.Contains("Arcanist", StringComparison.OrdinalIgnoreCase));
            updatedDontClickMetadata.Should().NotContain(metadata => metadata.Contains("Artisan", StringComparison.OrdinalIgnoreCase));
        }

        private static bool InvokeHasMatchingSnapshot(HashSet<string>? currentIds, HashSet<string>? snapshot)
        {
            MethodInfo method = typeof(StrongboxRenderer).GetMethod("HasMatchingSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!;
            method.Should().NotBeNull();
            return (bool)method.Invoke(null, [currentIds, snapshot])!;
        }

        private static void InvokeEnsureStrongboxMetadataCache(StrongboxRenderer renderer)
        {
            MethodInfo method = typeof(StrongboxRenderer).GetMethod("EnsureStrongboxMetadataCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Should().NotBeNull();
            method.Invoke(renderer, null);
        }

        private static IReadOnlyList<string> GetCachedMetadata(StrongboxRenderer renderer, string fieldName)
            => (IReadOnlyList<string>)typeof(StrongboxRenderer)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(renderer)!;
    }
}