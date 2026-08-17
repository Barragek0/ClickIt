namespace ClickIt.Features.Observability.Performance
{
    internal sealed class TimingChannelMetricsTracker
    {
        private readonly Lock _timerLock = new();
        private readonly Stopwatch _renderTimer = new();
        private readonly ExpiringSampleBuffer _renderTimings = new();
        private readonly ExpiringSampleBuffer _successfulClickTimings = new();
        private readonly ExpiringSampleBuffer _clickSleepTimings = new();

        // One per coroutine TimingChannel (indexed by enum value; the Unknown/Render slots are unused by coroutine timing). The Click channel is shared by the click-loop and manual-ui-hover coroutines (they can interleave at yield points), so Start/Stop are serialized under _timerLock.
        private sealed class ChannelTiming
        {
            public readonly Stopwatch Timer = new();
            public readonly ExpiringSampleBuffer Timings = new();
            public readonly ExpiringSampleBuffer Periods = new();
            public long LastStopTimestampMs;
        }

        private readonly ChannelTiming[] _channels = CreateChannels();

        private static ChannelTiming[] CreateChannels()
        {
            ChannelTiming[] channels = new ChannelTiming[8];
            for (int i = 0; i < channels.Length; i++)
                channels[i] = new ChannelTiming();
            return channels;
        }

        private static bool IsCoroutineChannel(TimingChannel channel)
            => channel is not (TimingChannel.Unknown or TimingChannel.Render);

        private ExpiringSampleBuffer TimingsFor(TimingChannel channel)
            => channel == TimingChannel.Render ? _renderTimings : _channels[(int)channel].Timings;

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
            if (!IsCoroutineChannel(channel))
                return;
            lock (_timerLock)
            {
                _channels[(int)channel].Timer.Restart();
            }
        }

        public void StartCoroutineTiming(string coroutineName)
            => StartCoroutineTiming(MapTimingChannel(coroutineName));

        public void StopCoroutineTiming(TimingChannel channel)
        {
            if (!IsCoroutineChannel(channel))
                return;
            lock (_timerLock)
            {
                ChannelTiming channelTiming = _channels[(int)channel];
                channelTiming.Timer.Stop();
                channelTiming.Timings.Record(channelTiming.Timer.ElapsedMilliseconds);
                long now = Environment.TickCount64;
                if (channelTiming.LastStopTimestampMs != 0)
                    channelTiming.Periods.Record(now - channelTiming.LastStopTimestampMs);
                channelTiming.LastStopTimestampMs = now;
            }
        }

        public void StopCoroutineTiming(string coroutineName)
            => StopCoroutineTiming(MapTimingChannel(coroutineName));

        public double GetLastTiming(TimingChannel channel)
            => channel == TimingChannel.Unknown ? 0 : TimingsFor(channel).Stats.Last;

        public double GetLastTiming(string timingType)
            => GetLastTiming(MapTimingChannel(timingType));

        public double GetAverageTiming(TimingChannel channel)
            => channel == TimingChannel.Unknown ? 0 : TimingsFor(channel).Stats.Average;

        public double GetAverageTiming(string timingType)
            => GetAverageTiming(MapTimingChannel(timingType));

        public double GetMaxTiming(TimingChannel channel)
            => channel == TimingChannel.Unknown ? 0 : TimingsFor(channel).Stats.Max;

        public double GetMaxTiming(string timingType)
            => GetMaxTiming(MapTimingChannel(timingType));

        public double GetAveragePeriod(TimingChannel channel)
            => IsCoroutineChannel(channel) ? _channels[(int)channel].Periods.Stats.Average : 0;

        public double GetAveragePeriod(string timingType)
            => GetAveragePeriod(MapTimingChannel(timingType));

        public int GetTimingSampleCount(TimingChannel channel)
            => channel == TimingChannel.Unknown ? 0 : (int)TimingsFor(channel).LiveSampleCount();

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

        public void Clear()
        {
            _renderTimer.Stop();
            _renderTimings.Clear();
            _successfulClickTimings.Clear();

            foreach (ChannelTiming channelTiming in _channels)
            {
                channelTiming.Timer.Stop();
                channelTiming.Timings.Clear();
                channelTiming.Periods.Clear();
                channelTiming.LastStopTimestampMs = 0;
            }
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