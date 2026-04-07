namespace ClickIt.Core.Runtime
{
    internal sealed class PluginInputHost
    {
        public void Tick(PluginContext state, ClickItSettings? settings)
        {
            PluginRuntimeState runtime = state.Runtime;
            if (runtime.IsShuttingDown)
            {
                return;
            }

            bool hotkeyPressed = IsClickHotkeyPressed(state);

            if (hotkeyPressed)
            {
                HandleHotkeyPressed(state);
            }
            else
            {
                HandleHotkeyReleased(state, settings);
            }

            ResumeAltarScanningIfDue(state);
        }

        internal bool IsClickHotkeyPressed(PluginContext state)
            => PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(state.Services);

        internal void HandleHotkeyPressed(PluginContext state)
        {
            PluginRuntimeState runtime = state.Runtime;
            if (runtime.IsShuttingDown)
            {
                return;
            }

            runtime.ManualUiHoverCoroutine?.Pause();

            if (runtime.ClickLabelCoroutine?.IsDone == true)
            {
                runtime.ClickLabelCoroutine = PluginCoroutineRegistry.FindClickLogicCoroutine();
            }

            runtime.ClickLabelCoroutine?.Resume();
            runtime.WorkFinished = false;
        }

        internal void HandleHotkeyReleased(PluginContext state, ClickItSettings? settings)
        {
            PluginRuntimeState runtime = state.Runtime;
            PluginServices services = state.Services;
            services.ClickAutomationPort?.CancelPostChestLootSettlementState();

            bool shouldRunManualUiHoverCoroutine = PluginClickRuntimeStateEvaluator.ResolveManualUiHoverMode(
                settings,
                clickHotkeyActive: false).ShouldRunCoroutine;

            UpdateManualUiHoverCoroutineForHotkeyRelease(runtime, shouldRunManualUiHoverCoroutine);

            if (runtime.WorkFinished)
            {
                runtime.ClickLabelCoroutine?.Pause();
            }

            services.PerformanceMonitor?.ResetClickCount();
        }

        internal void ResumeAltarScanningIfDue(PluginContext state)
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
            {
                runtime.ManualUiHoverCoroutine = PluginCoroutineRegistry.FindManualUiHoverCoroutine();
            }

            runtime.ManualUiHoverCoroutine?.Resume();
        }
    }
}