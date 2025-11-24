using ExileCore;
using ExileCore.Shared;

namespace ClickIt
{
    public partial class ClickIt
    {
        public override Job? Tick()
        {
            bool hotkeyPressed = IsClickHotkeyPressed();

            if (hotkeyPressed)
            {
                HandleHotkeyPressed();
            }
            else
            {
                HandleHotkeyReleased();
            }

            ResumeAltarScanningIfDue();

            return null;
        }

        private bool IsClickHotkeyPressed()
        {
            return State.InputHandler?.IsClickHotkeyPressed(State.CachedLabels, State.LabelFilterService) ?? false;
        }

        private void HandleHotkeyPressed()
        {
            if (State.ClickLabelCoroutine?.IsDone == true)
            {
                State.ClickLabelCoroutine = FindExistingClickLogicCoroutine();
            }

            State.ClickLabelCoroutine?.Resume();
            State.WorkFinished = false;
        }

        private void HandleHotkeyReleased()
        {
            if (State.WorkFinished)
            {
                State.ClickLabelCoroutine?.Pause();
            }
            State.PerformanceMonitor?.ResetClickCount();
        }

        private static Coroutine? FindExistingClickLogicCoroutine()
        {
            return Core.ParallelRunner.Coroutines.FirstOrDefault(x => x.Name == "ClickIt.ClickLogic");
        }

        private void ResumeAltarScanningIfDue()
        {
            if (State.SecondTimer.ElapsedMilliseconds > 200)
            {
                State.AltarCoroutine?.Resume();
                State.SecondTimer.Restart();
            }
        }
    }
}
