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

            ErrorHandler errorHandler = owner.State.Services.ErrorHandler
                ?? throw new InvalidOperationException("ErrorHandler was not initialized by composition root.");
            GameController gameController = owner.GameController
                ?? throw new InvalidOperationException("GameController is null during coroutine manager initialization.");

            errorHandler.RegisterGlobalExceptionHandlers();

            PluginLoopHost coroutineManager = new(
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

            LabelElementSearch.ClearThreadLocalStorage();
            MovementSkillMath.ClearThreadSkillBarEntriesBuffer();
            ShrineService.ClearThreadLocalStorageForCurrentThread();
            ClickAutomationPort.ClearThreadLocalStorageForCurrentThread();
            owner.State.Services.InventoryInteractionPolicy?.ClearForShutdown();
            owner.State.Services.AltarService?.ClearRuntimeCaches();

            owner.State.DisposeCompositionRoot();

            DrainFinalizers();
        }

        private static Coroutine?[] TrackedCoroutines(PluginContext state)
            => [state.Runtime.AltarCoroutine, state.Runtime.ClickLabelCoroutine, state.Runtime.ManualUiHoverCoroutine, state.Runtime.DelveFlareCoroutine, state.Runtime.GameStateDumpCoroutine, state.Runtime.AreaBlockedUiRefreshCoroutine];

        private static void StopTrackedCoroutines(PluginContext state)
        {
            foreach (Coroutine? coroutine in TrackedCoroutines(state))
                coroutine?.Done();

            // Overlay API: the host owns the per-overlay refresh coroutines.
            state.Rendering.OverlayRenderHost?.StopAll();
        }

        private static void WaitForTrackedCoroutines(PluginContext state)
        {
            foreach (Coroutine? coroutine in TrackedCoroutines(state))
                WaitForCoroutineShutdown(coroutine);
        }

        private static void ClearTrackedCoroutineReferences(PluginContext state)
        {
            PluginRuntimeState runtime = state.Runtime;
            runtime.AltarCoroutine = null;
            runtime.ClickLabelCoroutine = null;
            runtime.ManualUiHoverCoroutine = null;
            runtime.DelveFlareCoroutine = null;
            runtime.GameStateDumpCoroutine = null;
            runtime.AreaBlockedUiRefreshCoroutine = null;
        }

        private static void WaitForCoroutineShutdown(Coroutine? coroutine, int timeoutMs = 750)
        {
            if (coroutine == null)
                return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!coroutine.IsDone && stopwatch.ElapsedMilliseconds < timeoutMs)
                Thread.Sleep(10);

        }

        private static void StopNamedClickItCoroutines()
        {
            try
            {
                Coroutine[] coroutines = [.. ExileCoreApi.ParallelRunner.Coroutines.Where(c => c != null && PluginCoroutineNames.IsTrackedName(c.Name))];

                foreach (Coroutine? coroutine in coroutines)
                    coroutine.Done();

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
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (stopwatch.ElapsedMilliseconds < timeoutMs)
                {
                    bool anyActive = ExileCoreApi.ParallelRunner.Coroutines
                        .Any(c => c != null
                            && PluginCoroutineNames.IsTrackedName(c.Name)
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
