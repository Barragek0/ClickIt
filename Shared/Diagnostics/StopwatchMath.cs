namespace ClickIt.Shared.Diagnostics
{
    internal static class StopwatchMath
    {
        internal static double ElapsedMs(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}
