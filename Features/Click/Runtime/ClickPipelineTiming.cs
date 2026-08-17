namespace ClickIt.Features.Click.Runtime
{
    // Accumulates intentional safety sleeps in the click pipeline (cursor-settle, UI-hover verification, post-click settle, movement-skill cast) so the measured click "processing" time can be separated from deliberate wait time. Thread-static because the click coroutine is a dedicated thread; Reset/Consume bracket each click tick on that same thread.
    internal static class ClickPipelineTiming
    {
        [ThreadStatic]
        private static long s_sleepTicks;

        internal static void Sleep(int milliseconds)
        {
            if (milliseconds <= 0)
                return;
            s_sleepTicks += milliseconds * TimeSpan.TicksPerMillisecond;
            Thread.Sleep(milliseconds);
        }

        internal static void ResetSleepTime()
            => s_sleepTicks = 0;

        // Read-only probe: the accumulated safety-sleep time WITHOUT resetting, so per-stage performance measurements can subtract the deliberate waits that happened inside them while the host's ConsumeSleepTimeMs() still accounts for the full total once per tick.
        internal static double ReadSleepTimeMs()
            => s_sleepTicks / (double)TimeSpan.TicksPerMillisecond;

        internal static double ConsumeSleepTimeMs()
        {
            double ms = s_sleepTicks / (double)TimeSpan.TicksPerMillisecond;
            s_sleepTicks = 0;
            return ms;
        }
    }
}
