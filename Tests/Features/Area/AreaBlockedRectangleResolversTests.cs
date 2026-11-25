namespace ClickIt.Tests.Features.Area
{
    [TestClass]
    public class AreaBlockedRectangleResolversTests
    {
        [TestMethod]
        [TestCategory("Regression")]
        public void ButtonRectangles_CollectVisibleChildrenAcrossAllThreeContainers()
        {
            var root = new AreaUiNodeTraversalTests.FakeNode();
            var first = EnsurePath(root, [7, 17, 0]);
            first.ClientRect = new RectangleF(10, 20, 30, 40);
            var second = EnsurePath(root, [7, 18, 0]);
            second.ClientRect = new RectangleF(50, 20, 30, 40);
            var hidden = EnsurePath(root, [7, 18, 1]);
            hidden.ClientRect = new RectangleF(90, 20, 30, 40);
            hidden.IsVisible = false;
            var third = EnsurePath(root, [7, 19, 0]);
            third.ClientRect = new RectangleF(130, 20, 30, 40);

            AreaBlockedRectangleCollectionResolver.ResolveButtonRectanglesFromRoot(root)
                .Should().Equal(first.ClientRect, second.ClientRect, third.ClientRect);
            EnsurePath(root, [7, 19]).IsVisible = false;
            AreaBlockedRectangleCollectionResolver.ResolveButtonRectanglesFromRoot(root)
                .Should().Equal(first.ClientRect, second.ClientRect);
        }

        [TestMethod]
        public void RectangleResolvers_ReturnExpectedRectangles_FromObjectRoots()
        {
            RectangleF chatRect = new(10, 20, 30, 40);
            RectangleF mapRect = new(11, 21, 31, 41);
            RectangleF xpRect = new(12, 22, 32, 42);
            RectangleF delveRect = new(13, 23, 33, 43);

            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreatePathRoot(chatRect, 1, 2, 2), AreaBlockedRectangleKind.ChatPanel).Should().Be(chatRect);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreatePathRoot(mapRect, 2, 1), AreaBlockedRectangleKind.MapPanel).Should().Be(mapRect);
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(CreatePathRoot(xpRect, 0), AreaBlockedRectangleKind.XpBar).Should().Be(xpRect);
            List<object?> skills = Enumerable.Range(0, 14).Select(_ => (object?)new AreaUiNodeTraversalTests.FakeNode()).ToList();
            ((AreaUiNodeTraversalTests.FakeNode)skills[13]!).Children.Add(new AreaUiNodeTraversalTests.FakeNode { ClientRect = delveRect });
            AreaBlockedRectangleResolver.ResolveBlockedRectangleFromRoot(skills, AreaBlockedRectangleKind.Delve).Should().Be(delveRect);

            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.ChatPanel).Should().Be("Chat");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.MapPanel).Should().Be("Map");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.XpBar).Should().Be("XP Bar");
            AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.Delve).Should().Be("Delve");
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
