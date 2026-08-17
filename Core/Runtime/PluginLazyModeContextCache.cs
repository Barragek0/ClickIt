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
        private LazyModeContextCacheState _cached;
        private IReadOnlyList<LabelOnGround>? _cachedLabels;
        private long _cachedAtMs = long.MinValue;

        internal PluginLazyModeContextSnapshot GetContext(bool shouldEvaluateRitualState, bool shouldEvaluateRestrictedItems)
        {
            IReadOnlyList<LabelOnGround>? labels = _dependencies.GetLabels();
            long now = _dependencies.GetTimestampMs();

            // A single 50ms window slot: a changed label reference (or a clock step) resets the slot so the evaluated flags never outlive their inputs.
            if (now < _cachedAtMs || now - _cachedAtMs >= CacheWindowMs || !ReferenceEquals(labels, _cachedLabels))
            {
                _cached = default;
                _cachedLabels = labels;
                _cachedAtMs = now;
            }

            if (shouldEvaluateRitualState && !_cached.RitualEvaluated)
                _cached = _cached with { RitualActive = _dependencies.IsRitualActive(), RitualEvaluated = true };

            if (shouldEvaluateRestrictedItems && !_cached.RestrictedItemsEvaluated)
                _cached = _cached with { HasLazyModeRestrictedItems = _dependencies.HasLazyModeRestrictedItems(labels), RestrictedItemsEvaluated = true };

            return new PluginLazyModeContextSnapshot(
                IsRitualActive: shouldEvaluateRitualState && _cached.RitualActive,
                HasLazyModeRestrictedItems: shouldEvaluateRestrictedItems && _cached.HasLazyModeRestrictedItems,
                Labels: labels);
        }

        private readonly record struct LazyModeContextCacheState(
            bool RitualActive,
            bool HasLazyModeRestrictedItems,
            bool RitualEvaluated,
            bool RestrictedItemsEvaluated);
    }
}