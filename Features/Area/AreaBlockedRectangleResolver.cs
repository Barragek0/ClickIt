namespace ClickIt.Features.Area
{
    internal enum AreaBlockedRectangleKind
    {
        ChatPanel,
        MapPanel,
        XpBar,
        Mirage,
        Altar,
        Ritual,
        Sentinel,
    }

    internal static class AreaBlockedRectangleResolver
    {
        // One data table for every blocked rectangle: the IngameUi root property + child path + whether the leaf must be visible (Mirage/Altar/Ritual hide when the widget is off).
        private static readonly (string RootProperty, int[] Path, bool RequireVisible)[] ByKind =
        [
            ("ChatPanel", [1, 2, 2], false),
            ("Map", [2, 1], false),
            ("GameUI", [0], false),
            ("GameUI", [7, 17], true),
            ("GameUI", [7, 16], true),
            ("GameUI", [7, 18, 0], true),
            ("GameUI", [7, 18, 2, 0], false),
        ];

        internal static RectangleF ResolveBlockedRectangle(GameController gameController, AreaBlockedRectangleKind kind)
        {
            (string rootProperty, _, _) = ByKind[(int)kind];
            return ResolveBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, rootProperty), kind);
        }

        internal static RectangleF ResolveBlockedRectangleFromRoot(object? root, AreaBlockedRectangleKind kind)
        {
            (_, int[] path, bool requireVisible) = ByKind[(int)kind];
            return requireVisible
                ? AreaUiNodeTraversal.ResolveVisibleRectangleFromNodePath(root, path)
                : AreaUiNodeTraversal.ResolveRectangleFromNodePath(root, path);
        }
    }
}