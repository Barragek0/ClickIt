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

        public Utils.PerformanceMonitor? PerformanceMonitor { get; set; }
        public Utils.ErrorHandler? ErrorHandler { get; set; }
        public Random Random { get; } = new Random();
        public TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }
        public Coroutine? AltarCoroutine { get; set; }
        public Coroutine? ClickLabelCoroutine { get; set; }
        public Coroutine? ManualUiHoverCoroutine { get; set; }
        public Coroutine? DelveFlareCoroutine { get; set; }
        public Coroutine? DeepMemoryDumpCoroutine { get; set; }
        public Stopwatch LastRenderTimer { get; } = new Stopwatch();
        public Stopwatch LastTickTimer { get; } = new Stopwatch();
        public Stopwatch Timer { get; } = new Stopwatch();
        public Stopwatch SecondTimer { get; } = new Stopwatch();
        public bool LastHotkeyState { get; set; } = false;
        public Services.AreaService? AreaService { get; set; }
        public Services.AltarService? AltarService { get; set; }
        public Services.ShrineService? ShrineService { get; set; }
        public Utils.InputHandler? InputHandler { get; set; }
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
        public Services.LabelFilterService? LabelFilterService { get; set; }
        public Services.LabelService? LabelService { get; set; }
        public Services.ClickService? ClickService { get; set; }
        internal ClickRuntimeHost? ClickRuntimeHost { get; set; }
        public Services.PathfindingService? PathfindingService { get; set; }
        public Services.AlertService? AlertService { get; set; }
        public Camera? Camera { get; set; }
        public bool WorkFinished { get; set; } = false;
        public bool IsShuttingDown { get; set; } = false;

        /// <summary>
        /// Indicates whether we're currently in the Render() method.
        /// Used to prevent logging that could cause recursive rendering issues.
        /// </summary>
        public bool IsRendering { get; set; } = false;

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
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            _serviceRegistry.Reset();
            IsShuttingDown = false;
            _debugTelemetryFreezeState.Clear();

            ComposedServices services = ServiceCompositionRoot.Compose(owner, settings);

            PluginContextServiceStateInitializer.InitializeFromComposedServices(this, services);

            ServiceCompositionRoot.WireSettingsActions(settings, services.EffectiveSettings, services.AlertService, _serviceRegistry);
            _serviceRegistry.Register(() => ErrorHandler?.UnregisterGlobalExceptionHandlers());
            _serviceRegistry.Register(() => PerformanceMonitor?.ShutdownForHotReload());
            _serviceRegistry.Register(() => PluginRuntimeTimerCoordinator.StopAll(LastRenderTimer, LastTickTimer, Timer, SecondTimer));
        }

        public void FinalizeCompositionRootForStartup(ClickIt owner, ClickItSettings settings)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            settings.EnsureAllModsHaveWeights();

            AlertService?.ReloadAlertSound();
            PerformanceMonitor?.Start();

            PluginRuntimeTimerCoordinator.StartAll(LastRenderTimer, LastTickTimer, Timer, SecondTimer);
        }

        public void DisposeCompositionRoot()
        {
            _serviceRegistry.DisposeAll();
            _debugTelemetryFreezeState.Clear();

            PluginContextServiceStateResetter.Reset(this);
        }
    }
}