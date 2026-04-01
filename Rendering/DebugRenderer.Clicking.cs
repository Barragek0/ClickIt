namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        public int RenderRuntimeDebugLogOverlay(int xPos, int yPos, int lineHeight)
        {
            int localX = xPos;
            return _clickingDebugOverlaySection.RenderRuntimeDebugLogOverlay(ref localX, yPos, lineHeight);
        }

        public int RenderClickingDebug(int xPos, int yPos, int lineHeight)
        {
            int localX = xPos;
            return _clickingDebugOverlaySection.RenderClickingDebug(ref localX, yPos, lineHeight);
        }

        internal static IReadOnlyList<string> BuildClickSettingsDebugSnapshotLines(ClickItSettings settings)
            => Debug.Sections.ClickingDebugOverlaySection.BuildClickSettingsDebugSnapshotLines(settings);
    }
}
