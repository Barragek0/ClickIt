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
        public void Refresh_RescansLabels_WhenLabelSetChanges()
        {
            // Refresh runs every frame; the expensive scan re-runs only when the label snapshot or render state changes (the label-ref guard short-circuits unchanged calls).
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            overlay.Refresh(CreateRefreshContext(settings, [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(50f, 60f, 100f, 40f))], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));
            queue.GetPendingFrameSnapshot().Should().ContainSingle();

            // A later refresh with a new label set picks it up immediately.
            queue.ClearPending();
            overlay.Refresh(CreateRefreshContext(settings, [], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));
            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void Refresh_DoesNotRescan_WhenLabelSnapshotUnchanged()
        {
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            IReadOnlyList<LabelOnGround> labels = [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(50f, 60f, 100f, 40f))];
            overlay.Refresh(CreateRefreshContext(settings, labels, window));

            GetPrivateField<IReadOnlyList<LabelOnGround>>(overlay, "_lastScannedLabels").Should().BeSameAs(labels);

            // Same snapshot instance again — the guard short-circuits (the cached reference stays).
            overlay.Refresh(CreateRefreshContext(settings, labels, window));
            GetPrivateField<IReadOnlyList<LabelOnGround>>(overlay, "_lastScannedLabels").Should().BeSameAs(labels);
        }

        [TestMethod]
        public void Refresh_Rescans_WhenRenderStateChanges()
        {
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            IReadOnlyList<LabelOnGround> first = [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(50f, 60f, 100f, 40f))];
            overlay.Refresh(CreateRefreshContext(settings, first, window));

            // Changing the click filter replaces the cached metadata -> different render state.
            settings.StrongboxClickIds = ["artisan"];
            IReadOnlyList<LabelOnGround> second = [CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Artisan", new RectangleF(50f, 60f, 100f, 40f))];
            overlay.Refresh(CreateRefreshContext(settings, second, window));

            GetPrivateField<IReadOnlyList<LabelOnGround>>(overlay, "_lastScannedLabels").Should().BeSameAs(second);
        }

        [TestMethod]
        public void Draw_GreenUnopenedBox_HugsChild0Frame()
        {
            // Unopened (green) boxes hug the label's Child[0] text frame — the base label element is bigger before the strongbox opens, so the box must be drawn around the child.
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            StrongboxProbeElement label = new(new RectangleF(50f, 60f, 100f, 40f))
            {
                Child0 = new StrongboxProbeElement(new RectangleF(55f, 65f, 30f, 20f)),
            };
            LabelOnGround sb = CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", label.GetClientRect(), labelElement: label);

            overlay.Refresh(CreateRefreshContext(settings, [sb], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            var frames = queue.GetPendingFrameSnapshot();
            frames.Should().ContainSingle();
            frames[0].Rectangle.X.Should().Be(55f);
            frames[0].Rectangle.Y.Should().Be(65f);
            frames[0].Rectangle.Width.Should().Be(30f);
        }

        [TestMethod]
        public void Draw_GreenUnopenedBox_FallsBackToLabelRect_WhenNoChild()
        {
            // Unopened (green) with no readable child frame falls back to the label element rect.
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            LabelOnGround sb = CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(50f, 60f, 100f, 40f));

            overlay.Refresh(CreateRefreshContext(settings, [sb], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            var frames = queue.GetPendingFrameSnapshot();
            frames.Should().ContainSingle();
            frames[0].Rectangle.X.Should().Be(50f);
            frames[0].Rectangle.Y.Should().Be(60f);
            frames[0].Rectangle.Width.Should().Be(100f);
        }

        [TestMethod]
        public void Draw_RedOpenedBox_UsesLabelElementRect()
        {
            // Regression: opened (red) boxes must use the label element rect — the same geometry the click pipeline uses — not the Child[0] frame, which is rebuilt and sits at a wrong offset once the strongbox opens (caused red boxes in incorrect places).
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            StrongboxProbeElement label = new(new RectangleF(50f, 60f, 100f, 40f))
            {
                Child0 = new StrongboxProbeElement(new RectangleF(55f, 65f, 30f, 20f)),
            };
            Chest chest = (StrongboxChestProbe)RuntimeHelpers.GetUninitializedObject(typeof(StrongboxChestProbe));
            ((StrongboxChestProbe)chest).IsLocked = true;
            LabelOnGround sb = CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", label.GetClientRect(), labelElement: label, chest: chest);

            overlay.Refresh(CreateRefreshContext(settings, [sb], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            var frames = queue.GetPendingFrameSnapshot();
            frames.Should().ContainSingle();
            frames[0].Rectangle.X.Should().Be(50f);
            frames[0].Rectangle.Y.Should().Be(60f);
            frames[0].Rectangle.Width.Should().Be(100f);
        }

        [TestMethod]
        public void Draw_RendersStrongbox_ThatWasOffScreenAtScanTime()
        {
            // Regression: the scan must cache strongboxes by label identity, not on-screen state. A strongbox that was off-screen when the snapshot was built renders the moment any part of its label is on screen at draw time — no rescan required.
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            // Off-screen at refresh time (label rect far outside the window)…
            StrongboxProbeLabel probe = (StrongboxProbeLabel)CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(9000f, 9000f, 100f, 40f));
            IReadOnlyList<LabelOnGround> labels = [probe];
            overlay.Refresh(CreateRefreshContext(settings, labels, window));

            // …but the label element is now on screen at draw time: the frame must render without another refresh (the cached entry projects the live label rect per frame).
            probe.Label = new StrongboxProbeElement(new RectangleF(50f, 60f, 100f, 40f));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            var frames = queue.GetPendingFrameSnapshot();
            frames.Should().ContainSingle();
            frames[0].Rectangle.X.Should().Be(50f);
            frames[0].Rectangle.Y.Should().Be(60f);
            frames[0].Rectangle.Width.Should().Be(100f);
        }

        [TestMethod]
        public void Draw_GreenBox_FallsBackToLabelRect_WhenChildIsNearOrigin()
        {
            // Regression: a label entering/leaving the screen edge reports its Child[0] local rect near the window's top-left corner while the parent label is elsewhere. The box must not flash at the corner; it falls back to the positioned parent label rect.
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            StrongboxProbeElement label = new(new RectangleF(50f, 60f, 100f, 40f))
            {
                Child0 = new StrongboxProbeElement(new RectangleF(5f, 5f, 30f, 20f)),
            };
            LabelOnGround sb = CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", label.GetClientRect(), labelElement: label);

            overlay.Refresh(CreateRefreshContext(settings, [sb], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            var frames = queue.GetPendingFrameSnapshot();
            frames.Should().ContainSingle();
            frames[0].Rectangle.X.Should().Be(50f);
            frames[0].Rectangle.Y.Should().Be(60f);
        }

        [TestMethod]
        public void Draw_GreenBox_FallsBackToLabelRect_WhenChildIsOutsideLabel()
        {
            // Regression: a mid-layout child can report a rect far from its parent label (not near the origin). The box must not render at the wrong position; it falls back to the label.
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            StrongboxProbeElement label = new(new RectangleF(50f, 60f, 100f, 40f))
            {
                Child0 = new StrongboxProbeElement(new RectangleF(400f, 300f, 30f, 20f)),
            };
            LabelOnGround sb = CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", label.GetClientRect(), labelElement: label);

            overlay.Refresh(CreateRefreshContext(settings, [sb], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            var frames = queue.GetPendingFrameSnapshot();
            frames.Should().ContainSingle();
            frames[0].Rectangle.X.Should().Be(50f);
            frames[0].Rectangle.Y.Should().Be(60f);
        }

        [TestMethod]
        public void Draw_SkipsStrongbox_WhenLabelRectIsAtOrigin()
        {
            // A rebuilt label element (strongbox opening) briefly reports an origin rect; no frame is drawn rather than flashing at the window's top-left.
            var settings = new ClickItSettings { StrongboxClickIds = ["arcanist"] };
            var queue = new DeferredFrameQueue();
            var overlay = new StrongboxOverlay();
            RectangleF window = new(100f, 100f, 1280f, 720f);

            LabelOnGround sb = CreateStrongboxLabel("Metadata/Chests/StrongBoxes/Arcanist", new RectangleF(0f, 0f, 100f, 40f));

            overlay.Refresh(CreateRefreshContext(settings, [sb], window));
            overlay.Draw(CreateDrawContext(settings, window, queue));

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void Draw_BeforeFirstRefresh_DoesNotThrow()
        {
            // Regression: the snapshot must start empty (not null) so the first frame after plugin load — before the refresh coroutine's first iteration — cannot NRE while iterating.
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

        private static LabelOnGround CreateStrongboxLabel(string path, RectangleF rect, string renderName = "Strongbox", StrongboxProbeElement? labelElement = null, Chest? chest = null)
        {
            Entity item = EntityProbeFactory.Create(path: path, renderName: renderName);
            if (chest != null)
                EntityProbeFactory.WithComponent<Chest>(item, chest);
            StrongboxProbeLabel label = (StrongboxProbeLabel)RuntimeHelpers.GetUninitializedObject(typeof(StrongboxProbeLabel));
            label.ItemOnGround = item;
            label.Label = labelElement ?? new StrongboxProbeElement(rect);
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

            public Element? Child0 { get; set; }

            public override RectangleF GetClientRect() => clientRect;

            public new object? GetChildAtIndex(int index) => index == 0 ? Child0 : null;
        }

        // Chest-derived probe so DynamicAccess.GetComponent<Chest>() resolves it; the `new` IsLocked hides the base memory-read getter (read through DynamicAccess, like the classifier).
        public sealed class StrongboxChestProbe : Chest
        {
            public new bool IsLocked { get; set; }
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

        private static T GetPrivateField<T>(StrongboxOverlay overlay, string fieldName)
            => (T)typeof(StrongboxOverlay)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(overlay)!;
    }
}
