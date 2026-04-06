namespace ClickIt.Features.Area
{
    internal static class AreaBlockedRectangleResolver
    {
        internal static RectangleF ResolveChatPanelBlockedRectangle(GameController gameController)
            => ResolveChatPanelBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "ChatPanel"));

        internal static RectangleF ResolveChatPanelBlockedRectangleFromRoot(object? root)
            => AreaUiNodeTraversal.ResolveRectangleFromNodePath(root, 1, 2, 2);

        internal static RectangleF ResolveMapPanelBlockedRectangle(GameController gameController)
            => ResolveMapPanelBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "Map"));

        internal static RectangleF ResolveMapPanelBlockedRectangleFromRoot(object? root)
            => AreaUiNodeTraversal.ResolveRectangleFromNodePath(root, 2, 1);

        internal static RectangleF ResolveXpBarBlockedRectangle(GameController gameController)
            => ResolveXpBarBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"));

        internal static RectangleF ResolveXpBarBlockedRectangleFromRoot(object? root)
            => AreaUiNodeTraversal.ResolveRectangleFromNodePath(root, 0);

        internal static RectangleF ResolveMirageBlockedRectangle(GameController gameController)
            => ResolveMirageBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"));

        internal static RectangleF ResolveMirageBlockedRectangleFromRoot(object? root)
            => AreaUiNodeTraversal.ResolveVisibleRectangleFromNodePath(root, 7, 17);

        internal static RectangleF ResolveAltarBlockedRectangle(GameController gameController)
            => ResolveAltarBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"));

        internal static RectangleF ResolveAltarBlockedRectangleFromRoot(object? root)
            => AreaUiNodeTraversal.ResolveVisibleRectangleFromNodePath(root, 7, 16);

        internal static RectangleF ResolveRitualBlockedRectangle(GameController gameController)
            => ResolveRitualBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"));

        internal static RectangleF ResolveRitualBlockedRectangleFromRoot(object? root)
            => AreaUiNodeTraversal.ResolveVisibleRectangleFromNodePath(root, 7, 18, 0);

        internal static RectangleF ResolveSentinelBlockedRectangle(GameController gameController)
            => ResolveSentinelBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"));

        internal static RectangleF ResolveSentinelBlockedRectangleFromRoot(object? root)
            => AreaUiNodeTraversal.ResolveRectangleFromNodePath(root, 7, 18, 2, 0);
    }
}