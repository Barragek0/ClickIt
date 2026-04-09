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
            RectangleF mirageRect = new(13, 23, 33, 43);
            RectangleF altarRect = new(14, 24, 34, 44);
            RectangleF ritualRect = new(15, 25, 35, 45);
            RectangleF sentinelRect = new(16, 26, 36, 46);

            AreaBlockedRectangleResolver.ResolveChatPanelBlockedRectangleFromRoot(CreatePathRoot(chatRect, 1, 2, 2)).Should().Be(chatRect);
            AreaBlockedRectangleResolver.ResolveMapPanelBlockedRectangleFromRoot(CreatePathRoot(mapRect, 2, 1)).Should().Be(mapRect);
            AreaBlockedRectangleResolver.ResolveXpBarBlockedRectangleFromRoot(CreatePathRoot(xpRect, 0)).Should().Be(xpRect);
            AreaBlockedRectangleResolver.ResolveMirageBlockedRectangleFromRoot(CreateVisiblePathRoot(mirageRect, 7, 17)).Should().Be(mirageRect);
            AreaBlockedRectangleResolver.ResolveAltarBlockedRectangleFromRoot(CreateVisiblePathRoot(altarRect, 7, 16)).Should().Be(altarRect);
            AreaBlockedRectangleResolver.ResolveRitualBlockedRectangleFromRoot(CreateVisiblePathRoot(ritualRect, 7, 18, 0)).Should().Be(ritualRect);
            AreaBlockedRectangleResolver.ResolveSentinelBlockedRectangleFromRoot(CreatePathRoot(sentinelRect, 7, 18, 2, 0)).Should().Be(sentinelRect);
        }

        [TestMethod]
        public void VisibleRectangleResolvers_ReturnEmpty_WhenLeafIsHidden()
        {
            var hiddenRoot = CreatePathRoot(new RectangleF(1, 2, 10, 10), 7, 17);
            EnsurePath(hiddenRoot, [7, 17]).IsVisible = false;

            AreaBlockedRectangleResolver.ResolveMirageBlockedRectangleFromRoot(hiddenRoot).Should().Be(RectangleF.Empty);
            AreaBlockedRectangleResolver.ResolveAltarBlockedRectangleFromRoot(null).Should().Be(RectangleF.Empty);
        }

        [TestMethod]
        public void ResolveBuffsAndDebuffsBlockedRectanglesFromRoot_AddsOnlyValidRects()
        {
            var root = new AreaUiNodeTraversalTests.FakeNode();
            var child1 = EnsurePath(root, [1]);
            EnsureIndexedChild(child1, 23).ClientRect = new RectangleF(1, 2, 10, 10);
            EnsureIndexedChild(child1, 24).ClientRect = new RectangleF(3, 4, 1, 10);

            List<RectangleF> rectangles = AreaBlockedRectangleCollectionResolver.ResolveBuffsAndDebuffsBlockedRectanglesFromRoot(root);

            rectangles.Should().Equal([new RectangleF(1, 2, 10, 10)]);
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