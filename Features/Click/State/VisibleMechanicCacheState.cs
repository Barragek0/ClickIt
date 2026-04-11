namespace ClickIt.Features.Click.State
{
    internal sealed class VisibleMechanicCacheState
    {
        private readonly TimedValueCache<int, CandidateCacheSnapshot> _hiddenFallbackCandidates = new(
            int.MaxValue,
            settings: new TimedValueCacheSettings(
                RequireNonNegativeAge: true,
                RequirePositiveCachedTimestamp: true));

        private readonly TimedValueCache<int, CandidateCacheSnapshot> _visibleCandidates = new(
            int.MaxValue,
            settings: new TimedValueCacheSettings(
                RequireNonNegativeAge: true,
                RequirePositiveCachedTimestamp: true));

        public bool TryGetVisibleCandidates(
            long now,
            int labelCount,
            int cacheWindowMs,
            Func<LostShipmentCandidate?, bool> isLostShipmentUsable,
            Func<SettlersOreCandidate?, bool> isSettlersUsable,
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate)
        {
            if (_visibleCandidates.TryGetValue(labelCount, now, out CandidateCacheSnapshot cached)
                && IsWithinWindow(now, cached.TimestampMs, cacheWindowMs)
                && isLostShipmentUsable(cached.LostShipmentCandidate)
                && isSettlersUsable(cached.SettlersOreCandidate))
            {
                lostShipmentCandidate = cached.LostShipmentCandidate;
                settlersOreCandidate = cached.SettlersOreCandidate;
                return true;
            }

            lostShipmentCandidate = null;
            settlersOreCandidate = null;
            return false;
        }

        public void StoreVisibleCandidates(long now, int labelCount, LostShipmentCandidate? lostShipmentCandidate, SettlersOreCandidate? settlersOreCandidate)
        {
            _visibleCandidates.SetValue(
                labelCount,
                now,
                new CandidateCacheSnapshot(now, lostShipmentCandidate, settlersOreCandidate));
        }

        public bool TryGetHiddenFallbackCandidates(
            long now,
            int labelCount,
            int cacheWindowMs,
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate)
        {
            if (_hiddenFallbackCandidates.TryGetValue(labelCount, now, out CandidateCacheSnapshot cached)
                && IsWithinWindow(now, cached.TimestampMs, cacheWindowMs))
            {
                lostShipmentCandidate = cached.LostShipmentCandidate;
                settlersOreCandidate = cached.SettlersOreCandidate;
                return true;
            }

            lostShipmentCandidate = null;
            settlersOreCandidate = null;
            return false;
        }

        public void StoreHiddenFallbackCandidates(long now, int labelCount, LostShipmentCandidate? lostShipmentCandidate, SettlersOreCandidate? settlersOreCandidate)
        {
            _hiddenFallbackCandidates.SetValue(
                labelCount,
                now,
                new CandidateCacheSnapshot(now, lostShipmentCandidate, settlersOreCandidate));
        }

        private static bool IsWithinWindow(long nowMs, long cachedAtMs, int cacheWindowMs)
        {
            if (cacheWindowMs <= 0 || cachedAtMs <= 0)
                return false;

            long ageMs = nowMs - cachedAtMs;
            return ageMs >= 0 && ageMs <= cacheWindowMs;
        }

        private readonly record struct CandidateCacheSnapshot(
            long TimestampMs,
            LostShipmentCandidate? LostShipmentCandidate,
            SettlersOreCandidate? SettlersOreCandidate);
    }
}