namespace ClickIt.Features.Click.Runtime
{
    internal static class VisibleMechanicCachePolicy
    {
        internal static bool ShouldReuseTimedLabelCountCache(long now, long cachedAtMs, int cachedLabelCount, int currentLabelCount, int cacheWindowMs)
        {
            if (cachedAtMs <= 0 || cacheWindowMs <= 0)
                return false;

            if (cachedLabelCount != currentLabelCount)
                return false;

            long age = now - cachedAtMs;
            return age >= 0 && age <= cacheWindowMs;
        }
    }
}