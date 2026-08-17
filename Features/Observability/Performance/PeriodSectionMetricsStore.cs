namespace ClickIt.Features.Observability.Performance
{
    // Generic per-enum-value processing store: one PeriodTrackedBuffer per enum value, indexed by the enum's integer value.
    internal sealed class PeriodSectionMetricsStore<TEnum> where TEnum : struct, Enum
    {
        private readonly PeriodTrackedBuffer[] _buffers;

        internal PeriodSectionMetricsStore()
        {
            int maxValue = 0;
            foreach (TEnum value in Enum.GetValues<TEnum>())
                maxValue = SystemMath.Max(maxValue, Convert.ToInt32(value));
            _buffers = new PeriodTrackedBuffer[maxValue + 1];
            for (int i = 0; i < _buffers.Length; i++)
                _buffers[i] = new PeriodTrackedBuffer();
        }

        internal void Record(TEnum section, double ms)
        {
            if (Unsafe.As<TEnum, int>(ref section) == 0)
                return;
            _buffers[Unsafe.As<TEnum, int>(ref section)].Record(ms);
        }

        internal (double LastMs, double AverageMs, double MaxMs, long SampleCount, double AveragePeriodMs) GetStats(TEnum section)
            => _buffers[Unsafe.As<TEnum, int>(ref section)].Stats;
    }
}
