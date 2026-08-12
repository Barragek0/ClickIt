namespace ClickIt.Features.Observability.Performance
{
    internal sealed class TimingChannelMetricsTracker
    {
        private readonly Stopwatch _renderTimer = new();
        private readonly Stopwatch _altarCoroutineTimer = new();
        private readonly Stopwatch _clickCoroutineTimer = new();
        private readonly Stopwatch _flareCoroutineTimer = new();
        private readonly Stopwatch _blightCoroutineTimer = new();
        private readonly Stopwatch _ultimatumCoroutineTimer = new();
        private readonly Stopwatch _labelOverlayCoroutineTimer = new();

        // Every timing/period sample expires 10 seconds after it was recorded, so a channel that
        // stops running drains to zero instead of reporting a stale all-time average/max/last.
        private readonly ExpiringSampleBuffer _clickCoroutineTimings = new();
        private readonly ExpiringSampleBuffer _altarCoroutineTimings = new();
        private readonly ExpiringSampleBuffer _flareCoroutineTimings = new();
        private readonly ExpiringSampleBuffer _blightCoroutineTimings = new();
        private readonly ExpiringSampleBuffer _ultimatumCoroutineTimings = new();
        private readonly ExpiringSampleBuffer _labelOverlayCoroutineTimings = new();
        private readonly ExpiringSampleBuffer _renderTimings = new();
        private readonly ExpiringSampleBuffer _successfulClickTimings = new();
        private readonly ExpiringSampleBuffer _clickSleepTimings = new();

        private readonly ExpiringSampleBuffer _altarPeriods = new();
        private readonly ExpiringSampleBuffer _clickPeriods = new();
        private readonly ExpiringSampleBuffer _flarePeriods = new();
        private readonly ExpiringSampleBuffer _blightPeriods = new();
        private readonly ExpiringSampleBuffer _ultimatumPeriods = new();
        private readonly ExpiringSampleBuffer _labelOverlayPeriods = new();

        private long _lastAltarStopTimestampMs;
        private long _lastClickStopTimestampMs;
        private long _lastFlareStopTimestampMs;
        private long _lastBlightStopTimestampMs;
        private long _lastUltimatumStopTimestampMs;
        private long _lastLabelOverlayStopTimestampMs;

        public Queue<double> GetRenderTimingsSnapshot()
            => new(_renderTimings.ValuesSnapshot());

        public (double LastMs, double AverageMs, double MaxMs, int SampleCount) GetRenderTimingStats()
        {
            (double last, double average, double max, long count) = _renderTimings.Stats;
            return (last, average, max, (int)count);
        }

        public void StartRenderTiming()
        {
            _renderTimer.Restart();
        }

        public void StopRenderTiming()
        {
            _renderTimer.Stop();
            _renderTimings.Record(_renderTimer.Elapsed.TotalMilliseconds);
        }

        public void StartCoroutineTiming(TimingChannel channel)
        {
            switch (channel)
            {
                case TimingChannel.Altar:
                    _altarCoroutineTimer.Restart();
                    break;
                case TimingChannel.Click:
                    _clickCoroutineTimer.Restart();
                    break;
                case TimingChannel.Flare:
                    _flareCoroutineTimer.Restart();
                    break;
                case TimingChannel.Blight:
                    _blightCoroutineTimer.Restart();
                    break;
                case TimingChannel.Ultimatum:
                    _ultimatumCoroutineTimer.Restart();
                    break;
                case TimingChannel.LabelOverlay:
                    _labelOverlayCoroutineTimer.Restart();
                    break;
                case TimingChannel.Unknown:
                case TimingChannel.Render:
                default:
                    break;
            }
        }

        public void StartCoroutineTiming(string coroutineName)
        {
            StartCoroutineTiming(MapTimingChannel(coroutineName));
        }

        public void StopCoroutineTiming(TimingChannel channel)
        {
            switch (channel)
            {
                case TimingChannel.Altar:
                    _altarCoroutineTimer.Stop();
                    _altarCoroutineTimings.Record(_altarCoroutineTimer.ElapsedMilliseconds);
                    RecordPeriod(ref _lastAltarStopTimestampMs, _altarPeriods);
                    break;
                case TimingChannel.Click:
                    _clickCoroutineTimer.Stop();
                    _clickCoroutineTimings.Record(_clickCoroutineTimer.ElapsedMilliseconds);
                    RecordPeriod(ref _lastClickStopTimestampMs, _clickPeriods);
                    break;
                case TimingChannel.Flare:
                    _flareCoroutineTimer.Stop();
                    _flareCoroutineTimings.Record(_flareCoroutineTimer.ElapsedMilliseconds);
                    RecordPeriod(ref _lastFlareStopTimestampMs, _flarePeriods);
                    break;
                case TimingChannel.Blight:
                    _blightCoroutineTimer.Stop();
                    _blightCoroutineTimings.Record(_blightCoroutineTimer.ElapsedMilliseconds);
                    RecordPeriod(ref _lastBlightStopTimestampMs, _blightPeriods);
                    break;
                case TimingChannel.Ultimatum:
                    _ultimatumCoroutineTimer.Stop();
                    _ultimatumCoroutineTimings.Record(_ultimatumCoroutineTimer.ElapsedMilliseconds);
                    RecordPeriod(ref _lastUltimatumStopTimestampMs, _ultimatumPeriods);
                    break;
                case TimingChannel.LabelOverlay:
                    _labelOverlayCoroutineTimer.Stop();
                    _labelOverlayCoroutineTimings.Record(_labelOverlayCoroutineTimer.ElapsedMilliseconds);
                    RecordPeriod(ref _lastLabelOverlayStopTimestampMs, _labelOverlayPeriods);
                    break;
                case TimingChannel.Unknown:
                case TimingChannel.Render:
                default:
                    break;
            }
        }

        public void StopCoroutineTiming(string coroutineName)
        {
            StopCoroutineTiming(MapTimingChannel(coroutineName));
        }

        public double GetLastTiming(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Click => _clickCoroutineTimings.Stats.Last,
                TimingChannel.Altar => _altarCoroutineTimings.Stats.Last,
                TimingChannel.Flare => _flareCoroutineTimings.Stats.Last,
                TimingChannel.Blight => _blightCoroutineTimings.Stats.Last,
                TimingChannel.Ultimatum => _ultimatumCoroutineTimings.Stats.Last,
                TimingChannel.LabelOverlay => _labelOverlayCoroutineTimings.Stats.Last,
                TimingChannel.Render => _renderTimings.Stats.Last,
                TimingChannel.Unknown => 0,
                _ => 0,
            };
        }

        public double GetLastTiming(string timingType)
        {
            return GetLastTiming(MapTimingChannel(timingType));
        }

        public double GetAverageTiming(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Render => _renderTimings.Stats.Average,
                TimingChannel.Click => _clickCoroutineTimings.Stats.Average,
                TimingChannel.Altar => _altarCoroutineTimings.Stats.Average,
                TimingChannel.Flare => _flareCoroutineTimings.Stats.Average,
                TimingChannel.Blight => _blightCoroutineTimings.Stats.Average,
                TimingChannel.Ultimatum => _ultimatumCoroutineTimings.Stats.Average,
                TimingChannel.LabelOverlay => _labelOverlayCoroutineTimings.Stats.Average,
                TimingChannel.Unknown => 0,
                _ => 0,
            };
        }

        public double GetAverageTiming(string timingType)
        {
            return GetAverageTiming(MapTimingChannel(timingType));
        }

        public void RecordSuccessfulClickTiming(long duration)
            => _successfulClickTimings.Record(duration);

        public double GetAverageSuccessfulClickTiming()
            => _successfulClickTimings.Stats.Average;

        public void RecordClickSleepTiming(double ms)
            => _clickSleepTimings.Record(ms);

        public double GetAverageClickSleepMs()
            => _clickSleepTimings.Stats.Average;

        public (double LastMs, double AverageMs, double MaxMs, int SampleCount) GetClickSleepTimingStats()
        {
            (double last, double average, double max, long count) = _clickSleepTimings.Stats;
            return (last, average, max, (int)count);
        }

        public double GetMaxTiming(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Click => _clickCoroutineTimings.Stats.Max,
                TimingChannel.Altar => _altarCoroutineTimings.Stats.Max,
                TimingChannel.Flare => _flareCoroutineTimings.Stats.Max,
                TimingChannel.Blight => _blightCoroutineTimings.Stats.Max,
                TimingChannel.Ultimatum => _ultimatumCoroutineTimings.Stats.Max,
                TimingChannel.LabelOverlay => _labelOverlayCoroutineTimings.Stats.Max,
                TimingChannel.Render => _renderTimings.Stats.Max,
                TimingChannel.Unknown => 0,
                _ => 0,
            };
        }

        public double GetMaxTiming(string timingType)
        {
            return GetMaxTiming(MapTimingChannel(timingType));
        }

        public double GetAveragePeriod(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Altar => _altarPeriods.Stats.Average,
                TimingChannel.Click => _clickPeriods.Stats.Average,
                TimingChannel.Flare => _flarePeriods.Stats.Average,
                TimingChannel.Blight => _blightPeriods.Stats.Average,
                TimingChannel.Ultimatum => _ultimatumPeriods.Stats.Average,
                TimingChannel.LabelOverlay => _labelOverlayPeriods.Stats.Average,
                TimingChannel.Render => 0,
                TimingChannel.Unknown => 0,
                _ => 0,
            };
        }

        public double GetAveragePeriod(string timingType)
        {
            return GetAveragePeriod(MapTimingChannel(timingType));
        }

        public int GetTimingSampleCount(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Click => (int)_clickCoroutineTimings.LiveSampleCount(),
                TimingChannel.Altar => (int)_altarCoroutineTimings.LiveSampleCount(),
                TimingChannel.Flare => (int)_flareCoroutineTimings.LiveSampleCount(),
                TimingChannel.Blight => (int)_blightCoroutineTimings.LiveSampleCount(),
                TimingChannel.Ultimatum => (int)_ultimatumCoroutineTimings.LiveSampleCount(),
                TimingChannel.LabelOverlay => (int)_labelOverlayCoroutineTimings.LiveSampleCount(),
                TimingChannel.Render => (int)_renderTimings.LiveSampleCount(),
                TimingChannel.Unknown => 0,
                _ => 0,
            };
        }

        public void Clear()
        {
            _renderTimer.Stop();
            _altarCoroutineTimer.Stop();
            _clickCoroutineTimer.Stop();
            _flareCoroutineTimer.Stop();
            _blightCoroutineTimer.Stop();
            _ultimatumCoroutineTimer.Stop();
            _labelOverlayCoroutineTimer.Stop();

            _clickCoroutineTimings.Clear();
            _altarCoroutineTimings.Clear();
            _flareCoroutineTimings.Clear();
            _blightCoroutineTimings.Clear();
            _ultimatumCoroutineTimings.Clear();
            _labelOverlayCoroutineTimings.Clear();
            _renderTimings.Clear();
            _successfulClickTimings.Clear();

            _altarPeriods.Clear();
            _clickPeriods.Clear();
            _flarePeriods.Clear();
            _blightPeriods.Clear();
            _ultimatumPeriods.Clear();
            _labelOverlayPeriods.Clear();
            _lastAltarStopTimestampMs = 0;
            _lastClickStopTimestampMs = 0;
            _lastFlareStopTimestampMs = 0;
            _lastBlightStopTimestampMs = 0;
            _lastUltimatumStopTimestampMs = 0;
            _lastLabelOverlayStopTimestampMs = 0;
        }

        private static void RecordPeriod(ref long lastStopTimestampMs, ExpiringSampleBuffer periods)
        {
            long now = Environment.TickCount64;
            if (lastStopTimestampMs != 0)
                periods.Record(now - lastStopTimestampMs);
            lastStopTimestampMs = now;
        }

        private static TimingChannel MapTimingChannel(string? timingType)
        {
            return timingType switch
            {
                "click" => TimingChannel.Click,
                "altar" => TimingChannel.Altar,
                "flare" => TimingChannel.Flare,
                "blight" => TimingChannel.Blight,
                "ultimatum" => TimingChannel.Ultimatum,
                "labeloverlay" => TimingChannel.LabelOverlay,
                "render" => TimingChannel.Render,
                _ => TimingChannel.Unknown,
            };
        }
    }
}