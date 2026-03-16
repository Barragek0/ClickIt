using ExileCore;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;
using System.Collections;
using ClickIt;

namespace ClickIt.Utils
{
    public partial class CoroutineManager(
        PluginContext state,
        ClickItSettings settings,
        GameController gameController,
        ErrorHandler errorHandler)
    {
        private readonly PluginContext _state = state ?? throw new ArgumentNullException(nameof(state));
        private readonly ClickItSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly GameController _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
        private readonly ErrorHandler _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        private long _lastCanClickFailureLogTimestampMs;

        /// <summary>
        /// Check if a ritual is currently active by looking for RitualBlocker entities
        /// </summary>
        private bool IsRitualActive()
        {
            return EntityHelpers.IsRitualActive(_gameController);
        }

        private double GetTargetTime(double frequencyTarget, double averageTiming)
        {
            return (frequencyTarget - averageTiming) + _state.Random.Next(0, 6);
        }

        public void StartCoroutines(BaseSettingsPlugin<ClickItSettings> plugin)
        {
            _state.AltarCoroutine = new Coroutine(MainScanForAltarsLogic(), plugin, "ClickIt.ScanForAltarsLogic", false);
            _ = Core.ParallelRunner.Run(_state.AltarCoroutine);
            _state.AltarCoroutine.Priority = CoroutinePriority.Normal;

            _state.ClickLabelCoroutine = new Coroutine(MainClickLabelCoroutine(), plugin, "ClickIt.ClickLogic", false);
            _ = Core.ParallelRunner.Run(_state.ClickLabelCoroutine);
            _state.ClickLabelCoroutine.Priority = CoroutinePriority.High;

            _state.DelveFlareCoroutine = new Coroutine(FlareCoroutine(), plugin, "ClickIt.DelveFlareLogic", true);
            _ = Core.ParallelRunner.Run(_state.DelveFlareCoroutine);
            _state.DelveFlareCoroutine.Priority = CoroutinePriority.Normal;
        }

        private IEnumerator MainScanForAltarsLogic()
        {
            while (_settings.Enable && !_state.IsShuttingDown)
            {
                yield return ScanForAltarsLogic();
            }
        }

        private IEnumerator ScanForAltarsLogic()
        {
            if (_state.IsShuttingDown || _state.PerformanceMonitor == null) yield break;

            _state.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Altar);
            _state.AltarService?.ProcessAltarScanningLogic();
            _state.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Altar);

            _state.AltarCoroutine?.Pause();
        }

        private IEnumerator MainClickLabelCoroutine()
        {
            while (_settings.Enable && !_state.IsShuttingDown)
            {
                yield return ClickLabel();
            }
        }

        private IEnumerator ClickLabel()
        {
            if (_state.IsShuttingDown || _state.PerformanceMonitor == null || _state.ClickService == null) yield break;
            double avgClickTime = _state.PerformanceMonitor.GetAverageTiming(TimingChannel.Click);

            bool isRitualActive = IsRitualActive();
            var cached = _state.CachedLabels?.Value;
            bool hasLazyModeRestrictedItemsOnScreen = _state.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(cached) ?? false;
            bool lazyModeActive = _settings.LazyMode.Value &&
                                  !hasLazyModeRestrictedItemsOnScreen &&
                                  !isRitualActive;

            double frequencyTarget = lazyModeActive ? _settings.LazyModeClickLimiting.Value : _settings.ClickFrequencyTarget.Value;
            double targetTime = GetTargetTime(frequencyTarget, avgClickTime);
            bool readyByTime = _state.Timer.ElapsedMilliseconds >= targetTime;
            bool canClick = _state.InputHandler?.CanClick(_gameController, hasLazyModeRestrictedItemsOnScreen, isRitualActive) == true;
            if (!readyByTime || !canClick)
            {
                bool hotkeyHeld = _state.InputHandler?.IsClickHotkeyPressed(_state.CachedLabels, _state.LabelFilterService) ?? true;
                bool lazyModeEnabled = _settings.LazyMode?.Value == true;
                if (ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled, hotkeyHeld))
                {
                    _state.ClickService?.CancelOffscreenPathingState();
                }

                if (_settings.DebugMode?.Value == true)
                {
                    long now = Environment.TickCount64;
                    if (now - _lastCanClickFailureLogTimestampMs >= 500)
                    {
                        _lastCanClickFailureLogTimestampMs = now;
                        string canClickReason = canClick
                            ? "Timer gating"
                            : (_state.InputHandler?.GetCanClickFailureReason(_gameController) ?? "Unknown click blocker");
                        int labelCount = cached?.Count ?? 0;
                        _errorHandler.LogMessage($"[ClickLogic] blocked: reason='{canClickReason}', readyByTime={readyByTime}, hasRestricted={hasLazyModeRestrictedItemsOnScreen}, ritualActive={isRitualActive}, labels={labelCount}", 10);
                    }
                }

                _state.WorkFinished = true;
                yield break;
            }

            long clickSequenceBefore = _state.InputHandler?.GetSuccessfulClickSequence() ?? 0;
            _state.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Click);
            yield return _state.ClickService.ProcessRegularClick();
            _state.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            long clickSequenceAfter = _state.InputHandler?.GetSuccessfulClickSequence() ?? 0;
            if (ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter))
            {
                _state.Timer.Restart();
            }

            _state.WorkFinished = true;
        }

        internal static bool ShouldRestartClickTimerAfterSuccessfulClick(long clickSequenceBefore, long clickSequenceAfter)
        {
            return clickSequenceAfter > clickSequenceBefore;
        }

        internal static bool ShouldCancelOffscreenPathingForInputRelease(bool lazyModeEnabled, bool clickHotkeyHeld)
        {
            return !lazyModeEnabled && !clickHotkeyHeld;
        }

        private IEnumerator FlareCoroutine()
        {
            if (_state.IsShuttingDown || _state.PerformanceMonitor == null) yield break;

            while (_settings.Enable && !_state.IsShuttingDown)
            {
                _state.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Flare);

                yield return ProcessFlare();

                _state.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Flare);

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

