using ExileCore;
using ExileCore.PoEMemory.Elements;
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
        private const int LazyModeContextCacheMs = 50;
        private long _lastCanClickFailureLogTimestampMs;
        private long _lastLazyModeContextRefreshTimestampMs = long.MinValue;
        private bool _cachedRitualActive;
        private bool _cachedHasLazyModeRestrictedItems;
        private bool _cachedRitualEvaluated;
        private bool _cachedRestrictedItemsEvaluated;
        private IReadOnlyList<LabelOnGround>? _cachedLazyModeLabelsRef;
        private int _cachedLazyModeLabelCount = -1;

        /// <summary>
        /// Check if a ritual is currently active by looking for RitualBlocker entities
        /// </summary>
        private bool IsRitualActive()
        {
            return EntityHelpers.IsRitualActive(_gameController);
        }

        private (bool IsRitualActive, bool HasLazyModeRestrictedItems, IReadOnlyList<LabelOnGround>? Labels) GetCachedLazyModeContext(
            bool shouldEvaluateRitualState,
            bool shouldEvaluateRestrictedItems)
        {
            IReadOnlyList<LabelOnGround>? labels = _state.CachedLabels?.Value;
            int labelCount = labels?.Count ?? 0;
            long now = Environment.TickCount64;

            bool cacheStillFresh = (now - _lastLazyModeContextRefreshTimestampMs) < LazyModeContextCacheMs
                && ReferenceEquals(labels, _cachedLazyModeLabelsRef)
                && labelCount == _cachedLazyModeLabelCount;

            if (!cacheStillFresh)
            {
                _cachedLazyModeLabelsRef = labels;
                _cachedLazyModeLabelCount = labelCount;
                _lastLazyModeContextRefreshTimestampMs = now;
                _cachedRitualEvaluated = false;
                _cachedRestrictedItemsEvaluated = false;
            }

            if (shouldEvaluateRitualState && !_cachedRitualEvaluated)
            {
                _cachedRitualActive = IsRitualActive();
                _cachedRitualEvaluated = true;
            }

            if (shouldEvaluateRestrictedItems && !_cachedRestrictedItemsEvaluated)
            {
                _cachedHasLazyModeRestrictedItems = _state.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(labels) ?? false;
                _cachedRestrictedItemsEvaluated = true;
            }

            return (
                shouldEvaluateRitualState ? _cachedRitualActive : false,
                shouldEvaluateRestrictedItems ? _cachedHasLazyModeRestrictedItems : false,
                labels);
        }

        private double GetTargetTime(double frequencyTarget, double averageTiming)
        {
            return (frequencyTarget - averageTiming) + _state.Random.Next(0, 6);
        }

        public void StartCoroutines(BaseSettingsPlugin<ClickItSettings> plugin)
        {
            _state.AltarCoroutine = new Coroutine(MainScanForAltarsLogic(), plugin, "ClickIt.ScanForAltarsLogic", false);
            _ = global::ExileCore.Core.ParallelRunner.Run(_state.AltarCoroutine);
            _state.AltarCoroutine.Priority = CoroutinePriority.Normal;

            _state.ClickLabelCoroutine = new Coroutine(MainClickLabelCoroutine(), plugin, "ClickIt.ClickLogic", false);
            _ = global::ExileCore.Core.ParallelRunner.Run(_state.ClickLabelCoroutine);
            _state.ClickLabelCoroutine.Priority = CoroutinePriority.High;

            _state.ManualUiHoverCoroutine = new Coroutine(MainManualUiHoverClickCoroutine(), plugin, "ClickIt.ManualUiHoverLogic", false);
            _ = global::ExileCore.Core.ParallelRunner.Run(_state.ManualUiHoverCoroutine);
            _state.ManualUiHoverCoroutine.Priority = CoroutinePriority.High;

            _state.DelveFlareCoroutine = new Coroutine(FlareCoroutine(), plugin, "ClickIt.DelveFlareLogic", true);
            _ = global::ExileCore.Core.ParallelRunner.Run(_state.DelveFlareCoroutine);
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
            var runtimeHost = _state.ClickRuntimeHost
                ?? (_state.ClickService != null ? new global::ClickIt.Core.Runtime.ClickRuntimeHost(() => _state.ClickService) : null);

            if (_state.IsShuttingDown || _state.PerformanceMonitor == null || runtimeHost == null) yield break;

            bool hotkeyActive = _state.InputHandler?.IsClickHotkeyPressed(_state.CachedLabels, _state.LabelFilterService) == true;
            if (ShouldSuppressRegularClickForManualUiHoverMode(_settings.ClickOnManualUiHoverOnly.Value, _settings.LazyMode.Value, hotkeyActive))
            {
                _state.WorkFinished = true;
                yield break;
            }

            double avgClickTime = _state.PerformanceMonitor.GetAverageTiming(TimingChannel.Click);

            bool lazyModeEnabled = _settings.LazyMode.Value;
            bool shouldEvaluateRestrictedItems = ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled);
            bool shouldEvaluateRitualState = ShouldEvaluateRitualState(lazyModeEnabled, hotkeyActive);

            var lazyModeContext = GetCachedLazyModeContext(shouldEvaluateRitualState, shouldEvaluateRestrictedItems);
            bool isRitualActive = lazyModeContext.IsRitualActive;
            bool hasLazyModeRestrictedItemsOnScreen = lazyModeContext.HasLazyModeRestrictedItems;
            var cached = lazyModeContext.Labels;
            bool lazyModeActive = lazyModeEnabled &&
                                  !hasLazyModeRestrictedItemsOnScreen &&
                                  !isRitualActive;

            double frequencyTarget = lazyModeActive ? _settings.LazyModeClickLimiting.Value : _settings.ClickFrequencyTarget.Value;
            double targetTime = GetTargetTime(frequencyTarget, avgClickTime);
            bool readyByTime = _state.Timer.ElapsedMilliseconds >= targetTime;
            bool canClick = _state.InputHandler?.CanClick(_gameController, hasLazyModeRestrictedItemsOnScreen, isRitualActive) == true;
            if (!readyByTime || !canClick)
            {
                bool hotkeyHeld = _state.InputHandler == null || hotkeyActive;
                if (ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled, hotkeyHeld))
                {
                    runtimeHost.CancelOffscreenPathingState();
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
            yield return runtimeHost.ProcessRegularClick();
            _state.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            long clickSequenceAfter = _state.InputHandler?.GetSuccessfulClickSequence() ?? 0;
            if (ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter))
            {
                _state.Timer.Restart();
            }

            _state.WorkFinished = true;
        }

        internal IEnumerator RunClickLabelStepForTests()
            => ClickLabel();

        private IEnumerator MainManualUiHoverClickCoroutine()
        {
            while (_settings.Enable && !_state.IsShuttingDown)
            {
                yield return ProcessManualUiHoverClick();
                yield return new WaitTime(10);
            }
        }

        private IEnumerator ProcessManualUiHoverClick()
        {
            var runtimeHost = _state.ClickRuntimeHost
                ?? (_state.ClickService != null ? new global::ClickIt.Core.Runtime.ClickRuntimeHost(() => _state.ClickService) : null);

            if (_state.IsShuttingDown || _state.PerformanceMonitor == null || runtimeHost == null || _state.InputHandler == null)
                yield break;

            bool hotkeyActive = _state.InputHandler.IsClickHotkeyPressed(_state.CachedLabels, _state.LabelFilterService);
            if (!ShouldRunManualUiHoverCoroutine(_settings.ClickOnManualUiHoverOnly.Value, _settings.LazyMode.Value, hotkeyActive))
                yield break;

            bool isRitualActive = GetCachedLazyModeContext(shouldEvaluateRitualState: true, shouldEvaluateRestrictedItems: false).IsRitualActive;
            if (isRitualActive)
                yield break;

            if (!_state.InputHandler.CanClickWithoutInputState(_gameController))
                yield break;

            double avgClickTime = _state.PerformanceMonitor.GetAverageTiming(TimingChannel.Click);
            double targetTime = GetTargetTime(_settings.ClickFrequencyTarget.Value, avgClickTime);
            if (_state.Timer.ElapsedMilliseconds < targetTime)
                yield break;

            IReadOnlyList<ExileCore.PoEMemory.Elements.LabelOnGround>? labels = _state.CachedLabels?.Value;
            long clickSequenceBefore = _state.InputHandler.GetSuccessfulClickSequence();

            _state.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Click);
            bool clicked = runtimeHost.TryClickManualUiHoverLabel(labels);
            _state.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            long clickSequenceAfter = _state.InputHandler.GetSuccessfulClickSequence();
            if (clicked && ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter))
            {
                _state.Timer.Restart();
            }
        }

        internal static bool ShouldRestartClickTimerAfterSuccessfulClick(long clickSequenceBefore, long clickSequenceAfter)
        {
            return clickSequenceAfter > clickSequenceBefore;
        }

        internal static bool ShouldCancelOffscreenPathingForInputRelease(bool lazyModeEnabled, bool clickHotkeyHeld)
        {
            return !lazyModeEnabled && !clickHotkeyHeld;
        }

        internal static bool ShouldEvaluateRitualState(bool lazyModeEnabled, bool clickHotkeyActive)
        {
            return lazyModeEnabled || !clickHotkeyActive;
        }

        internal static bool ShouldEvaluateLazyModeRestrictedItems(bool lazyModeEnabled)
        {
            return lazyModeEnabled;
        }

        internal static bool ShouldRunManualUiHoverCoroutine(bool manualUiHoverEnabled, bool lazyModeEnabled, bool clickHotkeyActive)
        {
            return manualUiHoverEnabled && !lazyModeEnabled && !clickHotkeyActive;
        }

        internal static bool ShouldSuppressRegularClickForManualUiHoverMode(bool manualUiHoverEnabled, bool lazyModeEnabled, bool clickHotkeyActive)
        {
            return ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled, lazyModeEnabled, clickHotkeyActive);
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

            int delveBuffCharges = -1;
            foreach (var buff in _gameController.Player.Buffs)
            {
                if (buff != null && string.Equals(buff.Name, "delve_degen_buff", StringComparison.Ordinal))
                {
                    delveBuffCharges = buff.Charges;
                    break;
                }
            }

            if (delveBuffCharges < _settings.DarknessDebuffStacks.Value)
                yield break;

            float healthPercent = GetPlayerHealthPercent();
            float energyShieldPercent = GetPlayerEnergyShieldPercent();
            if (healthPercent > _settings.DelveFlareHealthThreshold.Value ||
                energyShieldPercent > _settings.DelveFlareEnergyShieldThreshold.Value)
                yield break;

            if (_state.InputHandler?.CanClick(_gameController, false, IsRitualActive()) != true)
                yield break;

            Keyboard.KeyPress(_settings.DelveFlareHotkey.Value, 50);
            _errorHandler.LogMessage($"Used delve flare (buff charges: {delveBuffCharges}, health: {healthPercent:F1}%, es: {energyShieldPercent:F1}%)", 5);
            yield return new WaitTime(1000);
        }

        internal float GetPlayerHealthPercent()
        {
#if RUNTIME_EXILECORE
            try
            {
                var player = _gameController?.Player;
                if (player == null)
                    return 100f;

                var life = player.GetComponent<ExileCore.PoEMemory.Components.Life>();
                if (life == null || life.Health.Max == 0)
                    return 100f;

                return (float)life.Health.Current / life.Health.Max * 100f;
            }
            catch
            {
                return 100f;
            }
#else
            return 100f;
#endif
        }

        internal float GetPlayerEnergyShieldPercent()
        {
#if RUNTIME_EXILECORE
            try
            {
                var player = _gameController?.Player;
                if (player == null)
                    return 100f;

                var life = player.GetComponent<ExileCore.PoEMemory.Components.Life>();
                if (life == null || life.EnergyShield.Max == 0)
                    return 100f;

                return (float)life.EnergyShield.Current / life.EnergyShield.Max * 100f;
            }
            catch
            {
                return 100f;
            }
#else
            return 100f;
#endif
        }
    }
}

