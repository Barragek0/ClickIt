namespace ClickIt.Core.Runtime
{
    public sealed class PluginRenderingState
    {
        public Rendering.DebugRenderer? DebugRenderer { get; set; }
        public Rendering.StrongboxRenderer? StrongboxRenderer { get; set; }
        public Rendering.UltimatumRenderer? UltimatumRenderer { get; set; }
        public Rendering.LazyModeRenderer? LazyModeRenderer { get; set; }
        public Rendering.ClickHotkeyToggleRenderer? ClickHotkeyToggleRenderer { get; set; }
        internal Rendering.InventoryFullWarningRenderer? InventoryFullWarningRenderer { get; set; }
        public Rendering.PathfindingRenderer? PathfindingRenderer { get; set; }
        public Rendering.AltarDisplayRenderer? AltarDisplayRenderer { get; set; }
        public Utils.DeferredTextQueue? DeferredTextQueue { get; set; }
        public Utils.DeferredFrameQueue? DeferredFrameQueue { get; set; }
        public ClickRuntimeHost? ClickRuntimeHost { get; set; }
        public bool IsRendering { get; set; }
    }
}