namespace ClickIt.Core.Runtime
{
    public sealed class PluginRenderingState
    {
        internal PluginRenderingState()
        {
        }

        public StrongboxRenderer? StrongboxRenderer { get; set; }
        public UltimatumRenderer? UltimatumRenderer { get; set; }
        public LazyModeRenderer? LazyModeRenderer { get; set; }
        public ClickHotkeyToggleRenderer? ClickHotkeyToggleRenderer { get; set; }
        internal InventoryFullWarningRenderer? InventoryFullWarningRenderer { get; set; }
        public PathfindingRenderer? PathfindingRenderer { get; set; }
        public AltarDisplayRenderer? AltarDisplayRenderer { get; set; }
        public HarvestOverlayRenderer? HarvestOverlayRenderer { get; set; }
        public BlightRenderer? BlightRenderer { get; set; }
        internal ImGuiDebugOverlay? ImGuiDebugOverlay { get; set; }
        internal UiRegionRectangleOverlay? UiRegionRectangleOverlay { get; set; }
        public DeferredTextQueue? DeferredTextQueue { get; set; }
        public DeferredFrameQueue? DeferredFrameQueue { get; set; }
        public bool IsRendering { get; set; }

        internal void Clear()
        {
            StrongboxRenderer = null;
            UltimatumRenderer = null;
            LazyModeRenderer = null;
            ClickHotkeyToggleRenderer = null;
            InventoryFullWarningRenderer = null;
            PathfindingRenderer = null;
            AltarDisplayRenderer = null;
            HarvestOverlayRenderer = null;
            UiRegionRectangleOverlay = null;
            DeferredTextQueue = null;
            DeferredFrameQueue = null;
            IsRendering = false;
        }
    }
}