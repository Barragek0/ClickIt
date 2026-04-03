namespace ClickIt.Core.Runtime
{
    public sealed class PluginRenderingState
    {
        private readonly PluginOverlayPorts _ports;

        internal PluginRenderingState(PluginOverlayPorts ports)
        {
            _ports = ports ?? throw new ArgumentNullException(nameof(ports));
        }

        public DebugRenderer? DebugRenderer { get => _ports.DebugRenderer; set => _ports.DebugRenderer = value; }
        public StrongboxRenderer? StrongboxRenderer { get => _ports.StrongboxRenderer; set => _ports.StrongboxRenderer = value; }
        public UltimatumRenderer? UltimatumRenderer { get => _ports.UltimatumRenderer; set => _ports.UltimatumRenderer = value; }
        public LazyModeRenderer? LazyModeRenderer { get => _ports.LazyModeRenderer; set => _ports.LazyModeRenderer = value; }
        public ClickHotkeyToggleRenderer? ClickHotkeyToggleRenderer { get => _ports.ClickHotkeyToggleRenderer; set => _ports.ClickHotkeyToggleRenderer = value; }
        internal InventoryFullWarningRenderer? InventoryFullWarningRenderer { get => _ports.InventoryFullWarningRenderer; set => _ports.InventoryFullWarningRenderer = value; }
        public PathfindingRenderer? PathfindingRenderer { get => _ports.PathfindingRenderer; set => _ports.PathfindingRenderer = value; }
        public AltarDisplayRenderer? AltarDisplayRenderer { get => _ports.AltarDisplayRenderer; set => _ports.AltarDisplayRenderer = value; }
        public DeferredTextQueue? DeferredTextQueue { get => _ports.DeferredTextQueue; set => _ports.DeferredTextQueue = value; }
        public DeferredFrameQueue? DeferredFrameQueue { get => _ports.DeferredFrameQueue; set => _ports.DeferredFrameQueue = value; }
        public ClickRuntimeHost? ClickRuntimeHost { get => _ports.ClickRuntimeHost; set => _ports.ClickRuntimeHost = value; }
        public bool IsRendering { get => _ports.IsRendering; set => _ports.IsRendering = value; }
    }
}