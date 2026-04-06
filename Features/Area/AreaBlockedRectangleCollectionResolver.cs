namespace ClickIt.Features.Area
{
    internal static class AreaBlockedRectangleCollectionResolver
    {
        internal static List<RectangleF> ResolveBuffsAndDebuffsBlockedRectangles(GameController gameController)
            => ResolveBuffsAndDebuffsBlockedRectanglesFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "Root"));

        internal static List<RectangleF> ResolveBuffsAndDebuffsBlockedRectanglesFromRoot(object? root)
        {
            var blocked = new List<RectangleF>(2);
            if (!AreaUiNodeTraversal.TryGetChildNode(root, 1, out object? child1) || child1 == null)
                return blocked;

            TryAddValidRectFromChild(child1, 23, blocked);
            TryAddValidRectFromChild(child1, 24, blocked);
            return blocked;
        }

        internal static List<RectangleF> ResolveQuestTrackerBlockedRectangles(GameController gameController)
            => ResolveQuestTrackerBlockedRectanglesFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "QuestTracker"));

        internal static List<RectangleF> ResolveQuestTrackerBlockedRectanglesFromRoot(object? root)
        {
            var blocked = new List<RectangleF>();
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

        private static void TryAddValidRectFromChild(object source, int childIndex, List<RectangleF> output)
        {
            if (!AreaUiNodeTraversal.TryGetChildNode(source, childIndex, out object? node) || node == null)
                return;

            if (AreaUiNodeTraversal.TryGetClientRect(node, out RectangleF rect) && rect.Width > 1f && rect.Height > 1f)
                output.Add(rect);
        }
    }
}