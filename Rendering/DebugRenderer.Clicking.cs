namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        public int RenderRuntimeDebugLogOverlay(int xPos, int yPos, int lineHeight)
        {
            return RenderRuntimeDebugLogOverlay(ref xPos, yPos, lineHeight);
        }

        private int RenderRuntimeDebugLogOverlay(ref int xPos, int yPos, int lineHeight)
            => _clickingDebugOverlaySection.RenderRuntimeDebugLogOverlay(ref xPos, yPos, lineHeight);

        public int RenderClickingDebug(int xPos, int yPos, int lineHeight)
        {
            return RenderClickingDebug(ref xPos, yPos, lineHeight);
        }

        private int RenderClickingDebug(ref int xPos, int yPos, int lineHeight)
            => _clickingDebugOverlaySection.RenderClickingDebug(ref xPos, yPos, lineHeight);

        internal static IReadOnlyList<string> BuildClickSettingsDebugSnapshotLines(ClickItSettings settings)
            => Debug.Sections.ClickingDebugOverlaySection.BuildClickSettingsDebugSnapshotLines(settings);
    }
}
