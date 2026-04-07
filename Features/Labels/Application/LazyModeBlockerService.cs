namespace ClickIt.Features.Labels.Application
{
    internal readonly record struct NearbyMonsterCandidate(
        bool IsValid,
        EntityType Type,
        float DistancePlayer,
        MonsterRarity Rarity,
        bool IsAlive,
        bool IsHostile);

    internal readonly record struct LockedChestCandidate(
        float DistancePlayer,
        string Path,
        bool IsLocked,
        bool IsStrongbox);

    internal readonly record struct NearbyMonsterRestrictionSettings(
        int NormalThreshold,
        int NormalDistance,
        int MagicThreshold,
        int MagicDistance,
        int RareThreshold,
        int RareDistance,
        int UniqueThreshold,
        int UniqueDistance,
        bool NormalEnabled,
        bool MagicEnabled,
        bool RareEnabled,
        bool UniqueEnabled,
        int MaxRelevantDistance,
        int Signature)
    {
        internal bool HasEnabledRestrictions
            => NormalEnabled || MagicEnabled || RareEnabled || UniqueEnabled;
    }

    internal readonly record struct NearbyMonsterCounts(
        int Normal,
        int Magic,
        int Rare,
        int Unique);

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

            string? lockedChestReason = TryFindLockedChestRestrictionReason(
                allLabels,
                _settings.ClickDistance.Value);
            if (lockedChestReason != null)
            {
                LastRestrictionReason = lockedChestReason;
                TryLogLazyModeRestriction(lockedChestReason);
                return true;
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

            NearbyMonsterRestrictionSettings restrictionSettings = BuildNearbyMonsterRestrictionSettings(
                _settings.LazyModeNormalMonsterBlockCount,
                _settings.LazyModeNormalMonsterBlockDistance,
                _settings.LazyModeMagicMonsterBlockCount,
                _settings.LazyModeMagicMonsterBlockDistance,
                _settings.LazyModeRareMonsterBlockCount,
                _settings.LazyModeRareMonsterBlockDistance,
                _settings.LazyModeUniqueMonsterBlockCount,
                _settings.LazyModeUniqueMonsterBlockDistance);
            if (!restrictionSettings.HasEnabledRestrictions)
                return false;

            long nowMs = _nowProvider();
            if (TryGetCachedNearbyMonsterRestriction(nowMs, restrictionSettings.Signature, out bool cachedBlocked, out string? cachedReason))
            {
                reason = cachedReason;
                return cachedBlocked;
            }

            var entities = _gameController?.EntityListWrapper?.OnlyValidEntities;
            if (entities == null)
                return false;

            (bool blocked, string? resolvedReason) = EvaluateNearbyMonsterRestriction(entities, restrictionSettings);
            reason = resolvedReason;

            CacheNearbyMonsterRestriction(nowMs, restrictionSettings.Signature, blocked, reason);
            return blocked;
        }

        private bool TryGetCachedNearbyMonsterRestriction(
            long nowMs,
            int settingsSignature,
            out bool blocked,
            out string? reason)
        {
            bool cacheFresh = _cachedNearbyMonsterRestrictionTimestampMs != long.MinValue
                && (nowMs - _cachedNearbyMonsterRestrictionTimestampMs) <= NearbyMonsterRestrictionCacheDurationMs
                && _cachedNearbyMonsterRestrictionSettingsSignature == settingsSignature;
            if (!cacheFresh)
            {
                blocked = false;
                reason = null;
                return false;
            }

            blocked = _cachedNearbyMonsterRestrictionResult;
            reason = _cachedNearbyMonsterRestrictionReason;
            return true;
        }

        private void CacheNearbyMonsterRestriction(long nowMs, int settingsSignature, bool blocked, string? reason)
        {
            _cachedNearbyMonsterRestrictionTimestampMs = nowMs;
            _cachedNearbyMonsterRestrictionSettingsSignature = settingsSignature;
            _cachedNearbyMonsterRestrictionResult = blocked;
            _cachedNearbyMonsterRestrictionReason = reason;
        }

        internal static string? TryFindLockedChestRestrictionReason(
            IEnumerable<LabelOnGround> labels,
            int clickDistance)
        {
            foreach (LabelOnGround label in labels)
            {
                Entity? item = label?.ItemOnGround;
                if (item == null || item.DistancePlayer > clickDistance)
                    continue;

                string path = item.Path ?? string.Empty;
                if (path.Length == 0)
                    continue;

                Chest? chest = item.GetComponent<Chest>();
                if (chest?.IsLocked == true && !chest.IsStrongbox)
                    return $"Locked chest detected ({path})";
            }

            return null;
        }

        internal static string? TryFindLockedChestRestrictionReason(
            IEnumerable<LockedChestCandidate> candidates,
            int clickDistance)
        {
            foreach (LockedChestCandidate candidate in candidates)
            {
                if (candidate.DistancePlayer > clickDistance)
                    continue;

                if (string.IsNullOrEmpty(candidate.Path))
                    continue;

                if (candidate.IsLocked && !candidate.IsStrongbox)
                    return $"Locked chest detected ({candidate.Path})";
            }

            return null;
        }

        internal static (bool Blocked, string? Reason) EvaluateNearbyMonsterRestriction(
            IEnumerable<Entity?> entities,
            int normalThreshold,
            int normalDistance,
            int magicThreshold,
            int magicDistance,
            int rareThreshold,
            int rareDistance,
            int uniqueThreshold,
            int uniqueDistance)
        {
            NearbyMonsterRestrictionSettings restrictionSettings = BuildNearbyMonsterRestrictionSettings(
                normalThreshold,
                normalDistance,
                magicThreshold,
                magicDistance,
                rareThreshold,
                rareDistance,
                uniqueThreshold,
                uniqueDistance);
            return EvaluateNearbyMonsterRestriction(entities, restrictionSettings);
        }

        internal static (bool Blocked, string? Reason) EvaluateNearbyMonsterRestriction(
            IEnumerable<Entity?> entities,
            NearbyMonsterRestrictionSettings restrictionSettings)
        {
            if (!restrictionSettings.HasEnabledRestrictions)
                return (false, null);

            NearbyMonsterCounts counts = CountNearbyMonsters(entities, restrictionSettings);
            return FinalizeNearbyMonsterRestriction(restrictionSettings, counts);
        }

        internal static (bool Blocked, string? Reason) EvaluateNearbyMonsterRestriction(
            IEnumerable<NearbyMonsterCandidate> candidates,
            int normalThreshold,
            int normalDistance,
            int magicThreshold,
            int magicDistance,
            int rareThreshold,
            int rareDistance,
            int uniqueThreshold,
            int uniqueDistance)
        {
            NearbyMonsterRestrictionSettings restrictionSettings = BuildNearbyMonsterRestrictionSettings(
                normalThreshold,
                normalDistance,
                magicThreshold,
                magicDistance,
                rareThreshold,
                rareDistance,
                uniqueThreshold,
                uniqueDistance);
            return EvaluateNearbyMonsterRestriction(candidates, restrictionSettings);
        }

        internal static (bool Blocked, string? Reason) EvaluateNearbyMonsterRestriction(
            IEnumerable<NearbyMonsterCandidate> candidates,
            NearbyMonsterRestrictionSettings restrictionSettings)
        {
            if (!restrictionSettings.HasEnabledRestrictions)
                return (false, null);

            NearbyMonsterCounts counts = CountNearbyMonsters(candidates, restrictionSettings);
            return FinalizeNearbyMonsterRestriction(restrictionSettings, counts);
        }

        private static NearbyMonsterRestrictionSettings BuildNearbyMonsterRestrictionSettings(
            int normalThreshold,
            int normalDistance,
            int magicThreshold,
            int magicDistance,
            int rareThreshold,
            int rareDistance,
            int uniqueThreshold,
            int uniqueDistance)
        {
            bool normalEnabled = normalThreshold > 0;
            bool magicEnabled = magicThreshold > 0;
            bool rareEnabled = rareThreshold > 0;
            bool uniqueEnabled = uniqueThreshold > 0;

            return new NearbyMonsterRestrictionSettings(
                normalThreshold,
                normalDistance,
                magicThreshold,
                magicDistance,
                rareThreshold,
                rareDistance,
                uniqueThreshold,
                uniqueDistance,
                normalEnabled,
                magicEnabled,
                rareEnabled,
                uniqueEnabled,
                GetMaxRelevantNearbyMonsterDistance(
                    normalEnabled,
                    normalDistance,
                    magicEnabled,
                    magicDistance,
                    rareEnabled,
                    rareDistance,
                    uniqueEnabled,
                    uniqueDistance),
                HashCode.Combine(
                    normalThreshold,
                    normalDistance,
                    magicThreshold,
                    magicDistance,
                    rareThreshold,
                    rareDistance,
                    uniqueThreshold,
                    uniqueDistance));
        }

        private static NearbyMonsterCounts CountNearbyMonsters(
            IEnumerable<Entity?> entities,
            NearbyMonsterRestrictionSettings restrictionSettings)
        {
            NearbyMonsterCounts counts = default;

            foreach (Entity? entity in entities)
            {
                if (entity == null)
                    continue;

                CountNearbyMonster(
                    new NearbyMonsterCandidate(
                        entity.IsValid,
                        entity.Type,
                        entity.DistancePlayer,
                        entity.Rarity,
                        entity.IsAlive,
                        entity.IsHostile),
                    restrictionSettings,
                    ref counts);
            }

            return counts;
        }

        private static NearbyMonsterCounts CountNearbyMonsters(
            IEnumerable<NearbyMonsterCandidate> candidates,
            NearbyMonsterRestrictionSettings restrictionSettings)
        {
            NearbyMonsterCounts counts = default;

            foreach (NearbyMonsterCandidate candidate in candidates)
            {
                CountNearbyMonster(candidate, restrictionSettings, ref counts);
            }

            return counts;
        }

        private static void CountNearbyMonster(
            NearbyMonsterCandidate candidate,
            NearbyMonsterRestrictionSettings restrictionSettings,
            ref NearbyMonsterCounts counts)
        {
            if (!candidate.IsValid || candidate.Type != EntityType.Monster)
                return;

            float distancePlayer = candidate.DistancePlayer;
            if (distancePlayer < 0f || float.IsNaN(distancePlayer) || float.IsInfinity(distancePlayer))
                return;
            if (distancePlayer > restrictionSettings.MaxRelevantDistance)
                return;

            switch (candidate.Rarity)
            {
                case MonsterRarity.White:
                    if (restrictionSettings.NormalEnabled && distancePlayer <= restrictionSettings.NormalDistance && candidate.IsAlive && candidate.IsHostile)
                        counts = counts with { Normal = counts.Normal + 1 };
                    break;
                case MonsterRarity.Magic:
                    if (restrictionSettings.MagicEnabled && distancePlayer <= restrictionSettings.MagicDistance && candidate.IsAlive && candidate.IsHostile)
                        counts = counts with { Magic = counts.Magic + 1 };
                    break;
                case MonsterRarity.Rare:
                    if (restrictionSettings.RareEnabled && distancePlayer <= restrictionSettings.RareDistance && candidate.IsAlive && candidate.IsHostile)
                        counts = counts with { Rare = counts.Rare + 1 };
                    break;
                case MonsterRarity.Unique:
                    if (restrictionSettings.UniqueEnabled && distancePlayer <= restrictionSettings.UniqueDistance && candidate.IsAlive && candidate.IsHostile)
                        counts = counts with { Unique = counts.Unique + 1 };
                    break;
            }
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

        private static (bool Blocked, string? Reason) FinalizeNearbyMonsterRestriction(
            NearbyMonsterRestrictionSettings restrictionSettings,
            NearbyMonsterCounts counts)
        {
            bool normalTriggered = restrictionSettings.NormalThreshold > 0 && counts.Normal >= restrictionSettings.NormalThreshold;
            bool magicTriggered = restrictionSettings.MagicThreshold > 0 && counts.Magic >= restrictionSettings.MagicThreshold;
            bool rareTriggered = restrictionSettings.RareThreshold > 0 && counts.Rare >= restrictionSettings.RareThreshold;
            bool uniqueTriggered = restrictionSettings.UniqueThreshold > 0 && counts.Unique >= restrictionSettings.UniqueThreshold;
            bool blocked = normalTriggered || magicTriggered || rareTriggered || uniqueTriggered;
            string? reason = blocked
                ? BuildNearbyMonsterBlockReason(
                    counts.Normal,
                    restrictionSettings.NormalThreshold,
                    restrictionSettings.NormalDistance,
                    normalTriggered,
                    counts.Magic,
                    restrictionSettings.MagicThreshold,
                    restrictionSettings.MagicDistance,
                    magicTriggered,
                    counts.Rare,
                    restrictionSettings.RareThreshold,
                    restrictionSettings.RareDistance,
                    rareTriggered,
                    counts.Unique,
                    restrictionSettings.UniqueThreshold,
                    restrictionSettings.UniqueDistance,
                    uniqueTriggered)
                : null;

            return (blocked, reason);
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