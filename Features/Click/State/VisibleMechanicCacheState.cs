using ExileCore.PoEMemory.Elements;
using ClickIt.Features.Click.Runtime;

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
        private readonly HashSet<long> cachedGroundLabelEntityAddresses = [];
        private long cachedGroundLabelEntityAddressesTimestampMs;
        private int cachedGroundLabelEntityLabelCount = UninitializedLabelCount;

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

        public IReadOnlySet<long> CollectGroundLabelEntityAddresses(IList<LabelOnGround>? labels, int cacheWindowMs)
        {
            try
            {
                int labelCount = labels?.Count ?? 0;
                if (labels == null || labelCount == 0)
                {
                    cachedGroundLabelEntityAddresses.Clear();
                    cachedGroundLabelEntityLabelCount = 0;
                    cachedGroundLabelEntityAddressesTimestampMs = Environment.TickCount64;
                    return cachedGroundLabelEntityAddresses;
                }

                long now = Environment.TickCount64;
                if (VisibleMechanicCachePolicy.ShouldReuseTimedLabelCountCache(
                        now,
                        cachedGroundLabelEntityAddressesTimestampMs,
                        cachedGroundLabelEntityLabelCount,
                        labelCount,
                        cacheWindowMs))
                {
                    return cachedGroundLabelEntityAddresses;
                }

                cachedGroundLabelEntityAddresses.Clear();
                cachedGroundLabelEntityAddresses.EnsureCapacity(labelCount);

                for (int i = 0; i < labelCount; i++)
                {
                    long address = labels[i]?.ItemOnGround?.Address ?? 0;
                    if (address != 0)
                    {
                        cachedGroundLabelEntityAddresses.Add(address);
                    }
                }

                cachedGroundLabelEntityAddressesTimestampMs = now;
                cachedGroundLabelEntityLabelCount = labelCount;
            }
            catch
            {
            }

            return cachedGroundLabelEntityAddresses;
        }
    }
}