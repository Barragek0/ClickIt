namespace ClickIt.Features.Area
{
    internal static class AreaBlockedRectangleResolver
    {
        internal static RectangleF ResolveChatPanelBlockedRectangle(GameController gameController)
            => AreaUiNodeTraversal.ResolveRectangleFromNodePath(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "ChatPanel"), 1, 2, 2);

        internal static RectangleF ResolveMapPanelBlockedRectangle(GameController gameController)
            => AreaUiNodeTraversal.ResolveRectangleFromNodePath(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "Map"), 2, 1);

        internal static RectangleF ResolveXpBarBlockedRectangle(GameController gameController)
            => AreaUiNodeTraversal.ResolveRectangleFromNodePath(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"), 0);

        internal static RectangleF ResolveMirageBlockedRectangle(GameController gameController)
            => AreaUiNodeTraversal.ResolveVisibleRectangleFromNodePath(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"), 7, 17);

        internal static RectangleF ResolveAltarBlockedRectangle(GameController gameController)
            => AreaUiNodeTraversal.ResolveVisibleRectangleFromNodePath(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"), 7, 16);

        internal static RectangleF ResolveRitualBlockedRectangle(GameController gameController)
            => AreaUiNodeTraversal.ResolveVisibleRectangleFromNodePath(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"), 7, 18, 0);

        internal static RectangleF ResolveSentinelBlockedRectangle(GameController gameController)
            => AreaUiNodeTraversal.ResolveRectangleFromNodePath(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, "GameUI"), 7, 18, 2, 0);
    }
}