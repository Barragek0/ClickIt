namespace ClickIt.Features.Area
{
    internal static class AreaBlockedRectangleCollectionResolver
    {
        internal static List<RectangleF> ResolveButtonRectangles(GameController controller)
            => ResolveButtonRectanglesFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(controller, "GameUI"));

        internal static List<RectangleF> ResolveButtonRectanglesFromRoot(object? root)
        {
            List<RectangleF> rectangles = [];
            if (!AreaUiNodeTraversal.TryGetChildNode(root, 7, out object? group) || group == null)
                return rectangles;

            for (int index = 17; index <= 19; index++)
            {
                if (!AreaUiNodeTraversal.TryGetChildNode(group, index, out object? container) || container == null
                    || !AreaUiNodeTraversal.TryReadVisibility(container, out bool valid, out bool visible) || !valid || !visible)
                    continue;
                foreach (object? child in AreaUiNodeTraversal.ResolveChildNodes(container))
                {
                    if (child != null && AreaUiNodeTraversal.TryReadVisibility(child, out bool childValid, out bool childVisible)
                        && childValid && childVisible && AreaUiNodeTraversal.TryGetClientRect(child, out RectangleF rect)
                        && rect.Width > 1f && rect.Height > 1f && !rectangles.Contains(rect))
                        rectangles.Add(rect);
                }
            }
            return rectangles;
        }

        internal static (RectangleF Buffs, RectangleF Debuffs) ResolveBuffsAndDebuffsBlockedRectangles(GameController gameController)
            => ResolveBuffsAndDebuffsBlockedRectanglesFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "Root"));

        internal static (RectangleF Buffs, RectangleF Debuffs) ResolveBuffsAndDebuffsBlockedRectanglesFromRoot(object? root)
        {
            if (!AreaUiNodeTraversal.TryGetChildNode(root, 1, out object? child1) || child1 == null)
                return (RectangleF.Empty, RectangleF.Empty);

            return (ResolveVisibleRectFromChild(child1, 24), ResolveVisibleRectFromChild(child1, 25));
        }

        internal static List<RectangleF> ResolveQuestTrackerBlockedRectangles(GameController gameController)
            => ResolveQuestTrackerBlockedRectanglesFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "QuestTracker"));

        internal static List<RectangleF> ResolveQuestTrackerBlockedRectanglesFromRoot(object? root)
        {
            List<RectangleF> blocked = [];
            if (!AreaUiNodeTraversal.TryGetChildNode(root, 0, out object? child0) || child0 == null)
                return blocked;
            if (!AreaUiNodeTraversal.TryGetChildNode(child0, 0, out object? rowsRoot) || rowsRoot == null)
                return blocked;

            List<object?> children = AreaUiNodeTraversal.ResolveChildNodes(rowsRoot);
            for (int i = 0; i < children.Count; i++)
            {
                if (!AreaUiNodeTraversal.TryGetChildNode(children[i], 1, out object? clickableContainer) || clickableContainer == null)
                    continue;

                if (!AreaUiNodeTraversal.TryGetClientRect(clickableContainer, out RectangleF clickableRect))
                    continue;

                if (clickableRect.Width > 1f && clickableRect.Height > 1f)
                    blocked.Add(clickableRect);
            }

            return blocked;
        }

        private static RectangleF ResolveVisibleRectFromChild(object source, int childIndex)
        {
            if (!AreaUiNodeTraversal.TryGetChildNode(source, childIndex, out object? node) || node == null)
                return RectangleF.Empty;

            if (!AreaUiNodeTraversal.TryReadVisibility(node, out bool isValid, out bool isVisible)
                || !AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(isValid, isVisible))
                return RectangleF.Empty;

            return AreaUiNodeTraversal.TryGetClientRect(node, out RectangleF rect) && rect.Width > 1f && rect.Height > 1f
                ? rect
                : RectangleF.Empty;
        }
    }
}
