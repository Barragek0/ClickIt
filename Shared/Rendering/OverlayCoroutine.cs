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
        private readonly Func<OverlayRefreshContext> _refreshContextFactory;
        private readonly Func<bool> _shouldContinue;
        private readonly PerformanceMonitor? _performanceMonitor;
        private readonly Action<string>? _logError;
        private readonly OverlayRefreshPolicy _policy;

        public OverlayCoroutine(
            BaseSettingsPlugin<ClickItSettings> plugin,
            IOverlay overlay,
            Func<OverlayRefreshContext> refreshContextFactory,
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
                // Resolve the refresh context (which can trigger the 50ms label read-model scan via
                // CachedLabels.Value) once, so the Enable gate below and the refresh share one scan.
                OverlayRefreshContext context;
                try { context = _refreshContextFactory(); }
                catch { context = default; }

                // Idle-yield while the Enable master switch is off instead of terminating: a
                // finished coroutine can never be resumed, so terminating here would leave this
                // overlay's refresh dead after the user toggles Enable back on (until a reload).
                if (!context.Settings.Enable)
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

                    long start = Stopwatch.GetTimestamp();
                    long allocStart = GC.GetAllocatedBytesForCurrentThread();
                    try
                    {
                        using (new DlrReadScope(_overlay.ProcessingSection))
                            _overlay.Refresh(context);
                    }
                    finally
                    {
                        if (_overlay.ProcessingSection != ProcessingSection.Unknown)
                        {
                            _performanceMonitor?.RecordProcessingTiming(_overlay.ProcessingSection, GetElapsedMs(start));
                            _performanceMonitor?.RecordAllocation(_overlay.ProcessingSection, GC.GetAllocatedBytesForCurrentThread() - allocStart);
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

        public void Pause()
            => _coroutine.Pause();

        public void Resume()
            => _coroutine.Resume();

        public void Stop()
            => _coroutine.Done();

        public void Dispose()
            => _coroutine.Done();

        private static double GetElapsedMs(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}
