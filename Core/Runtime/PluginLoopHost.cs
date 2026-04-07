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

            _state.Runtime.AreaBlockedUiRefreshCoroutine = new Coroutine(MainAreaBlockedUiRefreshCoroutine(), plugin, "ClickIt.BlockedUiRefresh", true);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.AreaBlockedUiRefreshCoroutine);
            _state.Runtime.AreaBlockedUiRefreshCoroutine.Priority = CoroutinePriority.Normal;

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

        private IEnumerator MainAreaBlockedUiRefreshCoroutine()
        {
            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                _state.Services.AreaService?.UpdateScreenAreas(_gameController, forceBlockedUiRefresh: true);

                int waitMs = Math.Max(50, _settings.BlockedUiRefreshIntervalMs?.Value ?? AreaBlockedSnapshotProvider.DefaultBlockedUiRectanglesRefreshIntervalMs);
                yield return new WaitTime(waitMs);
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

            bool hotkeyActive = PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(_state.Services);
            PluginManualUiHoverModeDecision manualUiHoverMode = PluginClickRuntimeStateEvaluator.ResolveManualUiHoverMode(
                _settings.ClickOnManualUiHoverOnly.Value,
                _settings.LazyMode.Value,
                hotkeyActive);
            if (manualUiHoverMode.ShouldSuppressRegularClick)
            {
                _state.Runtime.WorkFinished = true;
                yield break;
            }

            double avgClickTime = _state.Services.PerformanceMonitor.GetAverageTiming(TimingChannel.Click);

            PluginLazyModeContextSnapshot lazyModeContext = ResolveRegularClickLazyModeContext(hotkeyActive);
            PluginClickRuntimeStateSnapshot runtimeState = PluginClickRuntimeStateEvaluator.ResolveSnapshot(
                _settings,
                _state.Services.InputHandler,
                _gameController,
                lazyModeContext);

            double frequencyTarget = PluginClickRuntimeStateEvaluator.ResolveFrequencyTarget(_settings, runtimeState);
            double targetTime = GetTargetTime(frequencyTarget, avgClickTime);
            PluginClickGateDecision gateDecision = PluginClickRuntimeStateEvaluator.ResolveRegularClickGateDecision(
                _state.Services.InputHandler,
                _gameController,
                runtimeState,
                hotkeyActive,
                _state.Runtime.Timer.ElapsedMilliseconds,
                targetTime);
            if (gateDecision.IsBlocked)
            {
                if (gateDecision.ShouldCancelOffscreenPathing)
                {
                    runtimeHost.CancelOffscreenPathingState();
                }

                if (_settings.DebugMode?.Value == true)
                {
                    long now = Environment.TickCount64;
                    if (now - _lastCanClickFailureLogTimestampMs >= 500)
                    {
                        _lastCanClickFailureLogTimestampMs = now;
                        int labelCount = lazyModeContext.Labels?.Count ?? 0;
                        _errorHandler.LogMessage($"[ClickLogic] blocked: reason='{gateDecision.FailureReason}', readyByTime={gateDecision.ReadyByTime}, hasRestricted={runtimeState.HasLazyModeRestrictedItems}, ritualActive={runtimeState.IsRitualActive}, labels={labelCount}", 10);
                    }
                }

                _state.Runtime.WorkFinished = true;
                yield break;
            }

            long clickSequenceBefore = _state.Services.LockedInteractionDispatcher?.GetSuccessfulClickSequence() ?? 0;
            _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Click);
            yield return runtimeHost.ProcessRegularClick();
            _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            long clickSequenceAfter = _state.Services.LockedInteractionDispatcher?.GetSuccessfulClickSequence() ?? 0;
            if (PluginClickRuntimeStateEvaluator.ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter))
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

            bool hotkeyActive = PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(_state.Services);
            PluginManualUiHoverModeDecision manualUiHoverMode = PluginClickRuntimeStateEvaluator.ResolveManualUiHoverMode(
                _settings.ClickOnManualUiHoverOnly.Value,
                _settings.LazyMode.Value,
                hotkeyActive);
            if (!manualUiHoverMode.ShouldRunCoroutine)
                yield break;

            PluginClickRuntimeStateSnapshot runtimeState = ResolveRitualAwareRuntimeState();
            if (runtimeState.IsRitualActive)
                yield break;

            double avgClickTime = _state.Services.PerformanceMonitor.GetAverageTiming(TimingChannel.Click);
            double targetTime = GetTargetTime(_settings.ClickFrequencyTarget.Value, avgClickTime);
            PluginClickReadinessDecision gateDecision = PluginClickRuntimeStateEvaluator.ResolveManualUiHoverGateDecision(
                _state.Services.InputHandler,
                _gameController,
                _state.Runtime.Timer.ElapsedMilliseconds,
                targetTime);
            if (gateDecision.IsBlocked)
                yield break;

            IReadOnlyList<LabelOnGround>? labels = _state.Services.CachedLabels?.Value;
            long clickSequenceBefore = _state.Services.LockedInteractionDispatcher?.GetSuccessfulClickSequence() ?? 0;

            _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Click);
            bool clicked = runtimeHost.TryClickManualUiHoverLabel(labels);
            _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            long clickSequenceAfter = _state.Services.LockedInteractionDispatcher?.GetSuccessfulClickSequence() ?? 0;
            if (clicked && PluginClickRuntimeStateEvaluator.ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter))
            {
                _state.Runtime.Timer.Restart();
            }
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

            PluginClickRuntimeStateSnapshot runtimeState = ResolveRitualAwareRuntimeState();
            if (_state.Services.InputHandler?.CanClick(_gameController, false, runtimeState.IsRitualActive) != true)
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