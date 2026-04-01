using ClickIt.Utils;
using ExileCore;
using ExileCore.Shared;
using System.Diagnostics;
using System.Threading;

namespace ClickIt
{
    internal static class PluginLifecycleCoordinator
    {
        public static bool Initialise(ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            owner.State.IsShuttingDown = false;
            owner.SubscribeLifecycleButtonHandlers(settings);
            owner.State.InitializeCompositionRoot(owner, settings);

            var errorHandler = owner.State.ErrorHandler
                ?? throw new InvalidOperationException("ErrorHandler was not initialized by composition root.");
            var gameController = owner.GameController
                ?? throw new InvalidOperationException("GameController is null during coroutine manager initialization.");

            errorHandler.RegisterGlobalExceptionHandlers();

            var coroutineManager = new CoroutineManager(
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

            owner.State.IsShuttingDown = true;
            owner.UnsubscribeLifecycleButtonHandlers(runtimeSettings);

            StopTrackedCoroutines(owner.State);
            StopNamedClickItCoroutines();
            WaitForTrackedCoroutines(owner.State);
            WaitForNamedClickItCoroutinesShutdown();
            ClearTrackedCoroutineReferences(owner.State);

            LockManager.Instance = null;

            LabelUtils.ClearThreadLocalStorage();
            Services.ShrineService.ClearThreadLocalStorageForCurrentThread();
            Services.ClickService.ClearThreadLocalStorageForCurrentThread();
            owner.State.LabelFilterService?.ClearInventoryProbeCacheForShutdown();
            owner.State.AltarService?.ClearRuntimeCaches();

            owner.State.DisposeCompositionRoot();

            DrainFinalizers();
        }

        private static void StopTrackedCoroutines(PluginContext state)
        {
            state.AltarCoroutine?.Done();
            state.ClickLabelCoroutine?.Done();
            state.ManualUiHoverCoroutine?.Done();
            state.DelveFlareCoroutine?.Done();
            state.DeepMemoryDumpCoroutine?.Done();
        }

        private static void WaitForTrackedCoroutines(PluginContext state)
        {
            WaitForCoroutineShutdown(state.AltarCoroutine);
            WaitForCoroutineShutdown(state.ClickLabelCoroutine);
            WaitForCoroutineShutdown(state.ManualUiHoverCoroutine);
            WaitForCoroutineShutdown(state.DelveFlareCoroutine);
            WaitForCoroutineShutdown(state.DeepMemoryDumpCoroutine);
        }

        private static void ClearTrackedCoroutineReferences(PluginContext state)
        {
            state.AltarCoroutine = null;
            state.ClickLabelCoroutine = null;
            state.ManualUiHoverCoroutine = null;
            state.DelveFlareCoroutine = null;
            state.DeepMemoryDumpCoroutine = null;
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
                var coroutines = global::ExileCore.Core.ParallelRunner.Coroutines
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
                    bool anyActive = global::ExileCore.Core.ParallelRunner.Coroutines
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