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
        private readonly object _debugTelemetryFreezeLock = new();
        private DebugTelemetrySnapshot _frozenDebugTelemetrySnapshot = DebugTelemetrySnapshot.Empty;
        private long _debugTelemetryFreezeUntilTimestampMs;
        private string _debugTelemetryFreezeReason = string.Empty;

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
            lock (_debugTelemetryFreezeLock)
            {
                long remainingMs = _debugTelemetryFreezeUntilTimestampMs - Environment.TickCount64;
                if (remainingMs > 0)
                    return _frozenDebugTelemetrySnapshot;

                ClearDebugTelemetryFreezeUnsafe();
            }

            return GetLiveDebugTelemetrySnapshot();
        }

        internal void FreezeDebugTelemetrySnapshot(string reason, int holdDurationMs)
        {
            int durationMs = Math.Max(0, holdDurationMs);
            if (durationMs <= 0)
                return;

            DebugTelemetrySnapshot snapshot = GetLiveDebugTelemetrySnapshot();
            long now = Environment.TickCount64;

            lock (_debugTelemetryFreezeLock)
            {
                _frozenDebugTelemetrySnapshot = snapshot;
                _debugTelemetryFreezeUntilTimestampMs = now + durationMs;
                _debugTelemetryFreezeReason = reason ?? string.Empty;
            }
        }

        internal bool TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason)
        {
            lock (_debugTelemetryFreezeLock)
            {
                remainingMs = _debugTelemetryFreezeUntilTimestampMs - Environment.TickCount64;
                if (remainingMs > 0)
                {
                    reason = _debugTelemetryFreezeReason;
                    return true;
                }

                ClearDebugTelemetryFreezeUnsafe();
            }

            remainingMs = 0;
            reason = string.Empty;
            return false;
        }

        private DebugTelemetrySnapshot GetLiveDebugTelemetrySnapshot()
            => DebugTelemetryProjection.Build(ClickService, LabelFilterService, PathfindingService);

        private void ClearDebugTelemetryFreezeUnsafe()
        {
            _frozenDebugTelemetrySnapshot = DebugTelemetrySnapshot.Empty;
            _debugTelemetryFreezeUntilTimestampMs = 0;
            _debugTelemetryFreezeReason = string.Empty;
        }

        public void InitializeCompositionRoot(ClickIt owner, ClickItSettings settings)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            _serviceRegistry.Reset();
            IsShuttingDown = false;
            lock (_debugTelemetryFreezeLock)
            {
                ClearDebugTelemetryFreezeUnsafe();
            }

            ComposedServices services = ServiceCompositionRoot.Compose(owner, settings);

            PerformanceMonitor = services.PerformanceMonitor;
            ErrorHandler = services.ErrorHandler;
            AreaService = services.AreaService;
            LabelService = services.LabelService;
            CachedLabels = services.CachedLabels;
            Camera = services.Camera;
            AltarService = services.AltarService;
            LabelFilterService = services.LabelFilterService;
            ShrineService = services.ShrineService;
            InputHandler = services.InputHandler;
            PathfindingService = services.PathfindingService;
            DeferredTextQueue = services.DeferredTextQueue;
            DeferredFrameQueue = services.DeferredFrameQueue;
            DebugRenderer = services.DebugRenderer;
            StrongboxRenderer = services.StrongboxRenderer;
            LazyModeRenderer = services.LazyModeRenderer;
            ClickHotkeyToggleRenderer = services.ClickHotkeyToggleRenderer;
            InventoryFullWarningRenderer = services.InventoryFullWarningRenderer;
            PathfindingRenderer = services.PathfindingRenderer;
            AltarDisplayRenderer = services.AltarDisplayRenderer;
            ClickService = services.ClickService;
            ClickRuntimeHost = new ClickRuntimeHost(() => ClickService);
            UltimatumRenderer = services.UltimatumRenderer;
            AlertService = services.AlertService;

            ServiceCompositionRoot.WireSettingsActions(settings, services.EffectiveSettings, services.AlertService, _serviceRegistry);
            _serviceRegistry.Register(() => ErrorHandler?.UnregisterGlobalExceptionHandlers());
            _serviceRegistry.Register(() => PerformanceMonitor?.ShutdownForHotReload());
            _serviceRegistry.Register(() => LastRenderTimer.Stop());
            _serviceRegistry.Register(() => LastTickTimer.Stop());
            _serviceRegistry.Register(() => Timer.Stop());
            _serviceRegistry.Register(() => SecondTimer.Stop());
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

            LastRenderTimer.Start();
            LastTickTimer.Start();
            Timer.Start();
            SecondTimer.Start();
        }

        public void DisposeCompositionRoot()
        {
            _serviceRegistry.DisposeAll();
            lock (_debugTelemetryFreezeLock)
            {
                ClearDebugTelemetryFreezeUnsafe();
            }

            AreaService = null;
            AltarService = null;
            ShrineService = null;
            InputHandler = null;
            DebugRenderer = null;
            StrongboxRenderer = null;
            UltimatumRenderer = null;
            LazyModeRenderer = null;
            ClickHotkeyToggleRenderer = null;
            InventoryFullWarningRenderer = null;
            PathfindingRenderer = null;
            DeferredTextQueue = null;
            DeferredFrameQueue = null;
            AltarDisplayRenderer = null;
            PathfindingService = null;
            AlertService = null;
            LabelService = null;
            LabelFilterService = null;
            ClickService = null;
            ClickRuntimeHost = null;
            Camera = null;
            PerformanceMonitor = null;
            ErrorHandler = null;
            CachedLabels = null;
        }
    }
}