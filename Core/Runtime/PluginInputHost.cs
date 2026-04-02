using ExileCore.Shared;

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
        {
            PluginServices services = state.Services;
            return services.InputHandler?.IsClickHotkeyPressed(services.CachedLabels, services.LabelFilterService) ?? false;
        }

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
            services.ClickService?.CancelPostChestLootSettlementState();

            if (ShouldRunManualUiHoverCoroutine(settings))
            {
                runtime.ClickLabelCoroutine?.Pause();

                if (runtime.ManualUiHoverCoroutine?.IsDone == true)
                {
                    runtime.ManualUiHoverCoroutine = PluginCoroutineRegistry.FindManualUiHoverCoroutine();
                }

                runtime.ManualUiHoverCoroutine?.Resume();
            }
            else
            {
                runtime.ManualUiHoverCoroutine?.Pause();
            }

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

        internal static bool ShouldRunManualUiHoverCoroutine(bool manualUiHoverEnabled, bool lazyModeEnabled)
            => manualUiHoverEnabled && !lazyModeEnabled;

        internal static bool ShouldRunManualUiHoverCoroutine(ClickItSettings? settings)
            => ShouldRunManualUiHoverCoroutine(
                settings?.ClickOnManualUiHoverOnly?.Value == true,
                settings?.LazyMode?.Value == true);
    }
}