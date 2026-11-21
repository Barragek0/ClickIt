namespace ClickIt.Core.Runtime
{
    internal sealed class PluginInputHost
    {
        public static void Tick(PluginContext state, ClickItSettings? settings, GameController? gameController = null)
        {
            PluginRuntimeState runtime = state.Runtime;
            if (runtime.IsShuttingDown)
                return;

            bool enabled = settings?.DebugMode.Value == true;
            bool foreground = enabled && PluginClickRuntimeStateEvaluator.ResolvePoeForeground(gameController);
            Keys key = settings?.DumpAllHotkey?.Value.Key ?? Keys.None;
            bool keyDown = foreground && key != Keys.None && Input.GetKeyState(key);
            bool wasDown = runtime.DumpAllHotkeyWasDown;
            bool trigger = ShouldTriggerDumpHotkey(enabled, foreground, keyDown, ref wasDown);
            runtime.DumpAllHotkeyWasDown = wasDown;
            if (trigger)
                GameStateDumpCoordinator.Current?.QueueDumpAll();

            bool hotkeyPressed = IsClickHotkeyPressed(state);

            if (hotkeyPressed)
                HandleHotkeyPressed(state);

            else
                HandleHotkeyReleased(state, settings);


            ResumeAltarScanningIfDue(state);
        }

        internal static bool ShouldTriggerDumpHotkey(bool enabled, bool foreground, bool keyDown, ref bool wasDown)
        {
            bool active = enabled && foreground && keyDown;
            bool pressed = active && !wasDown;
            wasDown = active;
            return pressed;
        }

        internal static bool IsClickHotkeyPressed(PluginContext state)
            => PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(state.Services);

        internal static void HandleHotkeyPressed(PluginContext state)
        {
            PluginRuntimeState runtime = state.Runtime;
            if (runtime.IsShuttingDown)
                return;


            runtime.ManualUiHoverCoroutine?.Pause();

            if (runtime.ClickLabelCoroutine?.IsDone == true)
                runtime.ClickLabelCoroutine = PluginCoroutineRegistry.FindClickLogicCoroutine();


            runtime.ClickLabelCoroutine?.Resume();
            runtime.WorkFinished = false;
        }

        internal static void HandleHotkeyReleased(PluginContext state, ClickItSettings? settings)
        {
            PluginRuntimeState runtime = state.Runtime;
            PluginServices services = state.Services;
            services.ClickAutomationPort?.CancelPostChestLootSettlementState();

            bool shouldRunManualUiHoverCoroutine = PluginClickRuntimeStateEvaluator.ResolveManualUiHoverMode(
                settings,
                clickHotkeyActive: false);

            UpdateManualUiHoverCoroutineForHotkeyRelease(runtime, shouldRunManualUiHoverCoroutine);

            if (runtime.WorkFinished)
                runtime.ClickLabelCoroutine?.Pause();


            services.PerformanceMonitor?.ResetClickCount();
        }

        internal static void ResumeAltarScanningIfDue(PluginContext state)
        {
            PluginRuntimeState runtime = state.Runtime;
            if (runtime.SecondTimer.ElapsedMilliseconds > 200)
            {
                runtime.AltarCoroutine?.Resume();
                runtime.SecondTimer.Restart();
            }
        }

        private static void UpdateManualUiHoverCoroutineForHotkeyRelease(PluginRuntimeState runtime, bool shouldRunCoroutine)
        {
            if (!shouldRunCoroutine)
            {
                runtime.ManualUiHoverCoroutine?.Pause();
                return;
            }

            runtime.ClickLabelCoroutine?.Pause();

            if (runtime.ManualUiHoverCoroutine?.IsDone == true)
                runtime.ManualUiHoverCoroutine = PluginCoroutineRegistry.FindManualUiHoverCoroutine();


            runtime.ManualUiHoverCoroutine?.Resume();
        }
    }
}
