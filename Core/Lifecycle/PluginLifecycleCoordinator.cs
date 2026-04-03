namespace ClickIt.Core.Lifecycle
{
    internal static class PluginLifecycleCoordinator
    {
        public static bool Initialise(ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            owner.State.Runtime.IsShuttingDown = false;
            owner.LifecycleButtonBindings.Subscribe(settings);
            owner.State.InitializeCompositionRoot(owner, settings);

            var errorHandler = owner.State.Services.ErrorHandler
                ?? throw new InvalidOperationException("ErrorHandler was not initialized by composition root.");
            var gameController = owner.GameController
                ?? throw new InvalidOperationException("GameController is null during coroutine manager initialization.");

            errorHandler.RegisterGlobalExceptionHandlers();

            var coroutineManager = new PluginLoopHost(
                owner.State,
                settings,
                gameController,
                errorHandler);
            coroutineManager.StartCoroutines(owner);

            owner.State.FinalizeCompositionRootForStartup(owner, settings);
            return true;
        }

        public static void Shutdown(ClickIt owner, ClickItSettings runtimeSettings)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(runtimeSettings);

            owner.State.Runtime.IsShuttingDown = true;
            owner.LifecycleButtonBindings.Unsubscribe(runtimeSettings, owner.GetEffectiveSettingsForLifecycle());

            StopTrackedCoroutines(owner.State);
            StopNamedClickItCoroutines();
            WaitForTrackedCoroutines(owner.State);
            WaitForNamedClickItCoroutinesShutdown();
            ClearTrackedCoroutineReferences(owner.State);

            LockManager.Instance = null;

            LabelUtils.ClearThreadLocalStorage();
            ShrineService.ClearThreadLocalStorageForCurrentThread();
            ClickService.ClearThreadLocalStorageForCurrentThread();
            owner.State.Services.LabelFilterPort?.ClearInventoryProbeCacheForShutdown();
            owner.State.Services.AltarService?.ClearRuntimeCaches();

            owner.State.DisposeCompositionRoot();

            DrainFinalizers();
        }

        private static void StopTrackedCoroutines(PluginContext state)
        {
            PluginRuntimeState runtime = state.Runtime;
            runtime.AltarCoroutine?.Done();
            runtime.ClickLabelCoroutine?.Done();
            runtime.ManualUiHoverCoroutine?.Done();
            runtime.DelveFlareCoroutine?.Done();
            runtime.DeepMemoryDumpCoroutine?.Done();
        }

        private static void WaitForTrackedCoroutines(PluginContext state)
        {
            PluginRuntimeState runtime = state.Runtime;
            WaitForCoroutineShutdown(runtime.AltarCoroutine);
            WaitForCoroutineShutdown(runtime.ClickLabelCoroutine);
            WaitForCoroutineShutdown(runtime.ManualUiHoverCoroutine);
            WaitForCoroutineShutdown(runtime.DelveFlareCoroutine);
            WaitForCoroutineShutdown(runtime.DeepMemoryDumpCoroutine);
        }

        private static void ClearTrackedCoroutineReferences(PluginContext state)
        {
            PluginRuntimeState runtime = state.Runtime;
            runtime.AltarCoroutine = null;
            runtime.ClickLabelCoroutine = null;
            runtime.ManualUiHoverCoroutine = null;
            runtime.DelveFlareCoroutine = null;
            runtime.DeepMemoryDumpCoroutine = null;
        }

        private static void WaitForCoroutineShutdown(Coroutine? coroutine, int timeoutMs = 750)
        {
            if (coroutine == null)
                return;

            var stopwatch = Stopwatch.StartNew();
            while (!coroutine.IsDone && stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(10);
            }
        }

        private static void StopNamedClickItCoroutines()
        {
            try
            {
                var coroutines = ExileCoreApi.ParallelRunner.Coroutines
                    .Where(c => c != null && c.Name != null && c.Name.StartsWith("ClickIt.", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var coroutine in coroutines)
                {
                    coroutine.Done();
                }
            }
            catch
            {
                // Best effort cleanup during shutdown.
            }
        }

        private static void WaitForNamedClickItCoroutinesShutdown(int timeoutMs = 2000)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.ElapsedMilliseconds < timeoutMs)
                {
                    bool anyActive = ExileCoreApi.ParallelRunner.Coroutines
                        .Any(c => c != null
                            && c.Name != null
                            && c.Name.StartsWith("ClickIt.", StringComparison.OrdinalIgnoreCase)
                            && !c.IsDone);

                    if (!anyActive)
                        break;

                    Thread.Sleep(10);
                }
            }
            catch
            {
                // Best effort cleanup during shutdown.
            }
        }

        private static void DrainFinalizers()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(50);
            }
            catch
            {
                // Best effort only.
            }
        }
    }
}