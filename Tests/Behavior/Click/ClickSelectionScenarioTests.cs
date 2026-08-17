namespace ClickIt.Tests.Behavior.Click
{
    // Scenario tests for the REAL label selection pipeline (LabelSelectionService + LabelEligibilityEngine + LabelSelectionScanEngine): multiple strongboxes and items at different positions/distances, locked boxes, priority ranking, and scan-level suppression.
    [TestClass]
    public class ClickSelectionScenarioTests
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

        [TestMethod]
        public void SingleWorldItem_IsSelected_WithItemsMechanic()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround item = WorldItem(10f, 0x01, new RectangleF(100f, 100f, 60f, 20f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([item], 0, 1, isAcceptable: null);

            selected.Should().BeSameAs(item);
        }

        [TestMethod]
        public void CloserWorldItem_Selected_WhenTwoItemsAtDifferentDistances()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround near = WorldItem(10f, 0x01, new RectangleF(100f, 100f, 60f, 20f));
            LabelOnGround far = WorldItem(90f, 0x02, new RectangleF(800f, 600f, 60f, 20f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([far, near], 0, 2, isAcceptable: null);

            selected.Should().BeSameAs(near, "with penalty 0 the closest label must win regardless of list order");
        }

        [TestMethod]
        public void ClosestLabel_Selected_RegardlessOfType_WhenPenaltyZero()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround nearItem = WorldItem(10f, 0x01, new RectangleF(100f, 100f, 60f, 20f));
            LabelOnGround farStrongbox = Strongbox(80f, 0x02, locked: false, new RectangleF(900f, 600f, 160f, 40f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([farStrongbox, nearItem], 0, 2, isAcceptable: null);

            selected.Should().BeSameAs(nearItem, "penalty 0 ranks purely by distance, so the closer item beats the farther strongbox");
        }

        [TestMethod]
        public void Strongbox_Selected_OverCloserItem_WhenPriorityPenaltyHigh()
        {
            ClickPipelineScenarioFactory.ScenarioConfig config = BaseConfig();
            config.MechanicPriorityDistancePenalty = 100;
            config.MechanicPriorityIndexMap[MechanicIds.Strongboxes] = 0;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround nearItem = WorldItem(10f, 0x01, new RectangleF(100f, 100f, 60f, 20f));
            LabelOnGround farStrongbox = Strongbox(80f, 0x02, locked: false, new RectangleF(900f, 600f, 160f, 40f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([nearItem, farStrongbox], 0, 2, isAcceptable: null);

            selected.Should().BeSameAs(farStrongbox,
                "a high-priority strongbox must win over a closer item when the priority penalty is enabled");
        }

        [TestMethod]
        public void LockedStrongbox_NotSelected_OpenItemSelected()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, new RectangleF(100f, 100f, 160f, 40f));
            LabelOnGround openItem = WorldItem(50f, 0x02, new RectangleF(800f, 600f, 60f, 20f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([locked, openItem], 0, 2, isAcceptable: null);

            selected.Should().BeSameAs(openItem, "a locked strongbox is never a candidate, so the next label is selected");
        }

        [TestMethod]
        public void AllLockedStrongboxes_Skipped_OpenStrongboxSelected()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround lockedA = Strongbox(10f, 0x01, locked: true, new RectangleF(100f, 100f, 160f, 40f));
            LabelOnGround lockedB = Strongbox(20f, 0x02, locked: true, new RectangleF(400f, 200f, 160f, 40f));
            LabelOnGround open = Strongbox(60f, 0x03, locked: false, new RectangleF(900f, 600f, 160f, 40f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([lockedA, lockedB, open], 0, 3, isAcceptable: null);

            selected.Should().BeSameAs(open, "all locked strongboxes are excluded; the open one is selected");
        }

        [TestMethod]
        public void OutOfDistanceLabel_Rejected_CloserLabelSelected()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround outOfRange = Strongbox(150f, 0x01, locked: false, new RectangleF(100f, 100f, 160f, 40f));
            LabelOnGround inRange = WorldItem(30f, 0x02, new RectangleF(800f, 600f, 60f, 20f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([outOfRange, inRange], 0, 2, isAcceptable: null);

            selected.Should().BeSameAs(inRange, "a strongbox beyond ClickDistance must be rejected, the in-range label selected");
        }

        [TestMethod]
        public void AllLabelsOutOfDistance_SelectsNothing()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround farA = Strongbox(150f, 0x01, locked: false, new RectangleF(100f, 100f, 160f, 40f));
            LabelOnGround farB = WorldItem(200f, 0x02, new RectangleF(800f, 600f, 60f, 20f));

            harness.SelectionService.GetNextLabelToClick([farA, farB], 0, 2, isAcceptable: null).Should().BeNull();
        }

        [TestMethod]
        public void ItemsDisabled_WorldItemsRejected_StrongboxSelected()
        {
            ClickPipelineScenarioFactory.ScenarioConfig config = BaseConfig();
            config.ClickItems = false;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround item = WorldItem(10f, 0x01, new RectangleF(100f, 100f, 60f, 20f));
            LabelOnGround strongbox = Strongbox(50f, 0x02, locked: false, new RectangleF(800f, 600f, 160f, 40f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([item, strongbox], 0, 2, isAcceptable: null);

            selected.Should().BeSameAs(strongbox, "with item pickup disabled the world item has no mechanic and is rejected");
        }

        [TestMethod]
        public void StrongboxesDisabled_StrongboxRejected_ItemSelected()
        {
            ClickPipelineScenarioFactory.ScenarioConfig config = BaseConfig();
            config.ClickStrongboxes = false;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround strongbox = Strongbox(10f, 0x01, locked: false, new RectangleF(100f, 100f, 160f, 40f));
            LabelOnGround item = WorldItem(50f, 0x02, new RectangleF(800f, 600f, 60f, 20f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([strongbox, item], 0, 2, isAcceptable: null);

            selected.Should().BeSameAs(item, "with strongbox clicking disabled the strongbox is rejected");
        }

        [TestMethod]
        public void Scan_FullyOverlappedLabel_Skipped_NextLabelReturned()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            // inner is fully inside outer's rect; inner is closer so the scan would rank it first, but it is fully overlapped and must be skipped in favor of outer.
            RectangleF outerRect = new(100f, 100f, 200f, 60f);
            RectangleF innerRect = new(140f, 120f, 60f, 20f);
            LabelOnGround inner = WorldItem(10f, 0x01, innerRect);
            LabelOnGround outer = WorldItem(20f, 0x02, outerRect);

            LabelOnGround? selected = harness.ScanEngine.ResolveNextLabelCandidate([inner, outer]);

            selected.Should().BeSameAs(outer, "a fully overlapped label must be skipped by the scan");
        }

        [TestMethod]
        public void Scan_LockedStrongbox_Skipped_NextLabelReturned()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, new RectangleF(100f, 100f, 160f, 40f));
            LabelOnGround open = WorldItem(30f, 0x02, new RectangleF(800f, 600f, 60f, 20f));

            LabelOnGround? selected = harness.ScanEngine.ResolveNextLabelCandidate([locked, open]);

            selected.Should().BeSameAs(open, "the scan must advance past a locked strongbox to the next label");
        }

        [TestMethod]
        public void Scan_AllLockedStrongboxes_ReturnsNull()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround lockedA = Strongbox(10f, 0x01, locked: true, new RectangleF(100f, 100f, 160f, 40f));
            LabelOnGround lockedB = Strongbox(20f, 0x02, locked: true, new RectangleF(400f, 200f, 160f, 40f));

            harness.ScanEngine.ResolveNextLabelCandidate([lockedA, lockedB]).Should().BeNull();
        }

        [TestMethod]
        public void Scan_MixedField_SelectsClosestNonLockedNonOverlapped()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF bigRect = new(100f, 100f, 300f, 60f);
            RectangleF tinyRect = new(240f, 130f, 40f, 10f); // fully inside bigRect
            LabelOnGround overlapped = WorldItem(5f, 0x01, tinyRect);
            LabelOnGround locked = Strongbox(15f, 0x02, locked: true, new RectangleF(500f, 300f, 160f, 40f));
            LabelOnGround openBox = Strongbox(40f, 0x03, locked: false, new RectangleF(900f, 600f, 160f, 40f));
            LabelOnGround bigItem = WorldItem(25f, 0x04, bigRect);

            LabelOnGround? selected = harness.ScanEngine.ResolveNextLabelCandidate([overlapped, locked, bigItem, openBox]);

            selected.Should().BeSameAs(bigItem,
                "overlapped and locked labels are skipped; among the remaining the closest (bigItem at 25) wins over openBox at 40");
        }

        [TestMethod]
        public void Scan_CursorOverLockedStrongbox_HoverPreferenceDoesNotSwitchToIt()
        {
            // The reported in-game bug: the cursor is over a LOCKED strongbox that partially overlaps an open strongbox. The UI-hover strongbox preference must NOT switch the scan target to the locked box (the click path would reject it and walk instead) - the ranked open strongbox must be selected.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF lockedRect = new(500f, 300f, 160f, 40f);
            RectangleF openRect = new(580f, 310f, 160f, 40f); // partially overlaps lockedRect
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, lockedRect);
            LabelOnGround open = Strongbox(30f, 0x02, locked: false, openRect);
            var labels = new List<LabelOnGround> { locked, open };

            var lockedElement = (ClickPipelineScenarioFactory.ScenarioLabelElement)ClickPipelineScenarioFactory.GetLabelElement(locked);
            ClickPipelineScenarioFactory.SetElementAddress(lockedElement, 0xA000);
            harness.HoveredElement = lockedElement;

            LabelOnGround? selected = harness.ScanEngine.ResolveNextLabelCandidate(labels);

            selected.Should().BeSameAs(open,
                "the hover preference must not re-target a locked strongbox; the ranked open strongbox is selected");
        }

        [TestMethod]
        public void Scan_CursorOverOpenStrongbox_HoverPreferenceSelectsIt()
        {
            // With the cursor over an OPEN strongbox that overlaps another open strongbox, the UI-hover preference still selects the hovered one - the intended stacked-label behavior is preserved.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF aRect = new(500f, 300f, 160f, 40f);
            RectangleF bRect = new(580f, 310f, 160f, 40f); // partially overlaps aRect
            LabelOnGround a = Strongbox(10f, 0x01, locked: false, aRect);
            LabelOnGround b = Strongbox(30f, 0x02, locked: false, bRect);
            var labels = new List<LabelOnGround> { a, b };

            var bElement = (ClickPipelineScenarioFactory.ScenarioLabelElement)ClickPipelineScenarioFactory.GetLabelElement(b);
            ClickPipelineScenarioFactory.SetElementAddress(bElement, 0xA002);
            harness.HoveredElement = bElement;

            LabelOnGround? selected = harness.ScanEngine.ResolveNextLabelCandidate(labels);

            selected.Should().BeSameAs(b,
                "hovering an open overlapping strongbox still prefers it over the ranked next label");
        }

        [TestMethod]
        public void LockedStrongboxOnScreen_ClosestOpenStrongboxStillSelected()
        {
            // Two strongboxes partially overlapping on screen; the CLOSEST one is locked. With penalty 0 every clickable label sorts purely by distance, so the next closest clickable label (the open strongbox) must be selected - never a farther target.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            RectangleF lockedRect = new(500f, 300f, 160f, 40f);
            RectangleF openRect = new(580f, 310f, 160f, 40f); // partially overlaps lockedRect
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, lockedRect);
            LabelOnGround open = Strongbox(30f, 0x02, locked: false, openRect);
            LabelOnGround itemA = WorldItem(50f, 0x03, new RectangleF(200f, 200f, 60f, 20f));
            LabelOnGround itemB = WorldItem(70f, 0x04, new RectangleF(1500f, 800f, 60f, 20f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([locked, open, itemA, itemB], 0, 4, isAcceptable: null);

            selected.Should().BeSameAs(open,
                "the closest clickable label is the open strongbox; the locked one must not block it");
        }

        [TestMethod]
        public void LockedStrongboxOnScreen_ItemsCloserThanOpenStrongbox_ClosestItemSelected()
        {
            // The locked strongbox is closest, but an ITEM is closer than the open strongbox. With penalty 0 the closest clickable label (the item) must win.
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround locked = Strongbox(10f, 0x01, locked: true, new RectangleF(500f, 300f, 160f, 40f));
            LabelOnGround nearItem = WorldItem(25f, 0x02, new RectangleF(900f, 600f, 60f, 20f));
            LabelOnGround open = Strongbox(40f, 0x03, locked: false, new RectangleF(580f, 310f, 160f, 40f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([locked, open, nearItem], 0, 3, isAcceptable: null);

            selected.Should().BeSameAs(nearItem, "with penalty 0 the closest clickable label (the item) must win over the farther open strongbox");
        }

        [TestMethod]
        public void EqualDistanceItems_StillSelectsExactlyOne()
        {
            // Two items at the same distance: the tie must still resolve deterministically to one of them (the pipeline must never stall on an equal-distance pair).
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround first = WorldItem(50f, 0x01, new RectangleF(100f, 100f, 60f, 20f));
            LabelOnGround second = WorldItem(50f, 0x02, new RectangleF(1500f, 800f, 60f, 20f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([first, second], 0, 2, isAcceptable: null);

            selected.Should().NotBeNull("an equal-distance tie must still resolve to one label");
            (selected == first || selected == second).Should().BeTrue();
        }

        [TestMethod]
        public void ThreeItems_ClosestSelected()
        {
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(BaseConfig());
            LabelOnGround near = WorldItem(10f, 0x01, new RectangleF(100f, 100f, 60f, 20f));
            LabelOnGround mid = WorldItem(40f, 0x02, new RectangleF(800f, 600f, 60f, 20f));
            LabelOnGround far = WorldItem(90f, 0x03, new RectangleF(1500f, 800f, 60f, 20f));

            harness.SelectionService.GetNextLabelToClick([far, mid, near], 0, 3, isAcceptable: null).Should().BeSameAs(near);
        }

        [TestMethod]
        public void ItemsRankedAboveStrongbox_WhenItemsHavePriority()
        {
            ClickPipelineScenarioFactory.ScenarioConfig config = BaseConfig();
            config.MechanicPriorityDistancePenalty = 100;
            config.MechanicPriorityIndexMap[MechanicIds.Items] = 0;
            config.MechanicPriorityIndexMap[MechanicIds.Strongboxes] = 1;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround strongbox = Strongbox(80f, 0x01, locked: false, new RectangleF(900f, 600f, 160f, 40f));
            LabelOnGround item = WorldItem(20f, 0x02, new RectangleF(100f, 100f, 60f, 20f));

            LabelOnGround? selected = harness.SelectionService.GetNextLabelToClick([strongbox, item], 0, 2, isAcceptable: null);

            selected.Should().BeSameAs(item, "when items have higher priority and the penalty is on, the item wins despite the closer strongbox");
        }

        [TestMethod]
        public void AllClickMechanicsDisabled_SelectsNothing()
        {
            ClickPipelineScenarioFactory.ScenarioConfig config = BaseConfig();
            config.ClickItems = false;
            config.ClickStrongboxes = false;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround item = WorldItem(10f, 0x01, new RectangleF(100f, 100f, 60f, 20f));
            LabelOnGround strongbox = Strongbox(20f, 0x02, locked: false, new RectangleF(800f, 600f, 160f, 40f));

            harness.SelectionService.GetNextLabelToClick([item, strongbox], 0, 2, isAcceptable: null).Should().BeNull(
                "with both item and strongbox clicking disabled every label has no mechanic and is rejected");
        }
    }
}
