namespace ClickIt.Features.Area
{
    internal enum AreaBlockedRectangleKind
    {
        ChatPanel,
        MapPanel,
        XpBar,
        Delve,
    }

    internal static class AreaBlockedRectangleResolver
    {
        // Labels, API root properties, and child paths stay together so each mapping remains identifiable.
        private static readonly (string Label, string RootProperty, int[] Path, bool RequireVisible)[] ByKind =
        [
            ("Chat", "ChatPanel", [1, 2, 2], false),
            ("Map", "Map", [2, 1], false),
            ("XP Bar", "GameUI", [0], false),
            ("Delve", "SkillBar.Skills", [13, 0], false),
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
