namespace ClickIt.Shared.Rendering
{
    /// <summary>
    /// Owns one overlay's refresh coroutine: cadence (from the refresh policy), coroutine
    /// timing, and error routing. Mirrors the PluginLoopHost Guarded pattern — the yield
    /// stays outside the try/catch (CS1626) and failures back off to 500ms.
    /// </summary>
    public sealed class OverlayCoroutine : IDisposable
    {
        private readonly Coroutine _coroutine;
        private readonly IOverlay _overlay;
        private readonly Func<IOverlay, OverlayRefreshContext> _refreshContextFactory;
        private readonly Func<bool> _shouldContinue;
        private readonly PerformanceMonitor? _performanceMonitor;
        private readonly Action<string>? _logError;
        private readonly OverlayRefreshPolicy _policy;

        public OverlayCoroutine(
            BaseSettingsPlugin<ClickItSettings> plugin,
            IOverlay overlay,
            Func<IOverlay, OverlayRefreshContext> refreshContextFactory,
            Func<bool> shouldContinue,
            PerformanceMonitor? performanceMonitor,
            Action<string>? logError)
        {
            _overlay = overlay;
            _refreshContextFactory = refreshContextFactory;
            _shouldContinue = shouldContinue;
            _performanceMonitor = performanceMonitor;
            _logError = logError;
            _policy = overlay.RefreshPolicy;

            _coroutine = new Coroutine(Run(), plugin, PluginCoroutineNames.OverlayRefresh(overlay.Name), true);
            _ = ExileCoreApi.ParallelRunner.Run(_coroutine);
            _coroutine.Priority = CoroutinePriority.Normal;
        }

        private IEnumerator Run()
        {
            while (_shouldContinue())
            {
                // Resolve the refresh context (which can trigger the 50ms label read-model scan via CachedLabels.Value) once, so the Enable gate below and the refresh share one scan.
                OverlayRefreshContext context;
                try { context = _refreshContextFactory(_overlay); }
                catch (Exception ex)
                {
                    _logError?.Invoke($"[{_overlay.Name}RefreshContext] {ex}");
                    context = default;
                }

                // Idle-yield while the Enable master switch is off instead of terminating: a finished coroutine can never be resumed, so terminating here would leave this overlay's refresh dead after the user toggles Enable back on (until a reload).
                if (context.Settings == null || !context.Settings.Enable)
                {
                    yield return new WaitTime(250);
                    continue;
                }

                bool failed = false;
                try
                {
                    TimingChannel? channel = _overlay.RefreshTimingChannel;
                    if (channel is { } activeChannel)
                        _performanceMonitor?.StartCoroutineTiming(activeChannel);

                    ProcessingSection section = _overlay.ProcessingSection;
                    switch (section)
                    {
                        case ProcessingSection.Blight:
                            _performanceMonitor?.MarkInterval(IntervalKind.Blight);
                            break;
                        case ProcessingSection.Ultimatum:
                            _performanceMonitor?.MarkInterval(IntervalKind.Ultimatum);
                            break;
                    }

                    PerformanceMonitor? monitor = section != ProcessingSection.Unknown ? _performanceMonitor : null;
                    long start = monitor != null ? Stopwatch.GetTimestamp() : 0;
                    long allocStart = monitor != null ? GC.GetAllocatedBytesForCurrentThread() : 0;
                    try
                    {
                        using (new DlrReadScope(section))
                            _overlay.Refresh(context);
                    }
                    finally
                    {
                        if (monitor != null)
                        {
                            monitor.RecordProcessingTiming(section, GetElapsedMs(start));
                            monitor.RecordAllocation(section, GC.GetAllocatedBytesForCurrentThread() - allocStart);
                        }
                        if (channel is { } stoppedChannel)
                            _performanceMonitor?.StopCoroutineTiming(stoppedChannel);
                    }
                }
                catch (Exception ex)
                {
                    _logError?.Invoke($"[{_overlay.Name}Refresh] {ex}");
                    failed = true;
                }

                yield return new WaitTime(failed ? 500 : _policy.IntervalMs);
            }
        }

        public void Stop()
            => _coroutine.Done();

        public void Dispose()
            => _coroutine.Done();

        private static double GetElapsedMs(long startTimestamp)
            => StopwatchMath.ElapsedMs(startTimestamp);
    }
}
