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
        private const int ClickIdleWaitMs = 50;
        private bool _lastClickTickDidWork;
        // Set to the remaining frequency-target time when the gate is only waiting on the click
        // timer, so the observed click interval tracks the target instead of overshooting by up to
        // the coarse idle-wait window. Falls back to ClickIdleWaitMs otherwise.
        private int _clickIdleWaitMs = ClickIdleWaitMs;
        private double _clickTargetTimeCorrectionMs;
        private bool _lastClickTimerRestarted;

        // Delve-flare decision cache: per-player-address inputs (darkness-debuff charges, health/ES
        // percent) refreshed at most every FlareCacheWindowMs to avoid re-materializing Player.Buffs.
        private long _flarePlayerOwnerAddress;
        private int _cachedFlareCharges = -1;
        private float _cachedFlareHealth = 100f;
        private float _cachedFlareEnergyShield = 100f;
        private long _flareCacheAtMs;
        private const long FlareCacheWindowMs = 200;

        private static double GetElapsedMs(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        private void RecordProcessing(ProcessingSection section, long startTimestamp, long startAllocatedBytes, double subtractMs = 0)
        {
            PerformanceMonitor? pm = _state.Services.PerformanceMonitor;
            if (pm == null)
                return;
            pm.RecordProcessingTiming(section, SystemMath.Max(0, GetElapsedMs(startTimestamp) - subtractMs));
            pm.RecordAllocation(section, GC.GetAllocatedBytesForCurrentThread() - startAllocatedBytes);
        }

        private double ResolveClickTargetTime(double frequencyTarget)
        {
            double avgClickTime = _state.Services.PerformanceMonitor?.GetAverageTiming(TimingChannel.Click) ?? 0;
            return SystemMath.Clamp(
                frequencyTarget - avgClickTime + _state.Random.Next(0, 6) - _clickTargetTimeCorrectionMs,
                1,
                SystemMath.Max(1, frequencyTarget));
        }

        private void RecordClickStartDelay(long elapsedMilliseconds, double frequencyTarget)
        {
            double avgClickTime = _state.Services.PerformanceMonitor?.GetAverageTiming(TimingChannel.Click) ?? 0;
            double desiredDelay = frequencyTarget - avgClickTime;
            if (desiredDelay <= 0)
                return;

            double error = elapsedMilliseconds - desiredDelay;
            _clickTargetTimeCorrectionMs = SystemMath.Clamp(
                _clickTargetTimeCorrectionMs + (error * 0.5),
                0,
                SystemMath.Max(0, desiredDelay - 1));
        }

        private long GetSuccessfulClickSequence()
            => _state.Services.LockedInteractionDispatcher?.GetSuccessfulClickSequence() ?? 0;

        private void RestartClickTimerAfterSuccessfulInteraction(long clickSequenceBefore, bool interactionSucceeded = true)
        {
            if (!interactionSucceeded)
                return;

            long clickSequenceAfter = GetSuccessfulClickSequence();
            if (PluginClickRuntimeStateEvaluator.ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter))
            {
                _state.Runtime.Timer.Restart();
                _lastClickTimerRestarted = true;
            }

        }

        public void StartCoroutines(BaseSettingsPlugin<ClickItSettings> plugin)
        {
            _state.Runtime.AltarCoroutine = new Coroutine(MainScanForAltarsLogic(), plugin, PluginCoroutineNames.AltarScan, false);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.AltarCoroutine);
            _state.Runtime.AltarCoroutine.Priority = CoroutinePriority.Normal;

            _state.Runtime.AreaBlockedUiRefreshCoroutine = new Coroutine(MainAreaBlockedUiRefreshCoroutine(), plugin, PluginCoroutineNames.BlockedUiRefresh, true);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.AreaBlockedUiRefreshCoroutine);
            _state.Runtime.AreaBlockedUiRefreshCoroutine.Priority = CoroutinePriority.Normal;

            _state.Runtime.ClickLabelCoroutine = new Coroutine(MainClickLabelCoroutine(), plugin, PluginCoroutineNames.ClickLogic, false);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.ClickLabelCoroutine);
            _state.Runtime.ClickLabelCoroutine.Priority = CoroutinePriority.High;

            _state.Runtime.ManualUiHoverCoroutine = new Coroutine(MainManualUiHoverClickCoroutine(), plugin, PluginCoroutineNames.ManualUiHover, false);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.ManualUiHoverCoroutine);
            _state.Runtime.ManualUiHoverCoroutine.Priority = CoroutinePriority.High;

            _state.Runtime.DelveFlareCoroutine = new Coroutine(FlareCoroutine(), plugin, PluginCoroutineNames.DelveFlare, true);
            _ = ExileCoreApi.ParallelRunner.Run(_state.Runtime.DelveFlareCoroutine);
            _state.Runtime.DelveFlareCoroutine.Priority = CoroutinePriority.Normal;

            // Overlay API: owns the per-overlay refresh coroutines (cadence + timing + error routing).
            _state.Rendering.OverlayRenderHost?.StartAll(
                plugin,
                () => new OverlayRefreshContext(
                    _gameController,
                    _state.Services.CachedLabels?.Value,
                    _gameController.Window.GetWindowRectangleTimeCache,
                    _settings),
                () => _settings.Enable && !_state.Runtime.IsShuttingDown,
                _state.Services.PerformanceMonitor,
                message => _errorHandler.LogError(message));
        }

        private IEnumerator MainScanForAltarsLogic()
        {
            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
                yield return Guarded(ScanForAltarsLogic, "AltarScan");

        }

        // Catches exceptions escaping a plugin coroutine step and logs them through
        // the plugin ErrorHandler (Recent Errors + game log) instead of letting
        // ExileCore's runner swallow them into the game log only.
        private IEnumerator Guarded(Func<IEnumerator> step, string name)
        {
            bool failed = false;
            IEnumerator inner = step();
            while (!failed)
            {
                bool hasNext;
                try
                {
                    hasNext = inner.MoveNext();
                }
                catch (Exception ex)
                {
                    _errorHandler.LogError($"[{name}] {ex}");
                    failed = true;
                    break;
                }
                if (!hasNext)
                    break;
                yield return inner.Current;
            }
            if (failed)
                yield return new WaitTime(500);
        }

        private IEnumerator MainAreaBlockedUiRefreshCoroutine()
        {
            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                try
                {
                    // Let the scheduler honor BlockedUiRefreshIntervalMs; forcing here would bypass
                    // the documented refresh interval and rebuild blocked rects redundantly.
                    long start = Stopwatch.GetTimestamp();
                    long allocStart = GC.GetAllocatedBytesForCurrentThread();
                    _state.Services.AreaService?.UpdateScreenAreas(_gameController);
                    RecordProcessing(ProcessingSection.AreaBlockedUi, start, allocStart);
                }
                catch (Exception ex)
                {
                    _errorHandler.LogError($"[BlockedUiRefresh] {ex}");
                }

                int waitMs = SystemMath.Max(50, _settings.BlockedUiRefreshIntervalMs?.Value ?? AreaBlockedSnapshotProvider.DefaultBlockedUiRectanglesRefreshIntervalMs);
                yield return new WaitTime(waitMs);
            }
        }

        private IEnumerator ScanForAltarsLogic()
        {
            if (_state.Runtime.IsShuttingDown || _state.Services.PerformanceMonitor == null) yield break;

            _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Altar);
            long start = Stopwatch.GetTimestamp();
            long allocStart = GC.GetAllocatedBytesForCurrentThread();
            _state.Services.AltarService?.ProcessAltarScanningLogic();
            RecordProcessing(ProcessingSection.Altar, start, allocStart);
            _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Altar);

            _state.Runtime.AltarCoroutine?.Pause();
        }

        private IEnumerator MainClickLabelCoroutine()
        {
            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                yield return Guarded(ClickLabel, "ClickLabel");
                if (!_lastClickTickDidWork)
                    yield return new WaitTime(_clickIdleWaitMs);
            }
        }

        internal IEnumerator ClickLabel()
        {
            ClickAutomationPort? clickPort = _state.Services.ClickAutomationPort;

            if (_state.Runtime.IsShuttingDown || _state.Services.PerformanceMonitor == null || clickPort == null) yield break;

            _lastClickTickDidWork = false;

            bool hotkeyActive = PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(_state.Services);
            PluginManualUiHoverModeDecision manualUiHoverMode = ResolveManualUiHoverMode(hotkeyActive);
            if (manualUiHoverMode.ShouldRunCoroutine)
            {
                _state.Runtime.WorkFinished = true;
                yield break;
            }

            (PluginLazyModeContextSnapshot lazyModeContext, PluginClickRuntimeStateSnapshot runtimeState) = ResolveRegularClickRuntimeState(hotkeyActive);

            PluginClickFrequencyTargetDecision frequencyTarget = PluginClickRuntimeStateEvaluator.ResolveFrequencyTargetDecision(_settings, runtimeState);
            double targetTime = ResolveClickTargetTime(frequencyTarget.TargetIntervalMs);
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
                    clickPort.CancelOffscreenPathingState();

                // When only the frequency timer is gating (click is possible, just not due yet),
                // sleep exactly the remaining time instead of the coarse idle wait so the observed
                // click interval tracks the target.
                _clickIdleWaitMs = !gateDecision.ReadyByTime && gateDecision.CanClick
                    ? SystemMath.Clamp((int)SystemMath.Ceiling(targetTime - _state.Runtime.Timer.ElapsedMilliseconds), 1, ClickIdleWaitMs)
                    : ClickIdleWaitMs;

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
            _clickIdleWaitMs = ClickIdleWaitMs;

            if (_lastClickTimerRestarted)
            {
                _lastClickTimerRestarted = false;
                if (_state.Runtime.Timer.ElapsedMilliseconds < frequencyTarget.TargetIntervalMs + 100)
                    RecordClickStartDelay(_state.Runtime.Timer.ElapsedMilliseconds, frequencyTarget.TargetIntervalMs);
            }

            // Harvest labels are processed in the render path
            // (PluginRenderHost) so the overlay renders correctly and the
            // click path reads the decision via GetLabelToClick without
            // needing to reprocess labels.

            long clickSequenceBefore = GetSuccessfulClickSequence();
            _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Click);
            long clickStart = Stopwatch.GetTimestamp();
            long clickAllocStart = GC.GetAllocatedBytesForCurrentThread();
            ClickPipelineTiming.ResetSleepTime();
            yield return clickPort.ProcessRegularClick();
            double clickSleepMs = ClickPipelineTiming.ConsumeSleepTimeMs();
            _state.Services.PerformanceMonitor.RecordClickSleepTiming(clickSleepMs);
            RecordProcessing(ProcessingSection.Click, clickStart, clickAllocStart, clickSleepMs);
            _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            _lastClickTickDidWork = clickPort.LastClickTickWasActionable;

            RestartClickTimerAfterSuccessfulInteraction(clickSequenceBefore);

            _state.Runtime.WorkFinished = true;
        }

        private IEnumerator MainManualUiHoverClickCoroutine()
        {
            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                yield return Guarded(ProcessManualUiHoverClick, "ManualUiHoverClick");
                yield return new WaitTime(10);
            }
        }

        private IEnumerator ProcessManualUiHoverClick()
        {
            ClickAutomationPort? clickPort = _state.Services.ClickAutomationPort;

            if (_state.Runtime.IsShuttingDown || _state.Services.PerformanceMonitor == null || clickPort == null || _state.Services.InputHandler == null)
                yield break;

            bool hotkeyActive = PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(_state.Services);
            PluginManualUiHoverModeDecision manualUiHoverMode = ResolveManualUiHoverMode(hotkeyActive);
            if (!manualUiHoverMode.ShouldRunCoroutine)
                yield break;

            PluginClickRuntimeStateSnapshot runtimeState = ResolveRitualAwareRuntimeState();
            if (runtimeState.IsRitualActive)
                yield break;

            double targetTime = ResolveClickTargetTime(_settings.ClickFrequencyTarget.Value);
            PluginClickReadinessDecision gateDecision = PluginClickRuntimeStateEvaluator.ResolveManualUiHoverGateDecision(
                _state.Services.InputHandler,
                _gameController,
                _state.Runtime.Timer.ElapsedMilliseconds,
                targetTime);
            if (gateDecision.IsBlocked)
                yield break;

            IReadOnlyList<LabelOnGround>? labels = _state.Services.CachedLabels?.Value;
            long clickSequenceBefore = GetSuccessfulClickSequence();

            _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Click);
            long hoverStart = Stopwatch.GetTimestamp();
            long hoverAllocStart = GC.GetAllocatedBytesForCurrentThread();
            ClickPipelineTiming.ResetSleepTime();
            bool clicked = clickPort.TryClickManualUiHoverLabel(labels);
            double hoverSleepMs = ClickPipelineTiming.ConsumeSleepTimeMs();
            _state.Services.PerformanceMonitor.RecordClickSleepTiming(hoverSleepMs);
            RecordProcessing(ProcessingSection.ManualUiHover, hoverStart, hoverAllocStart, hoverSleepMs);
            _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Click);

            RestartClickTimerAfterSuccessfulInteraction(clickSequenceBefore, clicked);
        }

        private IEnumerator FlareCoroutine()
        {
            if (_state.Runtime.IsShuttingDown || _state.Services.PerformanceMonitor == null) yield break;

            while (_settings.Enable && !_state.Runtime.IsShuttingDown)
            {
                try
                {
                    _state.Services.PerformanceMonitor.StartCoroutineTiming(TimingChannel.Flare);
                }
                catch (Exception ex)
                {
                    _errorHandler.LogError($"[FlareCoroutine] {ex}");
                }

                long flareStart = Stopwatch.GetTimestamp();
                long flareAllocStart = GC.GetAllocatedBytesForCurrentThread();
                yield return Guarded(ProcessFlare, "Flare");
                RecordProcessing(ProcessingSection.Flare, flareStart, flareAllocStart);

                try
                {
                    _state.Services.PerformanceMonitor.StopCoroutineTiming(TimingChannel.Flare);
                }
                catch (Exception ex)
                {
                    _errorHandler.LogError($"[FlareCoroutine] {ex}");
                }

                yield return new WaitTime(100);
            }
        }

        private IEnumerator ProcessFlare()
        {
            if (!_settings.ClickDelveFlares)
                yield break;

            long now = Environment.TickCount64;
            Entity? player = _gameController?.Player;
            if (player == null)
                yield break;

            // Player.Buffs materializes every buff wrapper and FindDarknessDebuffCharges reads each
            // buff's Name/Charges over the obfuscated game types — the dominant flare cost at its
            // 100ms cadence. Cache the decision inputs per player address with a short window; the
            // darkness-debuff decision tolerates 200ms staleness.
            if (now - _flareCacheAtMs >= FlareCacheWindowMs || player.Address != _flarePlayerOwnerAddress)
            {
                _flarePlayerOwnerAddress = player.Address;
                _flareCacheAtMs = now;
                _cachedFlareCharges = PluginDelveFlarePolicy.FindDarknessDebuffCharges(player.Buffs);
                _cachedFlareHealth = GetPlayerHealthPercent();
                _cachedFlareEnergyShield = GetPlayerEnergyShieldPercent();
            }

            if (!PluginDelveFlarePolicy.ShouldUseFlare(
                _cachedFlareCharges,
                _settings.DarknessDebuffStacks.Value,
                _cachedFlareHealth,
                _settings.DelveFlareHealthThreshold.Value,
                _cachedFlareEnergyShield,
                _settings.DelveFlareEnergyShieldThreshold.Value))
                yield break;

            PluginClickRuntimeStateSnapshot runtimeState = ResolveRitualAwareRuntimeState();
            if (_state.Services.InputHandler?.CanClick(_gameController, false, runtimeState.IsRitualActive) != true)
                yield break;

            Keyboard.KeyPress(_settings.DelveFlareHotkeyBinding, 50);
            _errorHandler.LogMessage($"Used delve flare (buff charges: {_cachedFlareCharges}, health: {_cachedFlareHealth:F1}%, es: {_cachedFlareEnergyShield:F1}%)", 5);
            yield return new WaitTime(1000);
        }

        internal float GetPlayerHealthPercent()
        {
#if RUNTIME_EXILECORE
            try
            {
                Entity? player = _gameController?.Player;
                if (player == null)
                    return 100f;

                Life life = player.GetComponent<Life>();
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
                Entity? player = _gameController?.Player;
                if (player == null)
                    return 100f;

                Life life = player.GetComponent<Life>();
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