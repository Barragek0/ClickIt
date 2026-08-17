namespace ClickIt.Core.Runtime
{
    public sealed class PluginRenderingState
    {
        internal PluginRenderingState()
        {
        }

        internal ImGuiDebugOverlay? ImGuiDebugOverlay { get; set; }
        internal UiRegionRectangleOverlay? UiRegionRectangleOverlay { get; set; }
        public OverlayRenderHost? OverlayRenderHost { get; set; }
        public DeferredDrawQueue? DeferredDrawQueue { get; set; }
        public bool IsRendering { get; set; }

        internal void Clear()
        {
            ImGuiDebugOverlay = null;
            UiRegionRectangleOverlay = null;
            OverlayRenderHost = null;
            DeferredDrawQueue = null;
            IsRendering = false;
        }
    }
}