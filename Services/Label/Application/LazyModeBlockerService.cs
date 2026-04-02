using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;

namespace ClickIt.Services.Label.Application
{
    public sealed class LazyModeBlockerService(
        ClickItSettings settings,
        GameController? gameController,
        Action<string> logRestriction,
        Func<long>? nowProvider = null)
    {
        private const int NearbyMonsterRestrictionCacheDurationMs = 50;
        private const int LazyModeRestrictionLogThrottleMs = 500;

        private readonly ClickItSettings _settings = settings;
        private readonly GameController? _gameController = gameController;
        private readonly Action<string> _logRestriction = logRestriction;
        private readonly Func<long> _nowProvider = nowProvider ?? (() => Environment.TickCount64);
        private long _cachedNearbyMonsterRestrictionTimestampMs = long.MinValue;
        private int _cachedNearbyMonsterRestrictionSettingsSignature;
        private bool _cachedNearbyMonsterRestrictionResult;
        private string? _cachedNearbyMonsterRestrictionReason;
        private long _lastLazyModeRestrictionLogTimestampMs = long.MinValue;
        private string _lastLazyModeRestrictionLogReason = string.Empty;
        public string? LastRestrictionReason { get; private set; }

        public bool HasRestrictedItemsOnScreen(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (TryGetNearbyMonsterBlockReason(out string? nearbyMonsterReason))
            {
                string reason = nearbyMonsterReason ?? "Nearby monster threshold reached";
                LastRestrictionReason = reason;
                TryLogLazyModeRestriction(reason);
                return true;
            }

            if (allLabels == null)
            {
                LastRestrictionReason = null;
                return false;
            }

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                Entity? item = label.ItemOnGround;
                if (item == null || item.DistancePlayer > _settings.ClickDistance.Value)
                    continue;

                string path = item.Path ?? string.Empty;
                if (path.Length == 0)
                    continue;

                Chest? chest = item.GetComponent<Chest>();
                if (chest?.IsLocked == true && !chest.IsStrongbox)
                {
                    string reason = $"Locked chest detected ({path})";
                    LastRestrictionReason = reason;
                    TryLogLazyModeRestriction(reason);
                    return true;
                }
            }

            LastRestrictionReason = null;
            return false;
        }

        private void TryLogLazyModeRestriction(string reason)
        {
            long now = _nowProvider();
            if (now - _lastLazyModeRestrictionLogTimestampMs < LazyModeRestrictionLogThrottleMs
                && string.Equals(_lastLazyModeRestrictionLogReason, reason, StringComparison.Ordinal))
            {
                return;
            }

            _lastLazyModeRestrictionLogTimestampMs = now;
            _lastLazyModeRestrictionLogReason = reason;
            _logRestriction(reason);
        }

        private bool TryGetNearbyMonsterBlockReason(out string? reason)
        {
            reason = null;

            int normalThreshold = _settings.LazyModeNormalMonsterBlockCount;
            int normalDistance = _settings.LazyModeNormalMonsterBlockDistance;
            int magicThreshold = _settings.LazyModeMagicMonsterBlockCount;
            int magicDistance = _settings.LazyModeMagicMonsterBlockDistance;
            int rareThreshold = _settings.LazyModeRareMonsterBlockCount;
            int rareDistance = _settings.LazyModeRareMonsterBlockDistance;
            int uniqueThreshold = _settings.LazyModeUniqueMonsterBlockCount;
            int uniqueDistance = _settings.LazyModeUniqueMonsterBlockDistance;

            bool normalEnabled = normalThreshold > 0;
            bool magicEnabled = magicThreshold > 0;
            bool rareEnabled = rareThreshold > 0;
            bool uniqueEnabled = uniqueThreshold > 0;
            if (!normalEnabled && !magicEnabled && !rareEnabled && !uniqueEnabled)
                return false;

            int settingsSignature = HashCode.Combine(
                normalThreshold,
                normalDistance,
                magicThreshold,
                magicDistance,
                rareThreshold,
                rareDistance,
                uniqueThreshold,
                uniqueDistance);

            long nowMs = _nowProvider();
            bool cacheFresh = _cachedNearbyMonsterRestrictionTimestampMs != long.MinValue
                && (nowMs - _cachedNearbyMonsterRestrictionTimestampMs) <= NearbyMonsterRestrictionCacheDurationMs
                && _cachedNearbyMonsterRestrictionSettingsSignature == settingsSignature;
            if (cacheFresh)
            {
                reason = _cachedNearbyMonsterRestrictionReason;
                return _cachedNearbyMonsterRestrictionResult;
            }

            var entities = _gameController?.EntityListWrapper?.OnlyValidEntities;
            if (entities == null)
                return false;

            int nearbyNormalCount = 0;
            int nearbyMagicCount = 0;
            int nearbyRareCount = 0;
            int nearbyUniqueCount = 0;
            int maxRelevantDistance = GetMaxRelevantNearbyMonsterDistance(
                normalEnabled,
                normalDistance,
                magicEnabled,
                magicDistance,
                rareEnabled,
                rareDistance,
                uniqueEnabled,
                uniqueDistance);

            foreach (Entity? entity in entities)
            {
                if (entity == null || !entity.IsValid || entity.Type != EntityType.Monster)
                    continue;

                float distancePlayer = entity.DistancePlayer;
                if (distancePlayer < 0f || float.IsNaN(distancePlayer) || float.IsInfinity(distancePlayer))
                    continue;
                if (distancePlayer > maxRelevantDistance)
                    continue;

                switch (entity.Rarity)
                {
                    case MonsterRarity.White:
                        if (normalEnabled && distancePlayer <= normalDistance && entity.IsAlive && entity.IsHostile)
                            nearbyNormalCount++;
                        break;
                    case MonsterRarity.Magic:
                        if (magicEnabled && distancePlayer <= magicDistance && entity.IsAlive && entity.IsHostile)
                            nearbyMagicCount++;
                        break;
                    case MonsterRarity.Rare:
                        if (rareEnabled && distancePlayer <= rareDistance && entity.IsAlive && entity.IsHostile)
                            nearbyRareCount++;
                        break;
                    case MonsterRarity.Unique:
                        if (uniqueEnabled && distancePlayer <= uniqueDistance && entity.IsAlive && entity.IsHostile)
                            nearbyUniqueCount++;
                        break;
                }
            }

            bool normalTriggered = normalThreshold > 0 && nearbyNormalCount >= normalThreshold;
            bool magicTriggered = magicThreshold > 0 && nearbyMagicCount >= magicThreshold;
            bool rareTriggered = rareThreshold > 0 && nearbyRareCount >= rareThreshold;
            bool uniqueTriggered = uniqueThreshold > 0 && nearbyUniqueCount >= uniqueThreshold;
            bool blocked = normalTriggered || magicTriggered || rareTriggered || uniqueTriggered;
            if (blocked)
            {
                reason = BuildNearbyMonsterBlockReason(
                    nearbyNormalCount,
                    normalThreshold,
                    normalDistance,
                    normalTriggered,
                    nearbyMagicCount,
                    magicThreshold,
                    magicDistance,
                    magicTriggered,
                    nearbyRareCount,
                    rareThreshold,
                    rareDistance,
                    rareTriggered,
                    nearbyUniqueCount,
                    uniqueThreshold,
                    uniqueDistance,
                    uniqueTriggered);
            }

            _cachedNearbyMonsterRestrictionTimestampMs = nowMs;
            _cachedNearbyMonsterRestrictionSettingsSignature = settingsSignature;
            _cachedNearbyMonsterRestrictionResult = blocked;
            _cachedNearbyMonsterRestrictionReason = reason;
            return blocked;
        }

        private static int GetMaxRelevantNearbyMonsterDistance(
            bool normalEnabled,
            int normalDistance,
            bool magicEnabled,
            int magicDistance,
            bool rareEnabled,
            int rareDistance,
            bool uniqueEnabled,
            int uniqueDistance)
        {
            int maxDistance = 0;
            if (normalEnabled)
                maxDistance = Math.Max(maxDistance, normalDistance);
            if (magicEnabled)
                maxDistance = Math.Max(maxDistance, magicDistance);
            if (rareEnabled)
                maxDistance = Math.Max(maxDistance, rareDistance);
            if (uniqueEnabled)
                maxDistance = Math.Max(maxDistance, uniqueDistance);
            return maxDistance;
        }

        public static bool ShouldBlockLazyModeForNearbyMonsters(
            int nearbyNormalCount,
            int normalThreshold,
            int nearbyMagicCount,
            int magicThreshold,
            int nearbyRareCount,
            int rareThreshold,
            int nearbyUniqueCount,
            int uniqueThreshold)
        {
            bool normalTriggered = normalThreshold > 0 && nearbyNormalCount >= normalThreshold;
            bool magicTriggered = magicThreshold > 0 && nearbyMagicCount >= magicThreshold;
            bool rareTriggered = rareThreshold > 0 && nearbyRareCount >= rareThreshold;
            bool uniqueTriggered = uniqueThreshold > 0 && nearbyUniqueCount >= uniqueThreshold;
            return normalTriggered || magicTriggered || rareTriggered || uniqueTriggered;
        }

        public static string BuildNearbyMonsterBlockReason(
            int nearbyNormalCount,
            int normalThreshold,
            int normalDistance,
            bool normalTriggered,
            int nearbyMagicCount,
            int magicThreshold,
            int magicDistance,
            bool magicTriggered,
            int nearbyRareCount,
            int rareThreshold,
            int rareDistance,
            bool rareTriggered,
            int nearbyUniqueCount,
            int uniqueThreshold,
            int uniqueDistance,
            bool uniqueTriggered)
        {
            List<string> segments = [];

            if (normalTriggered)
                segments.Add($"Normal {nearbyNormalCount}/{normalThreshold} within {normalDistance}");
            if (magicTriggered)
                segments.Add($"Magic {nearbyMagicCount}/{magicThreshold} within {magicDistance}");
            if (rareTriggered)
                segments.Add($"Rare {nearbyRareCount}/{rareThreshold} within {rareDistance}");
            if (uniqueTriggered)
                segments.Add($"Unique {nearbyUniqueCount}/{uniqueThreshold} within {uniqueDistance}");

            return segments.Count == 0
                ? "Nearby monster threshold reached"
                : string.Join(", ", segments);
        }
    }
}