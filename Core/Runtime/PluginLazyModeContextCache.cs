namespace ClickIt.Core.Runtime
{
    internal readonly record struct PluginLazyModeContextSnapshot(
        bool IsRitualActive,
        bool HasLazyModeRestrictedItems,
        IReadOnlyList<LabelOnGround>? Labels);

    internal readonly record struct PluginLazyModeContextCacheDependencies(
        ClickItSettings Settings,
        Func<IReadOnlyList<LabelOnGround>?> GetLabels,
        Func<bool> IsRitualActive,
        Func<IReadOnlyList<LabelOnGround>?, bool> HasLazyModeRestrictedItems,
        Func<long> GetTimestampMs);

    internal sealed class PluginLazyModeContextCache(PluginLazyModeContextCacheDependencies dependencies)
    {
        private const int CacheWindowMs = 50;

        private readonly PluginLazyModeContextCacheDependencies _dependencies = dependencies;
        private readonly TimedValueCache<LazyModeContextCacheKey, LazyModeContextCacheState> _contextCache = new(
            CacheWindowMs,
            keyComparer: LazyModeContextCacheKeyComparer.Instance,
            settings: new TimedValueCacheSettings(RequireNonNegativeAge: true));

        internal PluginLazyModeContextSnapshot GetContext(bool shouldEvaluateRitualState, bool shouldEvaluateRestrictedItems)
        {
            IReadOnlyList<LabelOnGround>? labels = _dependencies.GetLabels();
            int labelCount = labels?.Count ?? 0;
            long now = _dependencies.GetTimestampMs();
            LazyModeContextCacheKey key = new(labels, labelCount);

            _contextCache.TryGetValue(key, now, out LazyModeContextCacheState cacheState);

            if (shouldEvaluateRitualState && !cacheState.RitualEvaluated)
            {
                cacheState = cacheState with
                {
                    RitualActive = _dependencies.IsRitualActive(),
                    RitualEvaluated = true
                };
            }

            if (shouldEvaluateRestrictedItems && !cacheState.RestrictedItemsEvaluated)
            {
                cacheState = cacheState with
                {
                    HasLazyModeRestrictedItems = _dependencies.HasLazyModeRestrictedItems(labels),
                    RestrictedItemsEvaluated = true
                };
            }

            _contextCache.SetValue(key, now, cacheState);

            bool isRitualActive = shouldEvaluateRitualState && cacheState.RitualActive;
            bool hasLazyModeRestrictedItems = shouldEvaluateRestrictedItems && cacheState.HasLazyModeRestrictedItems;
            return new PluginLazyModeContextSnapshot(
                IsRitualActive: isRitualActive,
                HasLazyModeRestrictedItems: hasLazyModeRestrictedItems,
                Labels: labels);
        }

        private readonly record struct LazyModeContextCacheKey(
            IReadOnlyList<LabelOnGround>? Labels,
            int LabelCount);

        private readonly record struct LazyModeContextCacheState(
            bool RitualActive,
            bool HasLazyModeRestrictedItems,
            bool RitualEvaluated,
            bool RestrictedItemsEvaluated);

        private sealed class LazyModeContextCacheKeyComparer : IEqualityComparer<LazyModeContextCacheKey>
        {
            public static readonly LazyModeContextCacheKeyComparer Instance = new();

            public bool Equals(LazyModeContextCacheKey x, LazyModeContextCacheKey y)
                => ReferenceEquals(x.Labels, y.Labels)
                    && x.LabelCount == y.LabelCount;

            public int GetHashCode(LazyModeContextCacheKey obj)
                => HashCode.Combine(obj.Labels == null ? 0 : RuntimeHelpers.GetHashCode(obj.Labels), obj.LabelCount);
        }
    }
}