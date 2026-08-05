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
        long SampleCount,
        double AveragePeriodMs = 0)
    {
        // Fraction of wall time the coroutine was executing (AverageMs of run / run period).
        public double DutyCyclePercent
            => AveragePeriodMs > 0 ? AverageMs / AveragePeriodMs * 100 : 0;

        // Multiplier converting a per-run ms value to per-frame ms (runs per second / frames per second).
        public double PerFrameScale(double fps)
            => AveragePeriodMs > 0 && fps > 0 ? 1000.0 / AveragePeriodMs / fps : 0;

        // Cost normalized to one frame so background coroutines are comparable to the render table.
        public double PerFrameMs(double fps)
            => PerFrameScale(fps) * AverageMs;
    }

    internal readonly record struct PerformanceMetricsSnapshot(
        FpsMetricsSnapshot Fps,
        TimingMetricsSnapshot Render,
        TimingMetricsSnapshot LazyMode,
        TimingMetricsSnapshot DebugOverlay,
        TimingMetricsSnapshot AltarOverlay,
        TimingMetricsSnapshot UltimatumOverlay,
        TimingMetricsSnapshot StrongboxOverlay,
        TimingMetricsSnapshot PathfindingOverlay,
        TimingMetricsSnapshot HarvestOverlay,
        TimingMetricsSnapshot BlightOverlay,
        TimingMetricsSnapshot TextFlush,
        TimingMetricsSnapshot FrameFlush,
        TimingMetricsSnapshot AltarCoroutine,
        TimingMetricsSnapshot ClickCoroutine,
        TimingMetricsSnapshot FlareCoroutine,
        TimingMetricsSnapshot BlightCoroutine,
        TimingMetricsSnapshot UltimatumCoroutine,
        TimingMetricsSnapshot LabelOverlayCoroutine,
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
                RenderSection.HarvestOverlay => HarvestOverlay,
                RenderSection.BlightOverlay => BlightOverlay,
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
                TimingChannel.Blight => BlightCoroutine,
                TimingChannel.Ultimatum => UltimatumCoroutine,
                TimingChannel.LabelOverlay => LabelOverlayCoroutine,
                TimingChannel.Render => Render,
                TimingChannel.Unknown => default,
                _ => default,
            };

        // Combined background-coroutine cost: last/avg are summed across every channel with samples,
        // max is the worst single spike. Mirrors the Render summary line in the debug overlay.
        public TimingMetricsSnapshot CoroutinesTotal
        {
            get
            {
                double last = 0, avg = 0, max = 0;
                long channels = 0;
                Aggregate(AltarCoroutine);
                Aggregate(ClickCoroutine);
                Aggregate(FlareCoroutine);
                Aggregate(BlightCoroutine);
                Aggregate(UltimatumCoroutine);
                Aggregate(LabelOverlayCoroutine);
                return new TimingMetricsSnapshot(last, avg, max, channels);

                void Aggregate(TimingMetricsSnapshot s)
                {
                    if (s.SampleCount == 0)
                        return;
                    last += s.LastMs;
                    avg += s.AverageMs;
                    if (s.MaxMs > max)
                        max = s.MaxMs;
                    channels++;
                }
            }
        }

        // Combined background-coroutine cost normalized per frame: last/avg are summed across every
        // channel with a measured run period (scaled by that channel's own period), max is the worst
        // single per-frame spike — directly comparable with the render section table above.
        public TimingMetricsSnapshot CoroutinesTotalPerFrameSnapshot
        {
            get
            {
                double fps = Fps.Current;
                double last = 0, avg = 0, max = 0;
                long channels = 0;
                Aggregate(AltarCoroutine);
                Aggregate(ClickCoroutine);
                Aggregate(FlareCoroutine);
                Aggregate(BlightCoroutine);
                Aggregate(UltimatumCoroutine);
                Aggregate(LabelOverlayCoroutine);
                return new TimingMetricsSnapshot(last, avg, max, channels);

                void Aggregate(TimingMetricsSnapshot s)
                {
                    double scale = s.PerFrameScale(fps);
                    if (s.SampleCount == 0 || scale <= 0)
                        return;
                    last += s.LastMs * scale;
                    avg += s.AverageMs * scale;
                    double frameMax = s.MaxMs * scale;
                    if (frameMax > max)
                        max = frameMax;
                    channels++;
                }
            }
        }

        public double CoroutinesTotalPerFrame => CoroutinesTotalPerFrameSnapshot.AverageMs;
    }
}