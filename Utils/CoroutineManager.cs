using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using SharpDX;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using RectangleF = SharpDX.RectangleF;
using ClickIt;

namespace ClickIt.Utils
{
    public class CoroutineManager(
        PluginContext state,
        ClickItSettings settings,
        GameController gameController,
        ErrorHandler errorHandler,
        Func<Vector2, bool> pointIsInClickableArea)
    {
        private readonly PluginContext _state = state ?? throw new ArgumentNullException(nameof(state));
        private readonly ClickItSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly GameController _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
        private readonly ErrorHandler _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        private readonly Func<Vector2, bool> _pointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));

        /// <summary>
        /// Helper to execute an action while holding the click-element access lock (if LockManager enabled)
        /// </summary>
        private void ExecuteWithElementAccessLock(Action action)
        {
            using (LockManager.AcquireStatic(_state.ClickService?.GetElementAccessLock()))
            {
                action();
            }
        }

        /// <summary>
        /// Check if a ritual is currently active by looking for RitualBlocker entities
        /// </summary>
        private bool IsRitualActive()
        {
            return EntityHelpers.IsRitualActive(_gameController);
        }

        private bool IsShrineClickBlockedInLazyMode()
        {
            if (!_settings.LazyMode.Value) return false;
            bool hasRestrictedItems = _state.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(_state.CachedLabels?.Value ?? []) ?? false;
            if (hasRestrictedItems) return false;
            bool leftClickBlocks = _settings.DisableLazyModeLeftClickHeld.Value && Input.GetKeyState(Keys.LButton);
            bool rightClickBlocks = _settings.DisableLazyModeRightClickHeld.Value && Input.GetKeyState(Keys.RButton);
            return leftClickBlocks || rightClickBlocks;
        }

        private bool HasClickableAltars()
        {
            if (_state.AltarService == null || _state.ClickService == null) return false;
            var altarSnapshot = _state.AltarService.GetAltarComponentsReadOnly();
            if (altarSnapshot.Count == 0) return false;
            bool clickEater = _settings.ClickEaterAltars;
            bool clickExarch = _settings.ClickExarchAltars;
            return altarSnapshot.Any(altar => _state.ClickService.ShouldClickAltar(altar, clickEater, clickExarch));
        }

        public void StartCoroutines(BaseSettingsPlugin<ClickItSettings> plugin)
        {
            _state.AltarCoroutine = new Coroutine(MainScanForAltarsLogic(), plugin, "ClickIt.ScanForAltarsLogic", false);
            _ = Core.ParallelRunner.Run(_state.AltarCoroutine);
            _state.AltarCoroutine.Priority = CoroutinePriority.Normal;

            _state.ClickLabelCoroutine = new Coroutine(MainClickLabelCoroutine(), plugin, "ClickIt.ClickLogic", false);
            _ = Core.ParallelRunner.Run(_state.ClickLabelCoroutine);
            _state.ClickLabelCoroutine.Priority = CoroutinePriority.High;

            _state.ShrineCoroutine = new Coroutine(MainShrineCoroutine(), plugin, "ClickIt.ShrineLogic", true);
            _ = Core.ParallelRunner.Run(_state.ShrineCoroutine);
            _state.ShrineCoroutine.Priority = CoroutinePriority.High;

            _state.DelveFlareCoroutine = new Coroutine(FlareCoroutine(), plugin, "ClickIt.DelveFlareLogic", true);
            _ = Core.ParallelRunner.Run(_state.DelveFlareCoroutine);
            _state.DelveFlareCoroutine.Priority = CoroutinePriority.Normal;
        }

        private IEnumerator MainScanForAltarsLogic()
        {
            while (_settings.Enable)
            {
                yield return ScanForAltarsLogic();
            }
        }

        private IEnumerator ScanForAltarsLogic()
        {
            if (_state.PerformanceMonitor == null) yield break;

            _state.PerformanceMonitor.StartCoroutineTiming("altar");
            _state.AltarService?.ProcessAltarScanningLogic();
            _state.PerformanceMonitor.StopCoroutineTiming("altar");

            _state.AltarCoroutine?.Pause();
        }

        private IEnumerator MainClickLabelCoroutine()
        {
            while (_settings.Enable)
            {
                yield return ClickLabel();
            }
        }

        private IEnumerator ClickLabel()
        {
            // Check for clickable altars first (highest priority)
            bool hasClickableAltars = false;
            if (_state.AltarService != null && _state.ClickService != null)
            {
                var altarSnapshot = _state.AltarService.GetAltarComponentsReadOnly();
                if (altarSnapshot.Count > 0)
                {
                    bool clickEater = _settings.ClickEaterAltars;
                    bool clickExarch = _settings.ClickExarchAltars;
                    hasClickableAltars = altarSnapshot.Any(altar => _state.ClickService.ShouldClickAltar(altar, clickEater, clickExarch));
                }
            }

            if (!hasClickableAltars)
            {
                // No clickable altars, check for shrines
                if (_settings.ClickShrines.Value && _state.ShrineService != null && _state.ShrineService.AreShrinesPresentInClickableArea((pos) => _pointIsInClickableArea(pos)))
                {
                    yield return new WaitTime(25);
                    yield break;
                }
            }

            if (_state.PerformanceMonitor == null || _state.ClickService == null) yield break;
            double avgClickTime = _state.PerformanceMonitor.GetAverageTiming("click");

            // Determine if lazy mode is active (enabled and no restricted items on screen and no ritual active)
            bool isRitualActive = IsRitualActive();
            bool lazyModeActive = _settings.LazyMode.Value &&
                !(_state.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(_state.CachedLabels?.Value ?? []) ?? false) &&
                !isRitualActive;

            // Check if there are lazy mode restricted items on screen
            bool hasLazyModeRestrictedItemsOnScreen = _state.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(_state.CachedLabels?.Value ?? []) ?? false;

            // Use lazy mode click limiting when lazy mode is active, otherwise use normal frequency target
            double frequencyTarget = lazyModeActive ? _settings.LazyModeClickLimiting.Value : _settings.ClickFrequencyTarget.Value;
            double baseTarget = frequencyTarget - avgClickTime;
            double targetTime = baseTarget + _state.Random.Next(0, 6);
            if (_state.Timer.ElapsedMilliseconds < targetTime || _state.InputHandler?.CanClick(_gameController, hasLazyModeRestrictedItemsOnScreen, isRitualActive) != true)
            {
                _state.WorkFinished = true;
                yield break;
            }

            if (_settings.DebugMode.Value)
            {
                _errorHandler.LogMessage($"Starting click process...", 5);
            }

            _state.Timer.Restart();
            _state.PerformanceMonitor.StartCoroutineTiming("click");
            yield return _state.ClickService.ProcessRegularClick();
            _state.PerformanceMonitor.StopCoroutineTiming("click");

            _state.WorkFinished = true;
        }

        private IEnumerator MainShrineCoroutine()
        {
            if (_state.PerformanceMonitor == null) yield break;

            while (_settings.Enable)
            {
                _state.PerformanceMonitor.StartCoroutineTiming("shrine");

                yield return HandleShrine();

                _state.PerformanceMonitor.StopCoroutineTiming("shrine");
            }
        }

        private IEnumerator HandleShrine()
        {
            if (!_settings.ClickShrines.Value || _state.ShrineService == null)
            {
                yield return new WaitTime(500); // Check less frequently when disabled
                yield break;
            }

            yield return ProcessShrineClicking();
        }

        private IEnumerator ProcessShrineClicking()
        {
            if (_state.ShrineService == null || _state.InputHandler == null)
            {
                yield break;
            }

            if (_state.PerformanceMonitor == null) yield break;
            double avgShrineTime = _state.PerformanceMonitor.GetAverageTiming("shrine");
            double baseTarget = _settings.ClickFrequencyTarget.Value - avgShrineTime;
            double targetTime = baseTarget + _state.Random.Next(0, 6);

            bool isRitualActive = IsRitualActive();
            if (IsShrineClickBlockedInLazyMode())
            {
                yield break;
            }

            if (_state.ShrineTimer.ElapsedMilliseconds < targetTime || !_state.InputHandler.CanClick(_gameController, false, isRitualActive))
            {
                yield break;
            }

            if (HasClickableAltars())
            {
                yield break;
            }

            var nearestShrine = _state.ShrineService.GetNearestShrineInRange(_settings.ClickDistance.Value, point => _pointIsInClickableArea(point));
            if (nearestShrine == null)
            {
                yield break;
            }

            _errorHandler.LogMessage($"Clicking shrine at distance: {nearestShrine.DistancePlayer:F1}", 5);

            if (_state.Camera == null)
            {
                yield break;
            }

            var screen = _state.Camera.WorldToScreen(nearestShrine.PosNum);
            Vector2 clickPos = new(screen.X, screen.Y);

            _state.ShrineTimer.Restart();

            ExecuteWithElementAccessLock(() => _state.InputHandler?.PerformClick(clickPos));

            _state.PerformanceMonitor?.RecordClickInterval();

            _state.ShrineService?.InvalidateCache();
        }

        private bool IsClickHotkeyPressed()
        {
            bool actual = Input.GetKeyState(_settings.ClickLabelKey.Value);
            if (_settings?.LazyMode != null && _settings.LazyMode.Value)
            {
                // In lazy mode, invert hotkey behaviour: released -> active, held -> inactive
                return !actual;
            }
            return actual;
        }

        private IEnumerator FlareCoroutine()
        {
            if (_state.PerformanceMonitor == null) yield break;

            while (_settings.Enable)
            {
                _state.PerformanceMonitor.StartCoroutineTiming("flare");

                yield return ProcessFlare();

                _state.PerformanceMonitor.StopCoroutineTiming("flare");

                yield return new WaitTime(100);
            }
        }

        private IEnumerator ProcessFlare()
        {
            if (!_settings.ClickDelveFlares || _gameController?.Player?.Buffs == null)
                yield break;

            var delveBuff = _gameController.Player.Buffs.FirstOrDefault(b => b.Name == "delve_degen_buff");
            if (delveBuff == null || delveBuff.Charges < _settings.DarknessDebuffStacks.Value)
                yield break;

            float healthPercent = GetPlayerHealthPercent();
            float energyShieldPercent = GetPlayerEnergyShieldPercent();
            if (healthPercent > _settings.DelveFlareHealthThreshold.Value ||
                energyShieldPercent > _settings.DelveFlareEnergyShieldThreshold.Value)
                yield break;

            if (_state.InputHandler?.CanClick(_gameController, false, IsRitualActive()) != true)
                yield break;

            Keyboard.KeyPress(_settings.DelveFlareHotkey.Value, 50);
            _errorHandler.LogMessage($"Used delve flare (buff charges: {delveBuff.Charges}, health: {healthPercent:F1}%, es: {energyShieldPercent:F1}%)", 5);
            yield return new WaitTime(1000);
        }

        private float GetPlayerHealthPercent()
        {
#if RUNTIME_EXILECORE
            if (_gameController?.Player == null) return 100f;
            var life = _gameController.Player.GetComponent<ExileCore.PoEMemory.Components.Life>();
            if (life == null || life.Health.Max == 0) return 100f;
            return (float)life.Health.Current / life.Health.Max * 100f;
#else
            return 100f;
#endif
        }

        private float GetPlayerEnergyShieldPercent()
        {
#if RUNTIME_EXILECORE
            if (_gameController?.Player == null) return 100f;
            var life = _gameController.Player.GetComponent<ExileCore.PoEMemory.Components.Life>();
            if (life == null || life.EnergyShield.Max == 0) return 100f;
            return (float)life.EnergyShield.Current / life.EnergyShield.Max * 100f;
#else
            return 100f;
#endif
        }
    }
}
