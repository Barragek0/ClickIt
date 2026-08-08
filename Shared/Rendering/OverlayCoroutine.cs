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
                        _overlay.Refresh(_refreshContextFactory());
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
