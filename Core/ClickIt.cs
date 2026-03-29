using ClickIt.Utils;
using ExileCore;
using System.Diagnostics;
using System.Threading;

namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
        }

        public override void OnClose()
        {
            ClickItSettings runtimeSettings = Settings ?? EffectiveSettings;
            State.IsShuttingDown = true;

            // Remove event handlers to prevent issues during DLL reload
            runtimeSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
            runtimeSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            if (!ReferenceEquals(runtimeSettings, EffectiveSettings))
            {
                EffectiveSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
                EffectiveSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            }

            State.AltarCoroutine?.Done();
            State.ClickLabelCoroutine?.Done();
            State.ManualUiHoverCoroutine?.Done();
            State.DelveFlareCoroutine?.Done();
            State.DeepMemoryDumpCoroutine?.Done();
            StopAllClickItCoroutines();
            WaitForCoroutineShutdown(State.AltarCoroutine);
            WaitForCoroutineShutdown(State.ClickLabelCoroutine);
            WaitForCoroutineShutdown(State.ManualUiHoverCoroutine);
            WaitForCoroutineShutdown(State.DelveFlareCoroutine);
            WaitForCoroutineShutdown(State.DeepMemoryDumpCoroutine);
            WaitForAllClickItCoroutinesShutdown();

            State.AltarCoroutine = null;
            State.ClickLabelCoroutine = null;
            State.ManualUiHoverCoroutine = null;
            State.DelveFlareCoroutine = null;
            State.DeepMemoryDumpCoroutine = null;

            LockManager.Instance = null;

            LabelUtils.ClearThreadLocalStorage();
            Services.ShrineService.ClearThreadLocalStorageForCurrentThread();
            Services.ClickService.ClearThreadLocalStorageForCurrentThread();
            Services.LabelFilterService.ClearInventoryProbeCacheForShutdown();
            State.AltarService?.ClearRuntimeCaches();

            State.DisposeCompositionRoot();

            // Best-effort finalizer drain to reduce transient assembly/file lock windows during host hot-reload.
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

            // In some test scenarios the Settings property isn't populated on the base class even though tests inject settings via the test seam.
            // Avoid invoking base.OnClose when the real Settings property is null to prevent ExileCore.BaseSettingsPlugin from attempting to save a null settings instance.
            if (Settings != null)
            {
                base.OnClose();
            }
        }

        public override bool Initialise()
        {
            var settings = Settings
                ?? throw new InvalidOperationException("Settings is null during plugin initialization.");

            State.IsShuttingDown = false;
            settings.ReportBugButton.OnPressed += ReportBugButtonPressed;
            settings.CopyAdditionalDebugInfoButton.OnPressed += CopyAdditionalDebugInfoButtonPressed;
            State.InitializeCompositionRoot(this, settings);
            var errorHandler = State.ErrorHandler
                ?? throw new InvalidOperationException("ErrorHandler was not initialized by composition root.");
            if (GameController == null)
                throw new InvalidOperationException("GameController is null during coroutine manager initialization.");
            var gameController = GameController;
            errorHandler.RegisterGlobalExceptionHandlers();

            var coroutineManager = new CoroutineManager(
                State!,
                settings,
                gameController!,
                errorHandler!);
            coroutineManager.StartCoroutines(this);

            State.FinalizeCompositionRootForStartup(this, settings);

            return true;
        }

        private static void WaitForCoroutineShutdown(ExileCore.Shared.Coroutine? coroutine, int timeoutMs = 750)
        {
            if (coroutine == null)
                return;

            var sw = Stopwatch.StartNew();
            while (!coroutine.IsDone && sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(10);
            }
        }

        private static void StopAllClickItCoroutines()
        {
            try
            {
                var coroutines = Core.ParallelRunner.Coroutines
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

        private static void WaitForAllClickItCoroutinesShutdown(int timeoutMs = 2000)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    bool anyActive = Core.ParallelRunner.Coroutines
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

        private void ReportBugButtonPressed()
        {
            _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues");
        }

        private void CopyAdditionalDebugInfoButtonPressed()
        {
            _copyAdditionalDebugInfoRequested = true;
            if (GameController != null)
                QueueDeepMemoryDumpCoroutine();
        }

        public override void Render()
        {
            if (State.IsShuttingDown || State.PerformanceMonitor == null) return;

            // Set flag to prevent logging during render loop
            State.IsRendering = true;
            try
            {
                RenderInternal();
            }
            finally
            {
                State.IsRendering = false;
            }
        }

        public void LogMessage(string message, int frame = 5)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogMessage(message, frame);
        }

        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            if (!localDebug || Settings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }
        public void LogError(string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogError(message, frame);
        }

    }
}
