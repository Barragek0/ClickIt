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

        [TestMethod]
        public void RenderFromLabels_EnqueuesFrame_ForOnScreenClickableStrongbox()
        {
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var renderer = new StrongboxRenderer(settings, queue);
            RectangleF window = new(100f, 100f, 1280f, 720f);

            renderer.RenderFromLabels(
                [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(50f, 60f, 100f, 40f))],
                window);

            queue.GetPendingFrameSnapshot().Should().ContainSingle();
        }

        [TestMethod]
        public void RenderFromLabels_SkipsStrongboxFullyOffScreen()
        {
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var renderer = new StrongboxRenderer(settings, queue);
            RectangleF window = new(100f, 100f, 1280f, 720f);

            renderer.RenderFromLabels(
                [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(9000f, 9000f, 100f, 40f))],
                window);

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void RenderFromLabels_SkipsOnScreenNonStrongboxLabel()
        {
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var renderer = new StrongboxRenderer(settings, queue);
            RectangleF window = new(100f, 100f, 1280f, 720f);

            renderer.RenderFromLabels(
                [CreateStrongboxLabel("Metadata/Items/Currency/Orb", new RectangleF(50f, 60f, 100f, 40f))],
                window);

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void RenderFromLabels_ThrottlesRescan_UntilScanIntervalElapses()
        {
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var renderer = new StrongboxRenderer(settings, new DeferredFrameQueue());
            RectangleF window = new(100f, 100f, 1280f, 720f);

            renderer.RenderFromLabels(
                [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(50f, 60f, 100f, 40f))],
                window);
            ReadCachedStrongboxCount(renderer).Should().Be(1);

            // Within the scan window the changed label set is not re-scanned.
            renderer.RenderFromLabels([], window);
            ReadCachedStrongboxCount(renderer).Should().Be(1);

            // Force the window to elapse → the re-scan sees the empty set.
            ForceStrongboxRescan(renderer);
            renderer.RenderFromLabels([], window);
            ReadCachedStrongboxCount(renderer).Should().Be(0);
        }

        private static LabelOnGround CreateStrongboxLabel(string path, RectangleF rect, string renderName = "Strongbox")
        {
            Entity item = EntityProbeFactory.Create(path: path, renderName: renderName);
            StrongboxProbeLabel label = (StrongboxProbeLabel)RuntimeHelpers.GetUninitializedObject(typeof(StrongboxProbeLabel));
            label.ItemOnGround = item;
            label.Label = new StrongboxProbeElement(rect);
            return label;
        }

        private static int ReadCachedStrongboxCount(StrongboxRenderer renderer)
        {
            object value = typeof(StrongboxRenderer)
                .GetField("_cachedStrongboxes", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(renderer)!;
            return ((System.Collections.ICollection)value).Count;
        }

        private static void ForceStrongboxRescan(StrongboxRenderer renderer)
            => typeof(StrongboxRenderer)
                .GetField("_lastStrongboxScanMs", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(renderer, 0L);

        public sealed class StrongboxProbeLabel : LabelOnGround
        {
            public new Entity? ItemOnGround { get; set; }

            public new Element? Label { get; set; }
        }

        public sealed class StrongboxProbeElement(RectangleF clientRect) : Element
        {
            public new bool IsValid { get; set; } = true;

            public override RectangleF GetClientRect() => clientRect;
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