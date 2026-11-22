namespace ClickIt.Features.Area
{
    internal enum AreaBlockedRectangleKind
    {
        ChatPanel,
        MapPanel,
        XpBar,
        Buttons,
        Ritual,
        Sentinel,
        Expedition,
    }

    internal static class AreaBlockedRectangleResolver
    {
        // Labels, API root properties, and child paths stay together so each mapping remains identifiable.
        private static readonly (string Label, string RootProperty, int[] Path, bool RequireVisible)[] ByKind =
        [
            ("Chat", "ChatPanel", [1, 2, 2], false),
            ("Map", "Map", [2, 1], false),
            ("XP Bar", "GameUI", [0], false),
            ("Buttons", "GameUI", [7, 17], true),
            ("Ritual", "GameUI", [7, 18, 0], true),
            ("Sentinel", "GameUI", [7, 18, 2, 0], false),
            ("Expedition", "GameUI", [7, 18, 1], true),
        ];

        internal static RectangleF ResolveBlockedRectangle(GameController gameController, AreaBlockedRectangleKind kind)
        {
            (_, string rootProperty, _, _) = ByKind[(int)kind];
            return ResolveBlockedRectangleFromRoot(AreaUiSnapshotReader.TryGetIngameUiProperty(gameController, rootProperty), kind);
        }

        internal static RectangleF ResolveBlockedRectangleFromRoot(object? root, AreaBlockedRectangleKind kind)
        {
            (_, _, int[] path, bool requireVisible) = ByKind[(int)kind];
            return requireVisible
                ? AreaUiNodeTraversal.ResolveVisibleRectangleFromNodePath(root, path)
                : AreaUiNodeTraversal.ResolveRectangleFromNodePath(root, path);
        }

        internal static string GetLabel(AreaBlockedRectangleKind kind)
            => ByKind[(int)kind].Label;
    }
}
