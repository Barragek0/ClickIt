namespace ClickIt.Tests.Behavior.Click
{
    // End-to-end execution scenarios: the REAL InteractionExecutionEngine with the REAL LabelClickPointResolver and clickable-area predicate. Verifies the click-vs-walk decision for on-screen strongboxes and items, including partially/fully obscured labels and locked boxes.
    [TestClass]
    public class ClickPipelineExecutionScenarioTests
    {
        private const string StrongboxPath = "Metadata/Chests/StrongBoxes/Arcanist";
        private const string WorldItemPath = "Metadata/MiscellaneousObjects/WorldItem";

        private static ClickPipelineScenarioFactory.ScenarioConfig BaseConfig()
            => new()
            {
                ClickDistance = 100,
                ClickItems = true,
                ClickStrongboxes = true,
            };

        private static LabelOnGround Strongbox(float distance, long address, bool locked, RectangleF rect)
            => ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance, address, locked),
                address + 0x1000);

        private static LabelOnGround WorldItem(float distance, long address, RectangleF rect)
            => ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateWorldItem(distance, address),
                address + 0x1000);

        private static DecisionResult GroundItemsDecision()
            => new(TrySettlers: false, TryLostShipment: false, TryShrine: false, GroundItemsVisible: true);

        private static DecisionResult HiddenGroundItemsDecision()
            => new(TrySettlers: false, TryLostShipment: false, TryShrine: false, GroundItemsVisible: false);

        [TestMethod]
        public void UnobscuredStrongboxOnScreen_IsClickedInPlace()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF rect = new(500f, 300f, 160f, 40f);
            LabelOnGround box = Strongbox(30f, 0x01, locked: false, rect);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: [box]),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue("a resolvable on-screen strongbox is clicked, not walked");
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(rect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void StrongboxPartiallyObscuredByBlockedRect_IsStillClicked_AtValidPoint()
        {
            RectangleF rect = new(500f, 300f, 200f, 40f);
            RectangleF leftBlock = new(480f, 290f, 120f, 60f);
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(
                new ClickPipelineScenarioFactory.ScenarioConfig { BlockedRects = [leftBlock] });
            LabelOnGround box = Strongbox(30f, 0x01, locked: false, rect);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: [box]),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue("a partially obscured strongbox must still be clicked");
            harness.InteractionsExecuted.Should().Be(1);
            Vector2 clickPos = harness.LastClickPosition!.Value;
            AssertPointInside(rect, clickPos);
            leftBlock.Contains(clickPos).Should().BeFalse("the click must land outside the blocked rectangle");
        }

        [TestMethod]
        public void StrongboxFullyObscured_IsNotClicked_WalksInstead()
        {
            RectangleF rect = new(500f, 300f, 200f, 40f);
            RectangleF fullBlock = new(490f, 290f, 220f, 60f);
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(
                new ClickPipelineScenarioFactory.ScenarioConfig { BlockedRects = [fullBlock] });
            LabelOnGround box = Strongbox(30f, 0x01, locked: false, rect);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: [box]),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeFalse("a fully obscured strongbox has no clickable point, so the tick walks instead");
            harness.InteractionsExecuted.Should().Be(0, "the walk decision stops the tick before any click is attempted");
        }

        [TestMethod]
        public void LockedStrongbox_IsNeverClicked_AndNothingElseActionable()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround locked = Strongbox(30f, 0x01, locked: true, new RectangleF(500f, 300f, 160f, 40f));
            var labels = new List<LabelOnGround> { locked };

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(locked, null),
                GroundItemsDecision());

            harness.InteractionsExecuted.Should().Be(0, "a locked strongbox must never be clicked");
            result.ShouldRunPostActions.Should().BeFalse();
        }

        [TestMethod]
        public void LockedStrongboxSkipped_OpenItemClicked()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF boxRect = new(500f, 300f, 160f, 40f);
            RectangleF itemRect = new(900f, 600f, 60f, 20f);
            LabelOnGround locked = Strongbox(30f, 0x01, locked: true, boxRect);
            LabelOnGround item = WorldItem(60f, 0x02, itemRect);
            var labels = new List<LabelOnGround> { locked, item };
            harness.CurrentLabels = labels;

            // The engine is handed the open item as the next label (as the selection scan would after skipping the locked box) and must click it.
            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(item, MechanicIds.Items),
                GroundItemsDecision());

            harness.InteractionsExecuted.Should().Be(1, "the open item must be clicked");
            result.ShouldRunPostActions.Should().BeTrue();
            AssertPointInside(itemRect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void UnobscuredWorldItem_IsClickedInPlace()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF rect = new(700f, 500f, 120f, 20f);
            LabelOnGround item = WorldItem(15f, 0x01, rect);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: [item]),
                harness.CreateCandidates(item, MechanicIds.Items),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue();
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(rect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void WorldItemFullyObscured_WalksInsteadOfClicking()
        {
            RectangleF rect = new(700f, 500f, 120f, 20f);
            RectangleF fullBlock = new(690f, 490f, 140f, 40f);
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(
                new ClickPipelineScenarioFactory.ScenarioConfig { BlockedRects = [fullBlock] });
            LabelOnGround item = WorldItem(15f, 0x01, rect);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: [item]),
                harness.CreateCandidates(item, MechanicIds.Items),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeFalse();
            harness.InteractionsExecuted.Should().Be(0);
        }

        [TestMethod]
        public void EndToEnd_ScanSelectsStrongbox_AndExecutionClicksIt()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF rect = new(500f, 300f, 160f, 40f);
            LabelOnGround box = Strongbox(30f, 0x01, locked: false, rect);
            var labels = new List<LabelOnGround> { box };
            harness.CurrentLabels = labels;

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(box);
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue();
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(rect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void EndToEnd_LockedStrongboxSkippedByScan_OpenItemClicked()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, new RectangleF(500f, 300f, 160f, 40f));
            LabelOnGround item = WorldItem(40f, 0x02, new RectangleF(900f, 600f, 60f, 20f));
            var labels = new List<LabelOnGround> { locked, item };
            harness.CurrentLabels = labels;

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(item, "the scan must skip the locked strongbox and return the open item");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue();
            harness.InteractionsExecuted.Should().Be(1);
        }

        [TestMethod]
        public void EndToEnd_MultipleItemsAtDifferentPositions_ScanPicksClosest_AndClicksIt()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF nearRect = new(200f, 200f, 60f, 20f);
            RectangleF midRect = new(800f, 600f, 60f, 20f);
            RectangleF farRect = new(1500f, 800f, 60f, 20f);
            LabelOnGround near = WorldItem(10f, 0x01, nearRect);
            LabelOnGround mid = WorldItem(45f, 0x02, midRect);
            LabelOnGround far = WorldItem(90f, 0x03, farRect);
            var labels = new List<LabelOnGround> { far, mid, near };

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(near, "the closest item must be selected first");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue();
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(nearRect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void EndToEnd_TwoOpenStrongboxes_ScanPicksCloser_AndClicksIt()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF nearRect = new(500f, 300f, 160f, 40f);
            RectangleF farRect = new(1500f, 800f, 160f, 40f);
            LabelOnGround near = Strongbox(30f, 0x01, locked: false, nearRect);
            LabelOnGround far = Strongbox(70f, 0x02, locked: false, farRect);
            var labels = new List<LabelOnGround> { far, near };

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(near, "with penalty 0 the closer strongbox must be selected first");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue();
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(nearRect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void EndToEnd_ItemPlusLockedStrongbox_ScanPicksItem_AndClicksIt()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround locked = Strongbox(5f, 0x01, locked: true, new RectangleF(500f, 300f, 160f, 40f));
            LabelOnGround item = WorldItem(50f, 0x02, new RectangleF(900f, 600f, 60f, 20f));
            var labels = new List<LabelOnGround> { locked, item };

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(item, "the closest locked strongbox is skipped and the open item selected");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue();
            harness.InteractionsExecuted.Should().Be(1);
        }

        [TestMethod]
        public void FullyObscuredLabel_WalkDisabled_NeitherClickedNorWalked()
        {
            RectangleF rect = new(500f, 300f, 200f, 40f);
            RectangleF fullBlock = new(490f, 290f, 220f, 60f);
            var config = new ClickPipelineScenarioFactory.ScenarioConfig
            {
                BlockedRects = [fullBlock],
                WalkTowardOffscreenLabels = false,
            };
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround box = Strongbox(30f, 0x01, locked: false, rect);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: [box]),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeFalse();
            harness.InteractionsExecuted.Should().Be(0);
        }

        [TestMethod]
        public void WorldItemPartiallyObscured_IsClickedAtValidPoint()
        {
            RectangleF rect = new(700f, 500f, 120f, 20f);
            RectangleF blocker = new(760f, 495f, 80f, 30f);
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(
                new ClickPipelineScenarioFactory.ScenarioConfig { BlockedRects = [blocker] });
            LabelOnGround item = WorldItem(15f, 0x01, rect);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: [item]),
                harness.CreateCandidates(item, MechanicIds.Items),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue();
            harness.InteractionsExecuted.Should().Be(1);
            Vector2 clickPos = harness.LastClickPosition!.Value;
            AssertPointInside(rect, clickPos);
            blocker.Contains(clickPos).Should().BeFalse();
        }

        [TestMethod]
        public void PostChestLootSettle_BlocksClick_NoInteraction()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF rect = new(500f, 300f, 160f, 40f);
            LabelOnGround box = Strongbox(30f, 0x01, locked: false, rect);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreatePostChestSettleContext([box]),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeFalse("clicks are paused while chest loot settles");
            harness.InteractionsExecuted.Should().Be(0);
        }

        [TestMethod]
        public void PostChestLootSettle_BlocksWalkToFarLabel_NoOffscreenPathfinding()
        {
            // During the post-chest-loot-settle wait, a label beyond ClickDistance (which would normally be walked to per Spec 11) must NOT be walked toward - the tick waits for drops to settle instead of pathfinding off-screen.
            var config = BaseConfig();
            config.CaptureClickDebug = true;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            RectangleF rect = new(500f, 300f, 160f, 40f);
            LabelOnGround box = Strongbox(150f, 0x01, locked: false, rect); // beyond ClickDistance 100 -> would normally be walked to
            var labels = new List<LabelOnGround> { box };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreatePostChestSettleContext(labels),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeFalse("the tick is consumed while chest drops settle");
            harness.InteractionsExecuted.Should().Be(0, "no click is dispatched while drops settle");
            harness.ClickDebugSnapshots.Should().Contain(s => s.Stage == "PostChestLootSettleBlocked",
                "the settle block must be enforced on the label walk decision");
            harness.ClickDebugSnapshots.Should().NotContain(s => s.Stage == "WalkTowardLabel",
                "the far label must not be pathfound toward while drops settle");
        }

        [TestMethod]
        public void LabelBeyondClickDistance_IsWalkedTo_NotClicked()
        {
            // Spec 11: a label beyond ClickDistance is walked to even when its click point resolves. The scan normally filters far labels at eligibility, but a hover-preference or hidden path can surface one here - it must never be clicked in place.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF rect = new(500f, 300f, 160f, 40f);
            LabelOnGround box = Strongbox(150f, 0x01, locked: false, rect); // beyond ClickDistance 100
            var labels = new List<LabelOnGround> { box };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeFalse("a label beyond ClickDistance is walked to, never clicked");
            harness.InteractionsExecuted.Should().Be(0);
        }

        [TestMethod]
        public void HiddenPath_LabelPresent_NeverClickedBlind()
        {
            // Spec 6: hidden mode never clicks a label blind - it only walks toward the nearest target; the visible path handles clicks once labels are visible again.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF rect = new(500f, 300f, 160f, 40f);
            LabelOnGround box = Strongbox(30f, 0x01, locked: false, rect);
            var labels = new List<LabelOnGround> { box };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: false, allLabels: labels),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                HiddenGroundItemsDecision());

            result.ShouldRunPostActions.Should().BeFalse("hidden mode never clicks - the visible path handles clicks");
            harness.InteractionsExecuted.Should().Be(0);
        }

        [TestMethod]
        public void HiddenPath_NoLabel_NoClickNoWalkFallback()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            harness.CurrentLabels = [];

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: false, allLabels: []),
                harness.CreateCandidates(null, null),
                HiddenGroundItemsDecision());

            result.ShouldRunPostActions.Should().BeFalse();
            harness.InteractionsExecuted.Should().Be(0);
        }

        [TestMethod]
        public void EndToEnd_LockedStrongboxOnScreen_ClosestOpenStrongboxClicked_NotWalked()
        {
            // The exact reported scenario: two strongboxes partially overlapping on screen, one locked, items spread around. With penalty 0 the closest clickable label must be clicked - the plugin must NOT walk to a farther strongbox while an open one is on screen in a clickable position.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF lockedRect = new(500f, 300f, 160f, 40f);
            RectangleF openRect = new(580f, 310f, 160f, 40f); // partially overlaps lockedRect
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, lockedRect);
            LabelOnGround open = Strongbox(30f, 0x02, locked: false, openRect);
            LabelOnGround itemA = WorldItem(50f, 0x03, new RectangleF(200f, 200f, 60f, 20f));
            LabelOnGround itemB = WorldItem(70f, 0x04, new RectangleF(1500f, 800f, 60f, 20f));
            var labels = new List<LabelOnGround> { locked, open, itemA, itemB };
            harness.CurrentLabels = labels;

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(open, "the scan must skip the locked strongbox and pick the closest clickable label (the open strongbox)");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue("the closest open strongbox must be clicked, not walked to");
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(openRect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void EndToEnd_CursorOverLockedStrongbox_OpenStrongboxClicked_NotWalked()
        {
            // The exact reported failure: two strongboxes partially overlapping on screen, cursor over the LOCKED one. The UI-hover strongbox preference must not re-target the locked box - the open strongbox must be clicked in place, never walked to.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF lockedRect = new(500f, 300f, 160f, 40f);
            RectangleF openRect = new(580f, 310f, 160f, 40f); // partially overlaps lockedRect
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, lockedRect);
            LabelOnGround open = Strongbox(30f, 0x02, locked: false, openRect);
            var labels = new List<LabelOnGround> { locked, open };
            harness.CurrentLabels = labels;

            var lockedElement = (ClickPipelineScenarioFactory.ScenarioLabelElement)ClickPipelineScenarioFactory.GetLabelElement(locked);
            ClickPipelineScenarioFactory.SetElementAddress(lockedElement, 0xB000);
            harness.HoveredElement = lockedElement;

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(open, "the hover preference must not switch to the locked strongbox");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue("the open strongbox must be clicked, not walked to");
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(openRect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void EndToEnd_CursorOverOpenStrongbox_OpenStrongboxStillClicked()
        {
            // Cursor over an OPEN strongbox that overlaps another open one: the UI-hover preference selects it and it is clicked - the stacked-label behavior is preserved end to end.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF aRect = new(500f, 300f, 160f, 40f);
            RectangleF bRect = new(580f, 310f, 160f, 40f); // partially overlaps aRect
            LabelOnGround a = Strongbox(10f, 0x01, locked: false, aRect);
            LabelOnGround b = Strongbox(30f, 0x02, locked: false, bRect);
            var labels = new List<LabelOnGround> { a, b };
            harness.CurrentLabels = labels;

            var bElement = (ClickPipelineScenarioFactory.ScenarioLabelElement)ClickPipelineScenarioFactory.GetLabelElement(b);
            ClickPipelineScenarioFactory.SetElementAddress(bElement, 0xB001);
            harness.HoveredElement = bElement;

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(b, "hovering an open overlapping strongbox prefers it");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue("the hovered open strongbox must be clicked");
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(bRect, harness.LastClickPosition!.Value);
        }

        [TestMethod]
        public void EndToEnd_LockedStrongboxOnScreen_ClosestItemClicked_WhenCloserThanOpenStrongbox()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, new RectangleF(500f, 300f, 160f, 40f));
            LabelOnGround nearItem = WorldItem(25f, 0x02, new RectangleF(900f, 600f, 60f, 20f));
            LabelOnGround open = Strongbox(40f, 0x03, locked: false, new RectangleF(580f, 310f, 160f, 40f));
            var labels = new List<LabelOnGround> { locked, open, nearItem };
            harness.CurrentLabels = labels;

            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(nearItem, "with penalty 0 the closest clickable label (the item) wins over the farther open strongbox");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue();
            harness.InteractionsExecuted.Should().Be(1);
        }

        [TestMethod]
        public void EndToEnd_AfterStrongboxLocks_NextRunClicksNextClosest_InsteadOfWalkingToFarBox()
        {
            // The reported sequence: strongbox A is open and closest -> selected and clicked. A opens and becomes locked, but the label-list reference is STABLE (the lock state does not change the label set), so the selection caches still hold A. The very next run must click the next closest clickable label (B) - it must NOT walk to the far box C (beyond click distance) while B is on screen and clickable.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            var boxA = (EntityProbe)ClickPipelineScenarioFactory.CreateStrongbox(
                "Metadata/Chests/StrongBoxes/Arcanist", 10f, 0x01, locked: false);
            RectangleF aRect = new(500f, 300f, 160f, 40f);
            RectangleF bRect = new(680f, 320f, 160f, 40f);
            RectangleF cRect = new(1500f, 800f, 160f, 40f);
            LabelOnGround a = ClickPipelineScenarioFactory.CreateLabel(aRect, boxA, 0x1001);
            LabelOnGround b = Strongbox(35f, 0x02, locked: false, bRect);
            LabelOnGround c = Strongbox(150f, 0x03, locked: false, cRect);
            var labels = new List<LabelOnGround> { a, b, c };
            harness.CurrentLabels = labels;

            // First run: A is open and closest -> selected and clicked.
            harness.ScanEngine.ResolveNextLabelCandidate(labels).Should().BeSameAs(a);

            // A opens and becomes locked; the label set (and its reference) is unchanged.
            EntityProbeFactory.WithComponent<Chest>(boxA, new LockedChestProbe { IsLocked = true });

            // Next run: the scan must NOT return A, and must pick the next closest clickable (B), never leaving B behind to walk to the far box C.
            LabelOnGround? next = harness.ScanEngine.ResolveNextLabelCandidate(labels);
            next.Should().BeSameAs(b, "after the closest strongbox locks, the next closest clickable must be selected");
            string? mechanic = harness.LabelInteractionPort.GetMechanicIdForLabel(next);

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(next, mechanic),
                GroundItemsDecision());

            result.ShouldRunPostActions.Should().BeTrue("the next closest open strongbox must be clicked, not walked to");
            harness.InteractionsExecuted.Should().Be(1);
            AssertPointInside(bRect, harness.LastClickPosition!.Value);
        }

        private static void AssertPointInside(RectangleF rect, Vector2 point)
        {
            point.X.Should().BeGreaterThanOrEqualTo(rect.Left);
            point.X.Should().BeLessThanOrEqualTo(rect.Right);
            point.Y.Should().BeGreaterThanOrEqualTo(rect.Top);
            point.Y.Should().BeLessThanOrEqualTo(rect.Bottom);
        }
    }
}
