namespace ClickIt.Features.Observability
{
    internal readonly record struct FpsMetricsSnapshot(
        double Current,
        double Average,
        double Max);

    internal readonly record struct TimingMetricsSnapshot(
        double LastMs,
        double AverageMs,
        double MaxMs,
        long SampleCount);

    internal readonly record struct PerformanceMetricsSnapshot(
        FpsMetricsSnapshot Fps,
        TimingMetricsSnapshot Render,
        TimingMetricsSnapshot LazyMode,
        TimingMetricsSnapshot DebugOverlay,
        TimingMetricsSnapshot AltarOverlay,
        TimingMetricsSnapshot UltimatumOverlay,
        TimingMetricsSnapshot StrongboxOverlay,
        TimingMetricsSnapshot PathfindingOverlay,
        TimingMetricsSnapshot TextFlush,
        TimingMetricsSnapshot FrameFlush,
        TimingMetricsSnapshot AltarCoroutine,
        TimingMetricsSnapshot ClickCoroutine,
        TimingMetricsSnapshot FlareCoroutine,
        double ClickTargetIntervalMs,
        double AverageSuccessfulClickTimingMs,
        double AverageClickIntervalMs)
    {
        public TimingMetricsSnapshot GetRenderSection(RenderSection section)
            => section switch
            {
                RenderSection.LazyMode => LazyMode,
                RenderSection.DebugOverlay => DebugOverlay,
                RenderSection.AltarOverlay => AltarOverlay,
                RenderSection.UltimatumOverlay => UltimatumOverlay,
                RenderSection.StrongboxOverlay => StrongboxOverlay,
                RenderSection.PathfindingOverlay => PathfindingOverlay,
                RenderSection.TextFlush => TextFlush,
                RenderSection.FrameFlush => FrameFlush,
                RenderSection.Unknown => default,
                _ => default,
            };

        public TimingMetricsSnapshot GetCoroutineTiming(TimingChannel channel)
            => channel switch
            {
                TimingChannel.Altar => AltarCoroutine,
                TimingChannel.Click => ClickCoroutine,
                TimingChannel.Flare => FlareCoroutine,
                TimingChannel.Render => Render,
                TimingChannel.Unknown => default,
                _ => default,
            };
    }
}