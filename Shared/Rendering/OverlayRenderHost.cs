namespace ClickIt.Shared.Rendering
{
    /// <summary>
    /// The single overlay-rendering API. Register every overlay, StartAll on plugin enable,
    /// Render once per frame (gate → time → draw, record 0 when disabled), StopAll on shutdown.
    /// The host owns each refreshable overlay's coroutine + cadence, so no overlay or caller
    /// hand-rolls coroutines, Stopwatches, or gate-else timing blocks.
    /// </summary>
    public sealed class OverlayRenderHost
    {
        private readonly List<IOverlay> _overlays = [];
        private readonly List<OverlayCoroutine> _coroutines = [];

        public void Register(IOverlay overlay)
            => _overlays.Add(overlay);

        public void StartAll(
            BaseSettingsPlugin<ClickItSettings> plugin,
            Func<OverlayRefreshContext> refreshContextFactory,
            Func<bool> shouldContinue,
            PerformanceMonitor? performanceMonitor,
            Action<string>? logError)
        {
            foreach (IOverlay overlay in _overlays)
            {
                if (overlay.RefreshPolicy.Mode == OverlayRefreshMode.None)
                    continue;

                _coroutines.Add(new OverlayCoroutine(plugin, overlay, refreshContextFactory, shouldContinue, performanceMonitor, logError));
            }
        }

        public void Render(OverlayRenderContext context, PerformanceMonitor? performanceMonitor)
        {
            for (int i = 0; i < _overlays.Count; i++)
            {
                IOverlay overlay = _overlays[i];
                long start = Stopwatch.GetTimestamp();
                context.DrawQueue.CurrentSection = overlay.Section;
                if (overlay.IsEnabled(context.Settings))
                    overlay.Draw(context);
                context.DrawQueue.CurrentSection = RenderSection.Unknown;

                performanceMonitor?.AccumulateRenderSectionTiming(overlay.Section, GetElapsedMs(start));
            }
        }

        public void StopAll()
        {
            foreach (OverlayCoroutine coroutine in _coroutines)
                coroutine.Stop();

            _coroutines.Clear();
        }

        private static double GetElapsedMs(long startTimestamp)
            => StopwatchMath.ElapsedMs(startTimestamp);
    }
}
