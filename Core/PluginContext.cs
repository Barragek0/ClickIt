using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using System.Diagnostics;
using ClickIt.Services;
using ClickIt.Services.Observability;
using ClickIt.Services.Observability.TelemetryProjection;
using ClickIt.Composition;
using ClickIt.Core.Runtime;
using ClickIt.Utils;

namespace ClickIt
{
    public class PluginContext
    {
        private readonly ServiceDisposalRegistry _serviceRegistry = new();
        private readonly DebugTelemetryFreezeState _debugTelemetryFreezeState = new();
        private readonly PluginServices _services = new();
        private readonly PluginRuntimeState _runtime = new();
        private readonly PluginRenderingState _rendering = new();

        internal ServiceDisposalRegistry ServiceRegistry => _serviceRegistry;
        internal DebugTelemetryFreezeState DebugTelemetryFreezeState => _debugTelemetryFreezeState;

        internal PluginServices Services => _services;
        internal PluginRuntimeState Runtime => _runtime;
        internal PluginRenderingState Rendering => _rendering;

        public Utils.PerformanceMonitor? PerformanceMonitor { get => _services.PerformanceMonitor; set => _services.PerformanceMonitor = value; }
        public Utils.ErrorHandler? ErrorHandler { get => _services.ErrorHandler; set => _services.ErrorHandler = value; }
        public Random Random { get; } = new Random();
        public TimeCache<List<LabelOnGround>>? CachedLabels { get => _services.CachedLabels; set => _services.CachedLabels = value; }
        public Coroutine? AltarCoroutine { get => _runtime.AltarCoroutine; set => _runtime.AltarCoroutine = value; }
        public Coroutine? ClickLabelCoroutine { get => _runtime.ClickLabelCoroutine; set => _runtime.ClickLabelCoroutine = value; }
        public Coroutine? ManualUiHoverCoroutine { get => _runtime.ManualUiHoverCoroutine; set => _runtime.ManualUiHoverCoroutine = value; }
        public Coroutine? DelveFlareCoroutine { get => _runtime.DelveFlareCoroutine; set => _runtime.DelveFlareCoroutine = value; }
        public Coroutine? DeepMemoryDumpCoroutine { get => _runtime.DeepMemoryDumpCoroutine; set => _runtime.DeepMemoryDumpCoroutine = value; }
        public Stopwatch LastRenderTimer => _runtime.LastRenderTimer;
        public Stopwatch LastTickTimer => _runtime.LastTickTimer;
        public Stopwatch Timer => _runtime.Timer;
        public Stopwatch SecondTimer => _runtime.SecondTimer;
        public bool LastHotkeyState { get => _runtime.LastHotkeyState; set => _runtime.LastHotkeyState = value; }
        public Services.AreaService? AreaService { get => _services.AreaService; set => _services.AreaService = value; }
        public Services.AltarService? AltarService { get => _services.AltarService; set => _services.AltarService = value; }
        public Services.ShrineService? ShrineService { get => _services.ShrineService; set => _services.ShrineService = value; }
        public Utils.InputHandler? InputHandler { get => _services.InputHandler; set => _services.InputHandler = value; }
        public Rendering.DebugRenderer? DebugRenderer { get => _rendering.DebugRenderer; set => _rendering.DebugRenderer = value; }
        public Rendering.StrongboxRenderer? StrongboxRenderer { get => _rendering.StrongboxRenderer; set => _rendering.StrongboxRenderer = value; }
        public Rendering.UltimatumRenderer? UltimatumRenderer { get => _rendering.UltimatumRenderer; set => _rendering.UltimatumRenderer = value; }
        public Rendering.LazyModeRenderer? LazyModeRenderer { get => _rendering.LazyModeRenderer; set => _rendering.LazyModeRenderer = value; }
        public Rendering.ClickHotkeyToggleRenderer? ClickHotkeyToggleRenderer { get => _rendering.ClickHotkeyToggleRenderer; set => _rendering.ClickHotkeyToggleRenderer = value; }
        internal Rendering.InventoryFullWarningRenderer? InventoryFullWarningRenderer { get => _rendering.InventoryFullWarningRenderer; set => _rendering.InventoryFullWarningRenderer = value; }
        public Rendering.PathfindingRenderer? PathfindingRenderer { get => _rendering.PathfindingRenderer; set => _rendering.PathfindingRenderer = value; }
        public Rendering.AltarDisplayRenderer? AltarDisplayRenderer { get => _rendering.AltarDisplayRenderer; set => _rendering.AltarDisplayRenderer = value; }
        public Utils.DeferredTextQueue? DeferredTextQueue { get => _rendering.DeferredTextQueue; set => _rendering.DeferredTextQueue = value; }
        public Utils.DeferredFrameQueue? DeferredFrameQueue { get => _rendering.DeferredFrameQueue; set => _rendering.DeferredFrameQueue = value; }
        public Services.LabelFilterService? LabelFilterService { get => _services.LabelFilterService; set => _services.LabelFilterService = value; }
        public Services.LabelService? LabelService { get => _services.LabelService; set => _services.LabelService = value; }
        public Services.ClickService? ClickService { get => _services.ClickService; set => _services.ClickService = value; }
        internal ClickRuntimeHost? ClickRuntimeHost { get => _rendering.ClickRuntimeHost; set => _rendering.ClickRuntimeHost = value; }
        public Services.PathfindingService? PathfindingService { get => _services.PathfindingService; set => _services.PathfindingService = value; }
        public Services.AlertService? AlertService { get => _services.AlertService; set => _services.AlertService = value; }
        public Camera? Camera { get => _services.Camera; set => _services.Camera = value; }
        public bool WorkFinished { get => _runtime.WorkFinished; set => _runtime.WorkFinished = value; }
        public bool IsShuttingDown { get => _runtime.IsShuttingDown; set => _runtime.IsShuttingDown = value; }

        /// <summary>
        /// Indicates whether we're currently in the Render() method.
        /// Used to prevent logging that could cause recursive rendering issues.
        /// </summary>
        public bool IsRendering { get => _rendering.IsRendering; set => _rendering.IsRendering = value; }

        public double CurrentFPS => PerformanceMonitor?.CurrentFPS ?? 0;

        public Queue<long> RenderTimings => PerformanceMonitor?.GetRenderTimingsSnapshot() ?? new Queue<long>();

        public IReadOnlyList<string> RecentErrors => ErrorHandler?.RecentErrors ?? [];

        internal DebugTelemetrySnapshot GetDebugTelemetrySnapshot()
        {
            if (_debugTelemetryFreezeState.TryGetFrozenSnapshot(Environment.TickCount64, out DebugTelemetrySnapshot frozenSnapshot))
                return frozenSnapshot;

            return GetLiveDebugTelemetrySnapshot();
        }

        internal void FreezeDebugTelemetrySnapshot(string reason, int holdDurationMs)
        {
            DebugTelemetrySnapshot snapshot = GetLiveDebugTelemetrySnapshot();
            long now = Environment.TickCount64;
            _debugTelemetryFreezeState.Freeze(snapshot, reason, holdDurationMs, now);
        }

        internal bool TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason)
            => _debugTelemetryFreezeState.TryGetFreezeState(Environment.TickCount64, out remainingMs, out reason);

        private DebugTelemetrySnapshot GetLiveDebugTelemetrySnapshot()
            => DebugTelemetryProjection.Build(ClickService, LabelFilterService, PathfindingService);

        public void InitializeCompositionRoot(ClickIt owner, ClickItSettings settings)
            => PluginLifecycleHost.InitializeCompositionRoot(this, owner, settings);

        public void FinalizeCompositionRootForStartup(ClickIt owner, ClickItSettings settings)
            => PluginLifecycleHost.FinalizeCompositionRootForStartup(this, owner, settings);

        public void DisposeCompositionRoot()
            => PluginLifecycleHost.DisposeCompositionRoot(this);
    }
}