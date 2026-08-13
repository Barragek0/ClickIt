namespace ClickIt.Tests.Behavior.Click
{
    // Scenario tests for the REAL LabelClickPointResolver geometry: simulate labels (strongboxes and world items) at real element rects, partially or fully obscured by unclickable rectangles, and verify the resolved click point is always inside the label, clickable, and outside every blocked rectangle - or that resolution correctly fails when nothing is clickable (walk path).
    [TestClass]
    public class ClickPointResolutionScenarioTests
    {
        private const string StrongboxPath = "Metadata/Chests/StrongBoxes/Arcanist";

        private static ClickPipelineScenarioFactory.ScenarioConfig Config(params RectangleF[] blocked)
            => new() { BlockedRects = [.. blocked] };

        private static ClickPipelineScenarioFactory.ScenarioHarness CreateHarness(params RectangleF[] blocked)
            => new(Config(blocked));

        [TestMethod]
        public void UnobscuredStrongbox_ResolvesClickPoint_InsideItsRect_AndClickable()
        {
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness();
            RectangleF rect = new(500f, 300f, 160f, 40f);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x101, locked: false),
                0x1001);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue("an unobscured on-screen strongbox must resolve a click point");
            AssertPointInside(rect, clickPos);
            harness.ClickableArea(clickPos).Should().BeTrue();
        }

        [TestMethod]
        public void Strongbox_WithLeftHalfBlocked_ResolvesClickPoint_InVisibleRightHalf()
        {
            RectangleF rect = new(500f, 300f, 200f, 40f);
            RectangleF leftBlock = new(480f, 290f, 120f, 60f); // covers x 480..600 of the rect
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(leftBlock);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x102, locked: false),
                0x1002);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue("a partially obscured strongbox must still resolve a click point");
            AssertPointInside(rect, clickPos);
            leftBlock.Contains(clickPos).Should().BeFalse("the click point must not land in the blocked rectangle");
            harness.ClickableArea(clickPos).Should().BeTrue();
        }

        [TestMethod]
        public void Strongbox_WithRightHalfBlocked_ResolvesClickPoint_InVisibleLeftHalf()
        {
            RectangleF rect = new(500f, 300f, 200f, 40f);
            RectangleF rightBlock = new(620f, 290f, 120f, 60f); // covers x 620..740
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(rightBlock);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x103, locked: false),
                0x1003);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue();
            AssertPointInside(rect, clickPos);
            rightBlock.Contains(clickPos).Should().BeFalse();
        }

        [TestMethod]
        public void Strongbox_WithCenterBandBlocked_ResolvesClickPoint_OutsideTheBand()
        {
            RectangleF rect = new(500f, 300f, 300f, 40f);
            RectangleF centerBand = new(620f, 290f, 60f, 60f); // covers x 620..680
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(centerBand);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x104, locked: false),
                0x1004);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue();
            AssertPointInside(rect, clickPos);
            centerBand.Contains(clickPos).Should().BeFalse();
        }

        [TestMethod]
        public void Strongbox_WithTopBandBlocked_ResolvesClickPoint_InLowerPart()
        {
            RectangleF rect = new(500f, 300f, 160f, 60f);
            RectangleF topBand = new(490f, 290f, 180f, 30f); // covers y 290..320
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(topBand);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x105, locked: false),
                0x1005);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue();
            AssertPointInside(rect, clickPos);
            topBand.Contains(clickPos).Should().BeFalse();
            clickPos.Y.Should().BeGreaterThan(topBand.Bottom, "the click point must land below the blocked band");
        }

        [TestMethod]
        public void Strongbox_WithTwoDisjointBlockedRects_ResolvesClickPoint_InRemainingSliver()
        {
            RectangleF rect = new(500f, 300f, 300f, 40f);
            RectangleF leftBlock = new(480f, 290f, 130f, 60f); // x 480..610
            RectangleF rightBlock = new(690f, 290f, 130f, 60f); // x 690..820
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(leftBlock, rightBlock);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x106, locked: false),
                0x1006);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue("a visible sliver between two blocked rectangles must still be clickable");
            AssertPointInside(rect, clickPos);
            leftBlock.Contains(clickPos).Should().BeFalse();
            rightBlock.Contains(clickPos).Should().BeFalse();
            clickPos.X.Should().BeGreaterThanOrEqualTo(leftBlock.Right);
            clickPos.X.Should().BeLessThanOrEqualTo(rightBlock.Left);
        }

        [TestMethod]
        public void Strongbox_WithPreferredPointBlocked_ResolvesClickPoint_ElsewhereInRect()
        {
            // The preferred point is the rect center; block only a small square around it.
            RectangleF rect = new(500f, 300f, 200f, 40f);
            RectangleF centerBlock = new(580f, 305f, 40f, 30f);
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(centerBlock);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x107, locked: false),
                0x1007);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue();
            AssertPointInside(rect, clickPos);
            centerBlock.Contains(clickPos).Should().BeFalse("the resolver must move off a blocked preferred point");
        }

        [TestMethod]
        public void Strongbox_FullyCoveredByOneBlockedRect_ResolvesNoClickPoint()
        {
            RectangleF rect = new(500f, 300f, 200f, 40f);
            RectangleF fullBlock = new(490f, 290f, 220f, 60f);
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(fullBlock);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x108, locked: false),
                0x1008);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out _);

            ok.Should().BeFalse("a fully obscured strongbox has no clickable point and must fall to the walk path");
        }

        [TestMethod]
        public void Strongbox_FullyCoveredByMultipleDisjointBlockedRects_ResolvesNoClickPoint()
        {
            RectangleF rect = new(500f, 300f, 300f, 40f);
            RectangleF left = new(490f, 290f, 110f, 60f); // covers 490..600
            RectangleF middle = new(600f, 290f, 100f, 60f); // covers 600..700
            RectangleF right = new(700f, 290f, 110f, 60f); // covers 700..810; rect is 500..800
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(left, middle, right);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x109, locked: false),
                0x1009);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out _);

            ok.Should().BeFalse("multiple disjoint blocks covering the whole rect must make it unclickable");
        }

        [TestMethod]
        public void TwoOverlappingStrongboxLabels_ResolveClickPoint_AvoidingTheOverlap()
        {
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness();
            RectangleF firstRect = new(500f, 300f, 200f, 40f);
            RectangleF secondRect = new(600f, 300f, 200f, 40f); // overlaps x 600..700
            var first = ClickPipelineScenarioFactory.CreateLabel(
                firstRect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x10A, locked: false),
                0x100A);
            var second = ClickPipelineScenarioFactory.CreateLabel(
                secondRect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x10B, locked: false),
                0x100B);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                first, Vector2.Zero, [first, second], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue();
            AssertPointInside(firstRect, clickPos);
            secondRect.Contains(clickPos).Should().BeFalse(
                "the click point for an overlapped label must avoid the overlapping label's rect");
        }

        [TestMethod]
        public void Strongbox_ClickPoint_RespectsChestHeightOffset()
        {
            var settings = new ClickItSettings();
            settings.ChestHeightOffset.Value = 12;
            var resolver = new LabelClickPointResolver(settings);
            RectangleF rect = new(500f, 300f, 160f, 40f);

            Vector2 clickPos = resolver.CalculateClickPosition(
                rect,
                EntityType.Chest,
                StrongboxPath,
                "Arcanist's Strongbox",
                windowTopLeft: Vector2.Zero,
                blockedAreas: [],
                avoidOverlapsEnabled: false);

            AssertPointInside(rect, clickPos);
            clickPos.Y.Should().BeLessThan(rect.Center.Y, "the chest height offset raises the preferred click point");
        }

        [TestMethod]
        public void WorldItem_Unobscured_ResolvesClickPoint_InsideItsRect()
        {
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness();
            RectangleF rect = new(700f, 500f, 120f, 20f);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateWorldItem(distance: 15f, address: 0x10C),
                0x100C);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue();
            AssertPointInside(rect, clickPos);
            harness.ClickableArea(clickPos).Should().BeTrue();
        }

        [TestMethod]
        public void WorldItem_PartiallyObscured_ResolvesClickPoint_InVisiblePart()
        {
            RectangleF rect = new(700f, 500f, 120f, 20f);
            RectangleF blocker = new(760f, 495f, 80f, 30f); // covers x 760..840
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(blocker);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateWorldItem(distance: 15f, address: 0x10D),
                0x100D);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue();
            AssertPointInside(rect, clickPos);
            blocker.Contains(clickPos).Should().BeFalse();
        }

        [TestMethod]
        public void Label_CompletelyOutsideWindow_ResolvesNoClickPoint()
        {
            // The clickable area predicate rejects anything outside the window; a label whose rect sits entirely off-window must resolve nothing.
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness();
            RectangleF rect = new(5000f, 5000f, 160f, 40f);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 5f, address: 0x10E, locked: false),
                0x100E);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out _);

            ok.Should().BeFalse("an off-window label must not resolve a click point");
        }

        [TestMethod]
        public void Strongbox_WithEdgeTouchingBlocker_StillResolvesClickPoint()
        {
            // A blocker that only kisses the label's right edge leaves the rest clickable.
            RectangleF rect = new(500f, 300f, 200f, 40f);
            RectangleF edgeBlock = new(700f, 290f, 30f, 60f); // touches rect.Right
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(edgeBlock);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x110, locked: false),
                0x1010);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue("a blocker touching only the edge must still leave a clickable point");
            AssertPointInside(rect, clickPos);
            edgeBlock.Contains(clickPos).Should().BeFalse();
        }

        [TestMethod]
        public void HeistContractWorldItem_ResolvesClickPoint_InLowerPartOfRect()
        {
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness();
            RectangleF rect = new(700f, 500f, 140f, 24f);
            Entity item = ClickPipelineScenarioFactory.CreateWorldItem(
                distance: 15f,
                address: 0x111,
                path: "Metadata/Items/Heist/Contracts/Deception/Contract1");
            var label = ClickPipelineScenarioFactory.CreateLabel(rect, item, 0x1011);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue();
            AssertPointInside(rect, clickPos);
            clickPos.Y.Should().BeGreaterThan(rect.Center.Y, "heist contracts prefer a click point in the lower part of the label");
        }

        [TestMethod]
        public void ClickableAreaRestrictedToTinyRegion_ResolvesClickPoint_WithinIt()
        {
            // Only a small sub-rectangle of the label is clickable; the resolver must find a point inside that sub-rectangle.
            RectangleF rect = new(500f, 300f, 300f, 60f);
            RectangleF onlyClickable = new(700f, 315f, 40f, 30f);
            var config = new ClickPipelineScenarioFactory.ScenarioConfig();
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x112, locked: false),
                0x1012);

            // Custom clickable predicate: only the tiny sub-rectangle is clickable.
            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], point => onlyClickable.Contains(point), out Vector2 clickPos);

            ok.Should().BeTrue("a tiny clickable region inside the label must still be found");
            onlyClickable.Contains(clickPos).Should().BeTrue();
        }

        [TestMethod]
        public void Strongbox_WithDiagonalVisibleStrip_ResolvesClickPoint_InStrip()
        {
            // Two corner blocks leave a diagonal visible strip across the middle.
            RectangleF rect = new(500f, 300f, 300f, 60f);
            RectangleF topLeft = new(490f, 290f, 120f, 35f);
            RectangleF bottomRight = new(690f, 325f, 120f, 45f);
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(topLeft, bottomRight);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x113, locked: false),
                0x1013);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out Vector2 clickPos);

            ok.Should().BeTrue("the diagonal visible strip must still yield a clickable point");
            AssertPointInside(rect, clickPos);
            topLeft.Contains(clickPos).Should().BeFalse();
            bottomRight.Contains(clickPos).Should().BeFalse();
        }

        [TestMethod]
        public void OverlappingBlockedRects_FullyCoverLabel_ResolveNoClickPoint()
        {
            // Two blocked rectangles that overlap each other still cover the whole label rect.
            RectangleF rect = new(500f, 300f, 300f, 60f);
            RectangleF top = new(490f, 290f, 320f, 40f);
            RectangleF bottom = new(490f, 330f, 320f, 40f);
            ClickPipelineScenarioFactory.ScenarioHarness harness = CreateHarness(top, bottom);
            var label = ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 30f, address: 0x114, locked: false),
                0x1014);

            bool ok = harness.ClickPointResolver.TryCalculateClickPosition(
                label, Vector2.Zero, [label], harness.ClickableArea, out _);

            ok.Should().BeFalse("overlapping blocks that together cover the whole rect must make it unclickable");
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
