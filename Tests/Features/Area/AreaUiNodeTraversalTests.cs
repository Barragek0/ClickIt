namespace ClickIt.Tests.Features.Area
{
    [TestClass]
    public class AreaUiNodeTraversalTests
    {
        [TestMethod]
        public void TryResolveRectangleFromNodePath_ReturnsFalse_WhenPathOrRectangleIsInvalid()
        {
            var root = new FakeNode();
            root.Children.Add(new FakeNode { ClientRect = new RectangleF(0, 0, 1, 10) });

            AreaUiNodeTraversal.TryResolveRectangleFromNodePath(root, requireVisibleElement: false, childPath: [1], out RectangleF missingRect).Should().BeFalse();
            missingRect.Should().Be(RectangleF.Empty);

            AreaUiNodeTraversal.TryResolveRectangleFromNodePath(root, requireVisibleElement: false, childPath: [0], out RectangleF tinyRect).Should().BeFalse();
            tinyRect.Should().Be(RectangleF.Empty);
        }

        [TestMethod]
        public void TryResolveRectangleFromNodePath_ReturnsRectangle_ForVisibleAndNonVisiblePaths()
        {
            var rect = new RectangleF(10, 20, 40, 50);
            var root = new FakeNode();
            var branch = new FakeNode();
            var leaf = new FakeNode { ClientRect = rect, IsValid = true, IsVisible = true };
            branch.Children.Add(leaf);
            root.Children.Add(branch);

            AreaUiNodeTraversal.TryResolveRectangleFromNodePath(root, requireVisibleElement: false, childPath: [0, 0], out RectangleF resolved).Should().BeTrue();
            resolved.Should().Be(rect);

            AreaUiNodeTraversal.TryResolveRectangleFromNodePath(root, requireVisibleElement: true, childPath: [0, 0], out RectangleF visibleResolved).Should().BeTrue();
            visibleResolved.Should().Be(rect);
        }

        [TestMethod]
        public void TryResolveRectangleFromNodePath_RequiresValidVisibleNode_WhenRequested()
        {
            var root = new FakeNode
            {
                Children =
                {
                    new FakeNode
                    {
                        Children =
                        {
                            new FakeNode { ClientRect = new RectangleF(1, 2, 10, 10), IsValid = true, IsVisible = false }
                        }
                    }
                }
            };

            AreaUiNodeTraversal.TryResolveRectangleFromNodePath(root, requireVisibleElement: true, childPath: [0, 0], out RectangleF rect).Should().BeFalse();
            rect.Should().Be(RectangleF.Empty);
        }

        [TestMethod]
        public void ResolveChildNodes_StopsAtFirstMissingChild()
        {
            var root = new FakeNode();
            root.Children.Add(new FakeNode());
            root.Children.Add(new FakeNode());

            List<object?> children = AreaUiNodeTraversal.ResolveChildNodes(root);

            children.Should().HaveCount(2);
        }

        [TestMethod]
        public void TryGetChildNode_And_TryGetClientRect_UseObjectFallbackMembers()
        {
            var child = new FakeNode { ClientRect = new RectangleF(3, 4, 5, 6) };
            var root = new FakeNode();
            root.Children.Add(child);

            AreaUiNodeTraversal.TryGetChildNode(root, 0, out object? resolvedChild).Should().BeTrue();
            resolvedChild.Should().BeSameAs(child);

            AreaUiNodeTraversal.TryGetClientRect(child, out RectangleF rect).Should().BeTrue();
            rect.Should().Be(child.ClientRect);
        }

        [TestMethod]
        public void TryReadVisibility_UsesObjectFallbackMembers()
        {
            AreaUiNodeTraversal.TryReadVisibility(new FakeNode { IsValid = true, IsVisible = false }, out bool isValid, out bool isVisible).Should().BeTrue();

            isValid.Should().BeTrue();
            isVisible.Should().BeFalse();
        }

        public sealed class FakeNode
        {
            public List<FakeNode> Children { get; } = [];

            public RectangleF ClientRect { get; set; }

            public bool IsValid { get; set; } = true;

            public bool IsVisible { get; set; } = true;
        }
    }
}