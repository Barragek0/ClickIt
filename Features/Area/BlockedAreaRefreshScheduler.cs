namespace ClickIt.Features.Area
{
    internal static class BlockedAreaRefreshScheduler
    {
        internal static bool ShouldRefresh(
            long now,
            long lastRefreshTimestampMs,
            int refreshIntervalMs,
            bool forceRefresh = false)
        {
            if (forceRefresh || refreshIntervalMs <= 0 || lastRefreshTimestampMs <= 0)
                return true;

            long elapsed = now - lastRefreshTimestampMs;
            return elapsed < 0 || elapsed >= refreshIntervalMs;
        }

        internal static bool ShouldRetainQuestTrackerRectanglesOnEmptyRead(
            int currentRectangleCount,
            long now,
            long lastSuccessTimestampMs,
            int holdLastGoodMs)
        {
            if (currentRectangleCount <= 0 || lastSuccessTimestampMs <= 0 || holdLastGoodMs <= 0)
                return false;

            long elapsed = now - lastSuccessTimestampMs;
            return elapsed >= 0 && elapsed <= holdLastGoodMs;
        }
    }
}