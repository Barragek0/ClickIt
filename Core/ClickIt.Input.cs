using ExileCore;
using ExileCore.Shared;

namespace ClickIt
{
    public partial class ClickIt
    {
        public override Job? Tick()
        {
            if (State.IsShuttingDown)
            {
                return null;
            }

            bool hotkeyPressed = IsClickHotkeyPressed();

            if (hotkeyPressed)
            {
                HandleHotkeyPressed();
            }
            else
            {
                HandleHotkeyReleased();
            }

            ResumeAltarScanningIfDue(hotkeyPressed);

            return null;
        }

        private bool IsClickHotkeyPressed()
        {
            return State.InputHandler?.IsClickHotkeyPressed(State.CachedLabels, State.LabelFilterService) ?? false;
        }

        private void HandleHotkeyPressed()
        {
            if (State.IsShuttingDown)
            {
                return;
            }

            State.ManualUiHoverCoroutine?.Pause();

            if (State.ClickLabelCoroutine?.IsDone == true)
            {
                State.ClickLabelCoroutine = FindExistingClickLogicCoroutine();
            }

            State.ClickLabelCoroutine?.Resume();
            State.WorkFinished = false;
        }

        private void HandleHotkeyReleased()
        {
            State.ClickService?.CancelPostChestLootSettlementState();

            if (ShouldUseManualUiHoverCoroutine())
            {
                State.ClickLabelCoroutine?.Pause();

                if (State.ManualUiHoverCoroutine?.IsDone == true)
                {
                    State.ManualUiHoverCoroutine = FindExistingManualUiHoverCoroutine();
                }

                State.ManualUiHoverCoroutine?.Resume();
            }
            else
            {
                State.ManualUiHoverCoroutine?.Pause();
            }

            if (State.WorkFinished)
            {
                State.ClickLabelCoroutine?.Pause();
            }

            State.PerformanceMonitor?.ResetClickCount();
        }

        private bool ShouldUseManualUiHoverCoroutine()
            => ShouldRunManualUiHoverCoroutineForInputState(
                Settings?.ClickOnManualUiHoverOnly?.Value == true,
                Settings?.LazyMode?.Value == true);

        internal static bool ShouldRunManualUiHoverCoroutineForInputState(bool manualUiHoverEnabled, bool lazyModeEnabled)
            => manualUiHoverEnabled && !lazyModeEnabled;

        private static Coroutine? FindExistingClickLogicCoroutine()
        {
            foreach (Coroutine coroutine in global::ExileCore.Core.ParallelRunner.Coroutines)
            {
                if (coroutine != null
                    && string.Equals(coroutine.Name, "ClickIt.ClickLogic", StringComparison.Ordinal)
                    && !coroutine.IsDone)
                {
                    return coroutine;
                }
            }

            return null;
        }

        private static Coroutine? FindExistingManualUiHoverCoroutine()
        {
            foreach (Coroutine coroutine in global::ExileCore.Core.ParallelRunner.Coroutines)
            {
                if (coroutine != null
                    && string.Equals(coroutine.Name, "ClickIt.ManualUiHoverLogic", StringComparison.Ordinal)
                    && !coroutine.IsDone)
                {
                    return coroutine;
                }
            }

            return null;
        }

        private void ResumeAltarScanningIfDue(bool clickHotkeyPressed)
        {

            if (State.SecondTimer.ElapsedMilliseconds > 200)
            {
                State.AltarCoroutine?.Resume();
                State.SecondTimer.Restart();
            }
        }
    }
}
