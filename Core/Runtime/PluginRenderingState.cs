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
        public DeferredTextQueue? DeferredTextQueue { get; set; }
        public DeferredFrameQueue? DeferredFrameQueue { get; set; }
        public DeferredDrawQueue? DeferredDrawQueue { get; set; }
        public bool IsRendering { get; set; }

        internal void Clear()
        {
            UiRegionRectangleOverlay = null;
            OverlayRenderHost = null;
            DeferredTextQueue = null;
            DeferredFrameQueue = null;
            DeferredDrawQueue = null;
            IsRendering = false;
        }
    }
}