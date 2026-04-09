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

    internal readonly record struct NearbyMonsterThresholdState(
        string Label,
        int Count,
        int Threshold,
        int Distance,
        bool Triggered);

    internal readonly record struct LazyModeRestrictionResult(
        bool Blocked,
        string? Reason)
    {
        internal string EffectiveReason
            => string.IsNullOrWhiteSpace(Reason)
                ? "Nearby monster threshold reached"
                : Reason;
    }

    internal readonly record struct NearbyMonsterRestrictionCacheState(
        long TimestampMs,
        int SettingsSignature,
        LazyModeRestrictionResult Result)
    {
        internal static NearbyMonsterRestrictionCacheState Empty
            => new(long.MinValue, 0, default);

        internal bool IsFresh(long nowMs, int settingsSignature)
            => TimestampMs != long.MinValue
                && (nowMs - TimestampMs) <= LazyModeBlockerService.NearbyMonsterRestrictionCacheDurationMs
                && SettingsSignature == settingsSignature;
    }

    public sealed class LazyModeBlockerService(
        ClickItSettings settings,
        GameController? gameController,
        Action<string> logRestriction,
        Func<long>? nowProvider = null)
    {
        internal const int NearbyMonsterRestrictionCacheDurationMs = 50;
        private const int LazyModeRestrictionLogThrottleMs = 500;

        private readonly ClickItSettings _settings = settings;
        private readonly GameController? _gameController = gameController;
        private readonly Action<string> _logRestriction = logRestriction;
        private readonly Func<long> _nowProvider = nowProvider ?? (() => Environment.TickCount64);
        private NearbyMonsterRestrictionCacheState _cachedNearbyMonsterRestrictionCacheState = NearbyMonsterRestrictionCacheState.Empty;
        private long _lastLazyModeRestrictionLogTimestampMs = long.MinValue;
        private string _lastLazyModeRestrictionLogReason = string.Empty;
        public string? LastRestrictionReason { get; private set; }

        public bool HasRestrictedItemsOnScreen(IReadOnlyList<LabelOnGround>? allLabels)
        {
            LazyModeRestrictionResult restriction = ResolveRestriction(allLabels);
            LastRestrictionReason = restriction.Blocked
                ? restriction.EffectiveReason
                : null;
            if (!restriction.Blocked)
                return false;

            TryLogLazyModeRestriction(restriction.EffectiveReason);
            return true;
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

        private LazyModeRestrictionResult ResolveRestriction(IReadOnlyList<LabelOnGround>? allLabels)
        {
            LazyModeRestrictionResult nearbyMonsterRestriction = ResolveNearbyMonsterRestriction();
            if (nearbyMonsterRestriction.Blocked)
                return nearbyMonsterRestriction;

            return ResolveLockedChestRestriction(allLabels);
        }

        private LazyModeRestrictionResult ResolveLockedChestRestriction(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null)
                return default;

            string? reason = TryFindLockedChestRestrictionReason(
                allLabels,
                _settings.ClickDistance.Value);
            return reason == null
                ? default
                : new LazyModeRestrictionResult(true, reason);
        }

        private LazyModeRestrictionResult ResolveNearbyMonsterRestriction()
        {
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
                return default;

            long nowMs = _nowProvider();
            if (TryGetCachedNearbyMonsterRestriction(nowMs, restrictionSettings.Signature, out LazyModeRestrictionResult cachedRestriction))
                return cachedRestriction;

            List<Entity>? entities = _gameController?.EntityListWrapper?.OnlyValidEntities;
            if (entities == null)
                return default;

            LazyModeRestrictionResult restriction = EvaluateNearbyMonsterRestrictionResult(entities, restrictionSettings);
            CacheNearbyMonsterRestriction(nowMs, restrictionSettings.Signature, restriction);
            return restriction;
        }

        private bool TryGetCachedNearbyMonsterRestriction(
            long nowMs,
            int settingsSignature,
            out LazyModeRestrictionResult restriction)
        {
            if (!_cachedNearbyMonsterRestrictionCacheState.IsFresh(nowMs, settingsSignature))
            {
                restriction = default;
                return false;
            }

            restriction = _cachedNearbyMonsterRestrictionCacheState.Result;
            return true;
        }

        private void CacheNearbyMonsterRestriction(long nowMs, int settingsSignature, LazyModeRestrictionResult restriction)
        {
            _cachedNearbyMonsterRestrictionCacheState = new NearbyMonsterRestrictionCacheState(
                nowMs,
                settingsSignature,
                restriction);
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

            return ToLegacyRestrictionResult(EvaluateNearbyMonsterRestrictionResult(entities, restrictionSettings));
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

            return ToLegacyRestrictionResult(EvaluateNearbyMonsterRestrictionResult(candidates, restrictionSettings));
        }

        private static LazyModeRestrictionResult EvaluateNearbyMonsterRestrictionResult(
            IEnumerable<Entity?> entities,
            NearbyMonsterRestrictionSettings restrictionSettings)
        {
            NearbyMonsterCounts counts = CountNearbyMonsters(entities, restrictionSettings);
            return EvaluateNearbyMonsterRestrictionResult(restrictionSettings, counts);
        }

        private static LazyModeRestrictionResult EvaluateNearbyMonsterRestrictionResult(
            IEnumerable<NearbyMonsterCandidate> candidates,
            NearbyMonsterRestrictionSettings restrictionSettings)
        {
            NearbyMonsterCounts counts = CountNearbyMonsters(candidates, restrictionSettings);
            return EvaluateNearbyMonsterRestrictionResult(restrictionSettings, counts);
        }

        private static LazyModeRestrictionResult EvaluateNearbyMonsterRestrictionResult(
            NearbyMonsterRestrictionSettings restrictionSettings,
            NearbyMonsterCounts counts)
        {
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
            if (!IsEligibleNearbyMonsterCandidate(candidate, restrictionSettings))
                return;

            if (!TryGetNearbyMonsterBucket(candidate, restrictionSettings, out NearbyMonsterThresholdBucket bucket))
                return;

            switch (bucket)
            {
                case NearbyMonsterThresholdBucket.Normal:
                    counts = counts with { Normal = counts.Normal + 1 };
                    break;
                case NearbyMonsterThresholdBucket.Magic:
                    counts = counts with { Magic = counts.Magic + 1 };
                    break;
                case NearbyMonsterThresholdBucket.Rare:
                    counts = counts with { Rare = counts.Rare + 1 };
                    break;
                case NearbyMonsterThresholdBucket.Unique:
                    counts = counts with { Unique = counts.Unique + 1 };
                    break;
                case NearbyMonsterThresholdBucket.None:
                    break;
                default:
                    break;
            }
        }

        private static bool IsEligibleNearbyMonsterCandidate(
            NearbyMonsterCandidate candidate,
            NearbyMonsterRestrictionSettings restrictionSettings)
        {
            if (!candidate.IsValid || candidate.Type != EntityType.Monster)
                return false;

            float distancePlayer = candidate.DistancePlayer;
            if (distancePlayer < 0f || float.IsNaN(distancePlayer) || float.IsInfinity(distancePlayer))
                return false;

            if (distancePlayer > restrictionSettings.MaxRelevantDistance)
                return false;

            return candidate.IsAlive && candidate.IsHostile;
        }

        private static bool TryGetNearbyMonsterBucket(
            NearbyMonsterCandidate candidate,
            NearbyMonsterRestrictionSettings restrictionSettings,
            out NearbyMonsterThresholdBucket bucket)
        {
            float distancePlayer = candidate.DistancePlayer;

            switch (candidate.Rarity)
            {
                case MonsterRarity.White when restrictionSettings.NormalEnabled && distancePlayer <= restrictionSettings.NormalDistance:
                    bucket = NearbyMonsterThresholdBucket.Normal;
                    return true;
                case MonsterRarity.Magic when restrictionSettings.MagicEnabled && distancePlayer <= restrictionSettings.MagicDistance:
                    bucket = NearbyMonsterThresholdBucket.Magic;
                    return true;
                case MonsterRarity.Rare when restrictionSettings.RareEnabled && distancePlayer <= restrictionSettings.RareDistance:
                    bucket = NearbyMonsterThresholdBucket.Rare;
                    return true;
                case MonsterRarity.Unique when restrictionSettings.UniqueEnabled && distancePlayer <= restrictionSettings.UniqueDistance:
                    bucket = NearbyMonsterThresholdBucket.Unique;
                    return true;
                case MonsterRarity.White:
                case MonsterRarity.Magic:
                case MonsterRarity.Rare:
                case MonsterRarity.Unique:
                case MonsterRarity.Error:
                default:
                    bucket = NearbyMonsterThresholdBucket.None;
                    return false;
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
                maxDistance = SystemMath.Max(maxDistance, normalDistance);
            if (magicEnabled)
                maxDistance = SystemMath.Max(maxDistance, magicDistance);
            if (rareEnabled)
                maxDistance = SystemMath.Max(maxDistance, rareDistance);
            if (uniqueEnabled)
                maxDistance = SystemMath.Max(maxDistance, uniqueDistance);
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

        private static LazyModeRestrictionResult FinalizeNearbyMonsterRestriction(
            NearbyMonsterRestrictionSettings restrictionSettings,
            NearbyMonsterCounts counts)
        {
            NearbyMonsterThresholdState normalState = BuildThresholdState(
                "Normal",
                counts.Normal,
                restrictionSettings.NormalThreshold,
                restrictionSettings.NormalDistance);
            NearbyMonsterThresholdState magicState = BuildThresholdState(
                "Magic",
                counts.Magic,
                restrictionSettings.MagicThreshold,
                restrictionSettings.MagicDistance);
            NearbyMonsterThresholdState rareState = BuildThresholdState(
                "Rare",
                counts.Rare,
                restrictionSettings.RareThreshold,
                restrictionSettings.RareDistance);
            NearbyMonsterThresholdState uniqueState = BuildThresholdState(
                "Unique",
                counts.Unique,
                restrictionSettings.UniqueThreshold,
                restrictionSettings.UniqueDistance);

            bool blocked = normalState.Triggered || magicState.Triggered || rareState.Triggered || uniqueState.Triggered;
            if (!blocked)
                return default;

            return new LazyModeRestrictionResult(
                true,
                BuildNearbyMonsterBlockReason(normalState, magicState, rareState, uniqueState));
        }

        private static (bool Blocked, string? Reason) ToLegacyRestrictionResult(LazyModeRestrictionResult restriction)
            => (restriction.Blocked, restriction.Reason);

        private static NearbyMonsterThresholdState BuildThresholdState(
            string label,
            int count,
            int threshold,
            int distance)
        {
            return new NearbyMonsterThresholdState(
                label,
                count,
                threshold,
                distance,
                threshold > 0 && count >= threshold);
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
            return BuildNearbyMonsterBlockReason(
                new NearbyMonsterThresholdState("Normal", nearbyNormalCount, normalThreshold, normalDistance, normalTriggered),
                new NearbyMonsterThresholdState("Magic", nearbyMagicCount, magicThreshold, magicDistance, magicTriggered),
                new NearbyMonsterThresholdState("Rare", nearbyRareCount, rareThreshold, rareDistance, rareTriggered),
                new NearbyMonsterThresholdState("Unique", nearbyUniqueCount, uniqueThreshold, uniqueDistance, uniqueTriggered));
        }

        private static string BuildNearbyMonsterBlockReason(
            NearbyMonsterThresholdState normalState,
            NearbyMonsterThresholdState magicState,
            NearbyMonsterThresholdState rareState,
            NearbyMonsterThresholdState uniqueState)
        {
            List<string> segments = [];

            AppendTriggeredThresholdSegment(segments, normalState);
            AppendTriggeredThresholdSegment(segments, magicState);
            AppendTriggeredThresholdSegment(segments, rareState);
            AppendTriggeredThresholdSegment(segments, uniqueState);

            return segments.Count == 0
                ? "Nearby monster threshold reached"
                : string.Join(", ", segments);
        }

        private static void AppendTriggeredThresholdSegment(
            List<string> segments,
            NearbyMonsterThresholdState state)
        {
            if (!state.Triggered)
                return;

            segments.Add($"{state.Label} {state.Count}/{state.Threshold} within {state.Distance}");
        }

        private enum NearbyMonsterThresholdBucket
        {
            Normal,
            Magic,
            Rare,
            Unique,
            None,
        }
    }
}