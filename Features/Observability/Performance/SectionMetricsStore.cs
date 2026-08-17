namespace ClickIt.Features.Observability.Performance
{
    // Generic per-enum-value sample store: one RollingSampleBuffer per enum value, indexed by the enum's integer value.
    internal sealed class SectionMetricsStore<TEnum> where TEnum : struct, Enum
    {
        private readonly RollingSampleBuffer[] _buffers;

        internal SectionMetricsStore()
        {
            int maxValue = 0;
            foreach (TEnum value in Enum.GetValues<TEnum>())
                maxValue = SystemMath.Max(maxValue, Convert.ToInt32(value));
            _buffers = new RollingSampleBuffer[maxValue + 1];
            for (int i = 0; i < _buffers.Length; i++)
                _buffers[i] = new RollingSampleBuffer();
        }

        internal void Record(TEnum section, double ms)
        {
            if (Unsafe.As<TEnum, int>(ref section) == 0)
                return;
            _buffers[Unsafe.As<TEnum, int>(ref section)].Record(ms);
        }

        internal (double LastMs, double AverageMs, double MaxMs, long SampleCount) GetStats(TEnum section)
            => _buffers[Unsafe.As<TEnum, int>(ref section)].Stats;
    }
}
