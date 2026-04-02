using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using System.Diagnostics;
using ClickIt.Services;
using ClickIt.Services.Observability;
using ClickIt.Composition;
using ClickIt.Core.Runtime;
using ClickIt.Utils;

namespace ClickIt
{
    public class PluginContext
    {
        private readonly ServiceDisposalRegistry _serviceRegistry = new();
        private readonly PluginServices _services = new();
        private readonly PluginRuntimeState _runtime = new();
        private readonly PluginRenderingState _rendering = new();
        private readonly PluginDebugTelemetryService _debugTelemetry;

        public PluginContext()
        {
            _debugTelemetry = new PluginDebugTelemetryService(
                () => _services.ClickService,
                () => _services.LabelFilterService,
                () => _services.PathfindingService);
        }

        internal ServiceDisposalRegistry ServiceRegistry => _serviceRegistry;
        internal PluginDebugTelemetryService DebugTelemetry => _debugTelemetry;

        public PluginServices Services => _services;
        public PluginRuntimeState Runtime => _runtime;
        public PluginRenderingState Rendering => _rendering;

        public Utils.ErrorHandler? ErrorHandler { get => _services.ErrorHandler; set => _services.ErrorHandler = value; }
        public Random Random { get; } = new Random();
        public double CurrentFPS => _services.PerformanceMonitor?.CurrentFPS ?? 0;

        public Queue<long> RenderTimings => _services.PerformanceMonitor?.GetRenderTimingsSnapshot() ?? new Queue<long>();

        public IReadOnlyList<string> RecentErrors => ErrorHandler?.RecentErrors ?? [];

        internal DebugTelemetrySnapshot GetDebugTelemetrySnapshot()
            => _debugTelemetry.GetSnapshot();

        internal void FreezeDebugTelemetrySnapshot(string reason, int holdDurationMs)
            => _debugTelemetry.FreezeSnapshot(reason, holdDurationMs);

        internal bool TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason)
            => _debugTelemetry.TryGetFreezeState(out remainingMs, out reason);

        public void InitializeCompositionRoot(ClickIt owner, ClickItSettings settings)
            => PluginLifecycleHost.InitializeCompositionRoot(this, owner, settings);

        public void FinalizeCompositionRootForStartup(ClickIt owner, ClickItSettings settings)
            => PluginLifecycleHost.FinalizeCompositionRootForStartup(this, owner, settings);

        public void DisposeCompositionRoot()
            => PluginLifecycleHost.DisposeCompositionRoot(this);
    }
}