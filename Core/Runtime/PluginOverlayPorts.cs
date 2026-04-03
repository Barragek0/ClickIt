namespace ClickIt.Core.Runtime
{
    internal sealed class PluginOverlayPorts
    {
        internal DebugRenderer? DebugRenderer { get; set; }
        internal StrongboxRenderer? StrongboxRenderer { get; set; }
        internal UltimatumRenderer? UltimatumRenderer { get; set; }
        internal LazyModeRenderer? LazyModeRenderer { get; set; }
        internal ClickHotkeyToggleRenderer? ClickHotkeyToggleRenderer { get; set; }
        internal InventoryFullWarningRenderer? InventoryFullWarningRenderer { get; set; }
        internal PathfindingRenderer? PathfindingRenderer { get; set; }
        internal AltarDisplayRenderer? AltarDisplayRenderer { get; set; }
        internal DeferredTextQueue? DeferredTextQueue { get; set; }
        internal DeferredFrameQueue? DeferredFrameQueue { get; set; }
        internal ClickRuntimeHost? ClickRuntimeHost { get; set; }
        internal bool IsRendering { get; set; }

        internal void Clear()
        {
            DebugRenderer = null;
            StrongboxRenderer = null;
            UltimatumRenderer = null;
            LazyModeRenderer = null;
            ClickHotkeyToggleRenderer = null;
            InventoryFullWarningRenderer = null;
            PathfindingRenderer = null;
            AltarDisplayRenderer = null;
            DeferredTextQueue = null;
            DeferredFrameQueue = null;
            ClickRuntimeHost = null;
            IsRendering = false;
        }
    }
}