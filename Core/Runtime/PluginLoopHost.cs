namespace ClickIt.Core.Runtime
{
    public partial class PluginLoopHost(
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

        private double GetTargetTime(double frequencyTarget, double averageTiming)
        {
            return (frequencyTarget - averageTiming) + _state.Random.Next(0, 6);
        }

        public void StartCoroutines(BaseSettingsPlugin<ClickItSettings> plugin)
        {
            _state.Runtime.AltarCoroutine = new Coroutine(MainScanForAltarsLogic(), plugin, "ClickIt.ScanForAltarsLogic", false);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.AltarCoroutine);
            _state.Runtime.AltarCoroutine.Priority = CoroutinePriority.Normal;

            _state.Runtime.ClickLabelCoroutine = new Coroutine(MainClickLabelCoroutine(), plugin, "ClickIt.ClickLogic", false);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.ClickLabelCoroutine);
            _state.Runtime.ClickLabelCoroutine.Priority = CoroutinePriority.High;

            _state.Runtime.ManualUiHoverCoroutine = new Coroutine(MainManualUiHoverClickCoroutine(), plugin, "ClickIt.ManualUiHoverLogic", false);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.ManualUiHoverCoroutine);
            _state.Runtime.ManualUiHoverCoroutine.Priority = CoroutinePriority.High;

            _state.Runtime.DelveFlareCoroutine = new Coroutine(FlareCoroutine(), plugin, "ClickIt.DelveFlareLogic", true);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.DelveFlareCoroutine);
            _state.Runtime.DelveFlareCoroutine.Priority = CoroutinePriority.Normal;
        }

        private IEnumerator MainScanForAltarsLogic()
        {
            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                yield return ScanForAltarsLogic();
            }
        }

        private IEnumerator ScanForAltarsLogic()
        {
            if (_state.Runtime.IsShuttingDown || _state.Services.PerformanceMonitor == null) yield break;

            _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Altar);
            _state.Services.AltarService?.ProcessAltarScanningLogic();
            _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Altar);

            _state.Runtime.AltarCoroutine?.Pause();
        }

        private IEnumerator MainClickLabelCoroutine()
        {
            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                yield return ClickLabel();
            }
        }

        private IEnumerator ClickLabel()
        {
            var runtimeHost = _state.Rendering.ClickRuntimeHost;

            if (_state.Runtime.IsShuttingDown || _state.Services.PerformanceMonitor == null || runtimeHost == null) yield break;

            bool hotkeyActive = _state.Services.InputHandler?.IsClickHotkeyPressed(_state.Services.CachedLabels, _state.Services.LabelFilterPort) == true;
            if (ShouldSuppressRegularClickForManualUiHoverMode(_settings.ClickOnManualUiHoverOnly.Value, _settings.LazyMode.Value, hotkeyActive))
            {
                _state.Runtime.WorkFinished = true;
                yield break;
            }

            double avgClickTime = _state.Services.PerformanceMonitor.GetAverageTiming(TimingChannel.Click);

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
            bool readyByTime = _state.Runtime.Timer.ElapsedMilliseconds >= targetTime;
            bool canClick = _state.Services.InputHandler?.CanClick(_gameController, hasLazyModeRestrictedItemsOnScreen, isRitualActive) == true;
            if (!readyByTime || !canClick)
            {
                bool hotkeyHeld = _state.Services.InputHandler == null || hotkeyActive;
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
                            : (_state.Services.InputHandler?.GetCanClickFailureReason(_gameController) ?? "Unknown click blocker");
                        int labelCount = cached?.Count ?? 0;
                        _errorHandler.LogMessage($"[ClickLogic] blocked: reason='{canClickReason}', readyByTime={readyByTime}, hasRestricted={hasLazyModeRestrictedItemsOnScreen}, ritualActive={isRitualActive}, labels={labelCount}", 10);
                    }
                }

                _state.Runtime.WorkFinished = true;
                yield break;
            }

            long clickSequenceBefore = _state.Services.InputHandler?.GetSuccessfulClickSequence() ?? 0;
            _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Click);
            yield return runtimeHost.ProcessRegularClick();
            _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            long clickSequenceAfter = _state.Services.InputHandler?.GetSuccessfulClickSequence() ?? 0;
            if (ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter))
            {
                _state.Runtime.Timer.Restart();
            }

            _state.Runtime.WorkFinished = true;
        }

        internal IEnumerator RunClickLabelStep()
            => ClickLabel();

        private IEnumerator MainManualUiHoverClickCoroutine()
        {
            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                yield return ProcessManualUiHoverClick();
                yield return new WaitTime(10);
            }
        }

        private IEnumerator ProcessManualUiHoverClick()
        {
            var runtimeHost = _state.Rendering.ClickRuntimeHost;

            if (_state.Runtime.IsShuttingDown || _state.Services.PerformanceMonitor == null || runtimeHost == null || _state.Services.InputHandler == null)
                yield break;

            bool hotkeyActive = _state.Services.InputHandler.IsClickHotkeyPressed(_state.Services.CachedLabels, _state.Services.LabelFilterPort);
            if (!ShouldRunManualUiHoverCoroutine(_settings.ClickOnManualUiHoverOnly.Value, _settings.LazyMode.Value, hotkeyActive))
                yield break;

            bool isRitualActive = GetCachedLazyModeContext(shouldEvaluateRitualState: true, shouldEvaluateRestrictedItems: false).IsRitualActive;
            if (isRitualActive)
                yield break;

            if (!_state.Services.InputHandler.CanClickWithoutInputState(_gameController))
                yield break;

            double avgClickTime = _state.Services.PerformanceMonitor.GetAverageTiming(TimingChannel.Click);
            double targetTime = GetTargetTime(_settings.ClickFrequencyTarget.Value, avgClickTime);
            if (_state.Runtime.Timer.ElapsedMilliseconds < targetTime)
                yield break;

            IReadOnlyList<LabelOnGround>? labels = _state.Services.CachedLabels?.Value;
            long clickSequenceBefore = _state.Services.InputHandler.GetSuccessfulClickSequence();

            _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Click);
            bool clicked = runtimeHost.TryClickManualUiHoverLabel(labels);
            _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            long clickSequenceAfter = _state.Services.InputHandler.GetSuccessfulClickSequence();
            if (clicked && ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter))
            {
                _state.Runtime.Timer.Restart();
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
            if (_state.Runtime.IsShuttingDown || _state.Services.PerformanceMonitor == null) yield break;

            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Flare);

                yield return ProcessFlare();

                _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Flare);

                yield return new WaitTime(100);
            }
        }

        private IEnumerator ProcessFlare()
        {
            if (!_settings.ClickDelveFlares || _gameController?.Player?.Buffs == null)
                yield break;

            int delveBuffCharges = PluginDelveFlarePolicy.FindDarknessDebuffCharges(_gameController.Player.Buffs);

            float healthPercent = GetPlayerHealthPercent();
            float energyShieldPercent = GetPlayerEnergyShieldPercent();
            if (!PluginDelveFlarePolicy.ShouldUseFlare(
                delveBuffCharges,
                _settings.DarknessDebuffStacks.Value,
                healthPercent,
                _settings.DelveFlareHealthThreshold.Value,
                energyShieldPercent,
                _settings.DelveFlareEnergyShieldThreshold.Value))
                yield break;

            if (_state.Services.InputHandler?.CanClick(_gameController, false, IsRitualActive()) != true)
                yield break;

            Keyboard.KeyPress(_settings.DelveFlareHotkeyBinding, 50);
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

                var life = player.GetComponent<Life>();
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

                var life = player.GetComponent<Life>();
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