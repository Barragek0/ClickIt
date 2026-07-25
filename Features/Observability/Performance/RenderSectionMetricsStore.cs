namespace ClickIt.Features.Observability.Performance
{
    internal sealed class RenderSectionMetricsStore
    {
        private double _lastLazyModeMs;
        private double _avgLazyModeMs;
        private double _maxLazyModeMs;
        private long _lazyModeSamples;

        private double _lastDebugOverlayMs;
        private double _avgDebugOverlayMs;
        private double _maxDebugOverlayMs;
        private long _debugOverlaySamples;

        private double _lastAltarOverlayMs;
        private double _avgAltarOverlayMs;
        private double _maxAltarOverlayMs;
        private long _altarOverlaySamples;

        private double _lastUltimatumOverlayMs;
        private double _avgUltimatumOverlayMs;
        private double _maxUltimatumOverlayMs;
        private long _ultimatumOverlaySamples;

        private double _lastStrongboxOverlayMs;
        private double _avgStrongboxOverlayMs;
        private double _maxStrongboxOverlayMs;
        private long _strongboxOverlaySamples;

        private double _lastTextFlushMs;
        private double _avgTextFlushMs;
        private double _maxTextFlushMs;
        private long _textFlushSamples;

        private double _lastPathfindingOverlayMs;
        private double _avgPathfindingOverlayMs;
        private double _maxPathfindingOverlayMs;
        private long _pathfindingOverlaySamples;

        private double _lastFrameFlushMs;
        private double _avgFrameFlushMs;
        private double _maxFrameFlushMs;
        private long _frameFlushSamples;

        internal void Record(RenderSection section, double ms)
        {
            switch (section)
            {
                case RenderSection.LazyMode:
                    RecordSample(ref _lastLazyModeMs, ref _avgLazyModeMs, ref _maxLazyModeMs, ref _lazyModeSamples, ms);
                    break;
                case RenderSection.DebugOverlay:
                    RecordSample(ref _lastDebugOverlayMs, ref _avgDebugOverlayMs, ref _maxDebugOverlayMs, ref _debugOverlaySamples, ms);
                    break;
                case RenderSection.AltarOverlay:
                    RecordSample(ref _lastAltarOverlayMs, ref _avgAltarOverlayMs, ref _maxAltarOverlayMs, ref _altarOverlaySamples, ms);
                    break;
                case RenderSection.UltimatumOverlay:
                    RecordSample(ref _lastUltimatumOverlayMs, ref _avgUltimatumOverlayMs, ref _maxUltimatumOverlayMs, ref _ultimatumOverlaySamples, ms);
                    break;
                case RenderSection.StrongboxOverlay:
                    RecordSample(ref _lastStrongboxOverlayMs, ref _avgStrongboxOverlayMs, ref _maxStrongboxOverlayMs, ref _strongboxOverlaySamples, ms);
                    break;
                case RenderSection.TextFlush:
                    RecordSample(ref _lastTextFlushMs, ref _avgTextFlushMs, ref _maxTextFlushMs, ref _textFlushSamples, ms);
                    break;
                case RenderSection.PathfindingOverlay:
                    RecordSample(ref _lastPathfindingOverlayMs, ref _avgPathfindingOverlayMs, ref _maxPathfindingOverlayMs, ref _pathfindingOverlaySamples, ms);
                    break;
                case RenderSection.FrameFlush:
                    RecordSample(ref _lastFrameFlushMs, ref _avgFrameFlushMs, ref _maxFrameFlushMs, ref _frameFlushSamples, ms);
                    break;
                case RenderSection.Unknown:
                default:
                    break;
            }
        }

        internal (double LastMs, double AverageMs, double MaxMs, long SampleCount) GetStats(RenderSection section)
        {
            return section switch
            {
                RenderSection.LazyMode => (_lastLazyModeMs, _avgLazyModeMs, _maxLazyModeMs, _lazyModeSamples),
                RenderSection.DebugOverlay => (_lastDebugOverlayMs, _avgDebugOverlayMs, _maxDebugOverlayMs, _debugOverlaySamples),
                RenderSection.AltarOverlay => (_lastAltarOverlayMs, _avgAltarOverlayMs, _maxAltarOverlayMs, _altarOverlaySamples),
                RenderSection.UltimatumOverlay => (_lastUltimatumOverlayMs, _avgUltimatumOverlayMs, _maxUltimatumOverlayMs, _ultimatumOverlaySamples),
                RenderSection.StrongboxOverlay => (_lastStrongboxOverlayMs, _avgStrongboxOverlayMs, _maxStrongboxOverlayMs, _strongboxOverlaySamples),
                RenderSection.PathfindingOverlay => (_lastPathfindingOverlayMs, _avgPathfindingOverlayMs, _maxPathfindingOverlayMs, _pathfindingOverlaySamples),
                RenderSection.TextFlush => (_lastTextFlushMs, _avgTextFlushMs, _maxTextFlushMs, _textFlushSamples),
                RenderSection.FrameFlush => (_lastFrameFlushMs, _avgFrameFlushMs, _maxFrameFlushMs, _frameFlushSamples),
                RenderSection.Unknown => (0, 0, 0, 0),
                _ => (0, 0, 0, 0)
            };
        }

        private static void RecordSample(ref double last, ref double avg, ref double max, ref long samples, double ms)
        {
            last = ms;
            samples++;
            avg += (ms - avg) / samples;
            if (ms > max)
                max = ms;

        }
    }
}