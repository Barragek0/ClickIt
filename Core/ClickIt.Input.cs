using ExileCore;
using ExileCore.Shared; // Coroutine, Job and related helpers live here
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms; // Keys

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
            bool hotkeyHeld = Input.GetKeyState(Settings.ClickLabelKey.Value);
            if (!Settings.LazyMode.Value)
            {
                return hotkeyHeld;
            }

            // Lazy mode: held always enables (manual override), released enables lazy auto only if safe
            bool hasRestricted = State.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(State.CachedLabels?.Value ?? []) ?? false;
            bool disableKeyHeld = Input.GetKeyState(Settings.LazyModeDisableKey.Value);
            bool leftClickBlocks = Settings.DisableLazyModeLeftClickHeld.Value && Input.GetKeyState(Keys.LButton);
            bool rightClickBlocks = Settings.DisableLazyModeRightClickHeld.Value && Input.GetKeyState(Keys.RButton);
            bool mouseButtonBlocks = leftClickBlocks || rightClickBlocks;

            if (hotkeyHeld)
            {
                return true;  // Manual override: always enable clicking (safe + restricted)
            }

            // Hotkey released: lazy auto only if no blockers
            return !hasRestricted && !disableKeyHeld && !mouseButtonBlocks;
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

        /// <summary>
        /// Resumes altar scanning coroutine if sufficient time has elapsed.
        /// Implements a throttling mechanism to prevent excessive scanning.
        /// </summary>
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
