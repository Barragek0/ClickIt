namespace ClickIt.Tests.Features.Strongboxes
{
    [TestClass]
    public class StrongboxOverlayTests
    {
        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsTrue_WhenSpecialIdentifierExists()
        {
            bool contains = StrongboxOverlay.ContainsStrongboxUniqueIdentifier(
                ["metadata/a", "special:strongbox-unique", "metadata/b"]);

            contains.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsFalse_WhenMissingOrEmpty()
        {
            StrongboxOverlay.ContainsStrongboxUniqueIdentifier([]).Should().BeFalse();
            StrongboxOverlay.ContainsStrongboxUniqueIdentifier(["metadata/a", "metadata/b"]).Should().BeFalse();
            StrongboxOverlay.ContainsStrongboxUniqueIdentifier(null).Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_UniqueStrongbox_RespectsUniqueIdentifierLists()
        {
            string path = "Metadata/Chests/StrongBoxes/Arcanist";
            string name = "Arcanist's Strongbox";

            bool clickable = StrongboxOverlay.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "special:strongbox-unique" },
                new List<string>(),
                true);

            bool blocked = StrongboxOverlay.IsStrongboxClickableBySettings(
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

            bool clickable = StrongboxOverlay.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "StrongBoxes/Arcanist" },
                new List<string>(),
                false);

            bool blocked = StrongboxOverlay.IsStrongboxClickableBySettings(
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
            StrongboxOverlay.IsStrongboxClickableBySettings(
                string.Empty,
                "Arcanist's Strongbox",
                ["StrongBoxes/Arcanist"],
                [],
                false).Should().BeFalse();

            StrongboxOverlay.IsStrongboxClickableBySettings(
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
            var overlay = new StrongboxOverlay();

            InvokeEnsureStrongboxMetadataCache(overlay, settings);

            IReadOnlyList<string> initialClickMetadata = GetCachedMetadata(overlay, "_cachedClickMetadata");
            IReadOnlyList<string> initialDontClickMetadata = GetCachedMetadata(overlay, "_cachedDontClickMetadata");

            initialClickMetadata.Should().Contain(metadata => metadata.Contains("Arcanist", StringComparison.OrdinalIgnoreCase));
            initialDontClickMetadata.Should().Contain(metadata => metadata.Contains("Artisan", StringComparison.OrdinalIgnoreCase));

            settings.StrongboxClickIds = ["artisan"];
            settings.StrongboxDontClickIds = ["arcanist"];

            InvokeEnsureStrongboxMetadataCache(overlay, settings);

            IReadOnlyList<string> updatedClickMetadata = GetCachedMetadata(overlay, "_cachedClickMetadata");
            IReadOnlyList<string> updatedDontClickMetadata = GetCachedMetadata(overlay, "_cachedDontClickMetadata");

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
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            overlay.Refresh(CreateRefreshContext(settings, [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(50f, 60f, 100f, 40f))], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            queue.GetPendingFrameSnapshot().Should().ContainSingle();
        }

        [TestMethod]
        public void RenderFromLabels_SkipsStrongboxFullyOffScreen()
        {
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            overlay.Refresh(CreateRefreshContext(settings, [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(9000f, 9000f, 100f, 40f))], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void RenderFromLabels_SkipsOnScreenNonStrongboxLabel()
        {
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            overlay.Refresh(CreateRefreshContext(settings, [CreateStrongboxLabel("Metadata/Items/Currency/Orb", new RectangleF(50f, 60f, 100f, 40f))], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void Refresh_RescansLabels_OnEveryCall()
        {
            // Cadence is owned by the OverlayRenderHost coroutine; Refresh always rescans.
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            overlay.Refresh(CreateRefreshContext(settings, [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(50f, 60f, 100f, 40f))], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));
            queue.GetPendingFrameSnapshot().Should().ContainSingle();

            // A later refresh picks up the new label set immediately (no internal throttle).
            queue.ClearPending();
            overlay.Refresh(CreateRefreshContext(settings, [], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));
            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void Draw_BeforeFirstRefresh_DoesNotThrow()
        {
            // Regression: the snapshot must start empty (not null) so the first frame after plugin
            // load — before the refresh coroutine's first iteration — cannot NRE while iterating.
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            overlay.Draw(CreateDrawContext(settings, window, queue));

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        private static OverlayRefreshContext CreateRefreshContext(ClickItSettings settings, IReadOnlyList<LabelOnGround>? labels, RectangleF window)
            => new(GameController: null, Labels: labels, WindowArea: window, Settings: settings);

        private static OverlayRenderContext CreateDrawContext(ClickItSettings settings, RectangleF window, DeferredFrameQueue queue)
            => new(settings, GameController: null, Graphics: null, WindowArea: window, Labels: null, new DeferredTextQueue(), queue, new DeferredDrawQueue());

        private static LabelOnGround CreateStrongboxLabel(string path, RectangleF rect, string renderName = "Strongbox")
        {
            Entity item = EntityProbeFactory.Create(path: path, renderName: renderName);
            StrongboxProbeLabel label = (StrongboxProbeLabel)RuntimeHelpers.GetUninitializedObject(typeof(StrongboxProbeLabel));
            label.ItemOnGround = item;
            label.Label = new StrongboxProbeElement(rect);
            return label;
        }

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
            MethodInfo method = typeof(StrongboxOverlay).GetMethod("HasMatchingSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!;
            method.Should().NotBeNull();
            return (bool)method.Invoke(null, [currentIds, snapshot])!;
        }

        private static void InvokeEnsureStrongboxMetadataCache(StrongboxOverlay overlay, ClickItSettings settings)
        {
            MethodInfo method = typeof(StrongboxOverlay).GetMethod("EnsureStrongboxMetadataCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Should().NotBeNull();
            method.Invoke(overlay, [settings]);
        }

        private static IReadOnlyList<string> GetCachedMetadata(StrongboxOverlay overlay, string fieldName)
            => (IReadOnlyList<string>)typeof(StrongboxOverlay)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(overlay)!;
    }
}
