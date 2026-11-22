namespace ClickIt.Tests.Features.Area
{
    [TestClass]
    public class AreaBlockedRectangleResolversTests
    {
        [TestMethod]
        public void RectangleResolvers_ReturnExpectedRectangles_FromObjectRoots()
        {
            RectangleF chatRect = new(10, 20, 30, 40);
            RectangleF mapRect = new(11, 21, 31, 41);
            RectangleF xpRect = new(12, 22, 32, 42);
            RectangleF buttonsRect = new(13, 23, 33, 43);
            RectangleF ritualRect = new(15, 25, 35, 45);
            RectangleF sentinelRect = new(16, 26, 36, 46);
            RectangleF expeditionRect = new(17, 27, 37, 47);

            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreatePathRoot(chatRect, 1, 2, 2), AreaBlockedRectangleKind.ChatPanel).Should().Be(chatRect);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreatePathRoot(mapRect, 2, 1), AreaBlockedRectangleKind.MapPanel).Should().Be(mapRect);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreatePathRoot(xpRect, 0), AreaBlockedRectangleKind.XpBar).Should().Be(xpRect);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreateVisiblePathRoot(buttonsRect, 7, 17), AreaBlockedRectangleKind.Buttons).Should().Be(buttonsRect);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreateVisiblePathRoot(ritualRect, 7, 18, 0), AreaBlockedRectangleKind.Ritual).Should().Be(ritualRect);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreatePathRoot(sentinelRect, 7, 18, 2, 0), AreaBlockedRectangleKind.Sentinel).Should().Be(sentinelRect);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreateVisiblePathRoot(expeditionRect, 7, 18, 1), AreaBlockedRectangleKind.Expedition).Should().Be(expeditionRect);

            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.ChatPanel).Should().Be("Chat");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.MapPanel).Should().Be("Map");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.XpBar).Should().Be("XP Bar");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.Buttons).Should().Be("Buttons");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.Ritual).Should().Be("Ritual");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.Sentinel).Should().Be("Sentinel");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.Expedition).Should().Be("Expedition");
        }

        [TestMethod]
        public void VisibleRectangleResolvers_ReturnEmpty_WhenLeafIsHidden()
        {
            var hiddenRoot = CreatePathRoot(new RectangleF(1, 2, 10, 10), 7, 18);
            EnsurePath(hiddenRoot, [7, 18]).IsVisible = false;

            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(hiddenRoot, AreaBlockedRectangleKind.Buttons).Should().Be(RectangleF.Empty);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(null, AreaBlockedRectangleKind.Buttons).Should().Be(RectangleF.Empty);
        }

        [TestMethod]
        public void ResolveBuffsAndDebuffsBlockedRectanglesFromRoot_MapsVisibleRectanglesByChildIndex()
        {
            var root = new AreaUiNodeTraversalTests.FakeNode();
            var child1 = EnsurePath(root, [1]);
            EnsureIndexedChild(child1, 23).ClientRect = new RectangleF(1, 2, 10, 10);
            EnsureIndexedChild(child1, 24).ClientRect = new RectangleF(3, 4, 10, 10);
            EnsureIndexedChild(child1, 25).ClientRect = new RectangleF(5, 6, 10, 10);

            (RectangleF buffs, RectangleF debuffs) = AreaBlockedRectangleCollectionResolver.ResolveBuffsAndDebuffsBlockedRectanglesFromRoot(root);

            buffs.Should().Be(new RectangleF(3, 4, 10, 10));
            debuffs.Should().Be(new RectangleF(5, 6, 10, 10));
        }

        [TestMethod]
        public void ResolveBuffsAndDebuffsBlockedRectanglesFromRoot_ExcludesHiddenRectangles()
        {
            var root = new AreaUiNodeTraversalTests.FakeNode();
            var child1 = EnsurePath(root, [1]);
            EnsureIndexedChild(child1, 24).ClientRect = new RectangleF(3, 4, 10, 10);
            EnsureIndexedChild(child1, 25).ClientRect = new RectangleF(5, 6, 10, 10);
            EnsureIndexedChild(child1, 25).IsVisible = false;

            (RectangleF buffs, RectangleF debuffs) = AreaBlockedRectangleCollectionResolver.ResolveBuffsAndDebuffsBlockedRectanglesFromRoot(root);

            buffs.Should().Be(new RectangleF(3, 4, 10, 10));
            debuffs.Should().Be(RectangleF.Empty);
        }

        [TestMethod]
        public void ResolveBuffsAndDebuffsBlockedRectanglesFromRoot_PreservesDebuffSlotWhenBuffsAreHidden()
        {
            var root = new AreaUiNodeTraversalTests.FakeNode();
            var child1 = EnsurePath(root, [1]);
            EnsureIndexedChild(child1, 24).ClientRect = new RectangleF(3, 4, 10, 10);
            EnsureIndexedChild(child1, 24).IsVisible = false;
            EnsureIndexedChild(child1, 25).ClientRect = new RectangleF(5, 6, 10, 10);

            (RectangleF buffs, RectangleF debuffs) = AreaBlockedRectangleCollectionResolver.ResolveBuffsAndDebuffsBlockedRectanglesFromRoot(root);

            buffs.Should().Be(RectangleF.Empty);
            debuffs.Should().Be(new RectangleF(5, 6, 10, 10));
        }

        [TestMethod]
        public void ResolveQuestTrackerBlockedRectanglesFromRoot_CollectsOnlyValidClickableChildren()
        {
            var root = new AreaUiNodeTraversalTests.FakeNode();
            var rowsRoot = EnsurePath(root, [0, 0]);

            var validRow = new AreaUiNodeTraversalTests.FakeNode();
            EnsureIndexedChild(validRow, 1).ClientRect = new RectangleF(10, 20, 30, 40);
            rowsRoot.Children.Add(validRow);

            var tinyRow = new AreaUiNodeTraversalTests.FakeNode();
            EnsureIndexedChild(tinyRow, 1).ClientRect = new RectangleF(0, 0, 1, 40);
            rowsRoot.Children.Add(tinyRow);

            var missingClickableRow = new AreaUiNodeTraversalTests.FakeNode();
            rowsRoot.Children.Add(missingClickableRow);

            List<RectangleF> rectangles = AreaBlockedRectangleCollectionResolver.ResolveQuestTrackerBlockedRectanglesFromRoot(root);

            rectangles.Should().Equal([new RectangleF(10, 20, 30, 40)]);
        }

        [TestMethod]
        public void ResolveQuestTrackerBlockedRectanglesFromRoot_ReturnsEmpty_WhenRootPathMissing()
        {
            AreaBlockedRectangleCollectionResolver.ResolveQuestTrackerBlockedRectanglesFromRoot(new AreaUiNodeTraversalTests.FakeNode())
                .Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void ButtonsResolver_ReadsChild17_NotTheFormerChild18()
        {
            var root = CreateVisiblePathRoot(new RectangleF(7, 17, 10, 10), 7, 17);
            AreaUiNodeTraversalTests.FakeNode stale = EnsureIndexedChild(EnsurePath(root, [7]), 18);
            stale.ClientRect = new RectangleF(99, 99, 10, 10);
            stale.IsValid = true;
            stale.IsVisible = true;

            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(root, AreaBlockedRectangleKind.Buttons)
                .Should().Be(new RectangleF(7, 17, 10, 10));
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void ExpeditionResolver_ReadsNestedPath_NotARootLevel18_1()
        {
            var root = CreateVisiblePathRoot(new RectangleF(7, 18, 10, 10), 7, 18, 1);
            AreaUiNodeTraversalTests.FakeNode staleRootPath = EnsurePath(root, [18, 1]);
            staleRootPath.ClientRect = new RectangleF(99, 99, 10, 10);
            staleRootPath.IsValid = true;
            staleRootPath.IsVisible = true;

            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(root, AreaBlockedRectangleKind.Expedition)
                .Should().Be(new RectangleF(7, 18, 10, 10));
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void ExpeditionResolver_ReturnsEmpty_WhenLeafIsHidden()
        {
            var hiddenRoot = CreatePathRoot(new RectangleF(1, 2, 10, 10), 7, 18, 1);
            EnsurePath(hiddenRoot, [7, 18, 1]).IsVisible = false;

            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(hiddenRoot, AreaBlockedRectangleKind.Expedition)
                .Should().Be(RectangleF.Empty);
        }

        private static AreaUiNodeTraversalTests.FakeNode CreatePathRoot(RectangleF rect, params int[] path)
        {
            var root = new AreaUiNodeTraversalTests.FakeNode();
            EnsurePath(root, path).ClientRect = rect;
            return root;
        }

        private static AreaUiNodeTraversalTests.FakeNode CreateVisiblePathRoot(RectangleF rect, params int[] path)
        {
            var root = CreatePathRoot(rect, path);
            AreaUiNodeTraversalTests.FakeNode leaf = EnsurePath(root, path);
            leaf.IsValid = true;
            leaf.IsVisible = true;
            return root;
        }

        private static AreaUiNodeTraversalTests.FakeNode EnsurePath(AreaUiNodeTraversalTests.FakeNode root, int[] path)
        {
            AreaUiNodeTraversalTests.FakeNode current = root;
            for (int i = 0; i < path.Length; i++)
                current = EnsureIndexedChild(current, path[i]);


            return current;
        }

        private static AreaUiNodeTraversalTests.FakeNode EnsureIndexedChild(AreaUiNodeTraversalTests.FakeNode parent, int index)
        {
            while (parent.Children.Count <= index)
                parent.Children.Add(new AreaUiNodeTraversalTests.FakeNode());


            return parent.Children[index];
        }
    }
}
