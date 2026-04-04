namespace ClickIt.Features.Click.State
{
    internal sealed class VisibleMechanicCacheState
    {
        private const int UninitializedLabelCount = -1;

        private long hiddenFallbackCandidateCacheTimestampMs;
        private int hiddenFallbackCandidateLabelCount = UninitializedLabelCount;
        private bool hiddenFallbackCandidateCacheHasValue;
        private LostShipmentCandidate? hiddenFallbackCachedLostShipmentCandidate;
        private SettlersOreCandidate? hiddenFallbackCachedSettlersCandidate;
        private long visibleMechanicCandidateCacheTimestampMs;
        private int visibleMechanicCandidateLabelCount = UninitializedLabelCount;
        private bool visibleMechanicCandidateCacheHasValue;
        private LostShipmentCandidate? visibleMechanicCachedLostShipmentCandidate;
        private SettlersOreCandidate? visibleMechanicCachedSettlersCandidate;

        public bool TryGetVisibleCandidates(
            long now,
            int labelCount,
            int cacheWindowMs,
            Func<LostShipmentCandidate?, bool> isLostShipmentUsable,
            Func<SettlersOreCandidate?, bool> isSettlersUsable,
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate)
        {
            if (visibleMechanicCandidateCacheHasValue
                && VisibleMechanicCachePolicy.ShouldReuseTimedLabelCountCache(
                    now,
                    visibleMechanicCandidateCacheTimestampMs,
                    visibleMechanicCandidateLabelCount,
                    labelCount,
                    cacheWindowMs)
                && isLostShipmentUsable(visibleMechanicCachedLostShipmentCandidate)
                && isSettlersUsable(visibleMechanicCachedSettlersCandidate))
            {
                lostShipmentCandidate = visibleMechanicCachedLostShipmentCandidate;
                settlersOreCandidate = visibleMechanicCachedSettlersCandidate;
                return true;
            }

            lostShipmentCandidate = null;
            settlersOreCandidate = null;
            return false;
        }

        public void StoreVisibleCandidates(long now, int labelCount, LostShipmentCandidate? lostShipmentCandidate, SettlersOreCandidate? settlersOreCandidate)
        {
            visibleMechanicCachedLostShipmentCandidate = lostShipmentCandidate;
            visibleMechanicCachedSettlersCandidate = settlersOreCandidate;
            visibleMechanicCandidateCacheTimestampMs = now;
            visibleMechanicCandidateLabelCount = labelCount;
            visibleMechanicCandidateCacheHasValue = true;
        }

        public bool TryGetHiddenFallbackCandidates(
            long now,
            int labelCount,
            int cacheWindowMs,
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate)
        {
            if (hiddenFallbackCandidateCacheHasValue
                && VisibleMechanicCachePolicy.ShouldReuseTimedLabelCountCache(
                    now,
                    hiddenFallbackCandidateCacheTimestampMs,
                    hiddenFallbackCandidateLabelCount,
                    labelCount,
                    cacheWindowMs))
            {
                lostShipmentCandidate = hiddenFallbackCachedLostShipmentCandidate;
                settlersOreCandidate = hiddenFallbackCachedSettlersCandidate;
                return true;
            }

            lostShipmentCandidate = null;
            settlersOreCandidate = null;
            return false;
        }

        public void StoreHiddenFallbackCandidates(long now, int labelCount, LostShipmentCandidate? lostShipmentCandidate, SettlersOreCandidate? settlersOreCandidate)
        {
            hiddenFallbackCachedLostShipmentCandidate = lostShipmentCandidate;
            hiddenFallbackCachedSettlersCandidate = settlersOreCandidate;
            hiddenFallbackCandidateCacheTimestampMs = now;
            hiddenFallbackCandidateLabelCount = labelCount;
            hiddenFallbackCandidateCacheHasValue = true;
        }
    }
}