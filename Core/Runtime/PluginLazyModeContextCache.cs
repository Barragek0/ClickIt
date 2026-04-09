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
        private long _lastRefreshTimestampMs = long.MinValue;
        private bool _cachedRitualActive;
        private bool _cachedHasLazyModeRestrictedItems;
        private bool _cachedRitualEvaluated;
        private bool _cachedRestrictedItemsEvaluated;
        private IReadOnlyList<LabelOnGround>? _cachedLabelsReference;
        private int _cachedLabelCount = -1;

        internal PluginLazyModeContextSnapshot GetContext(bool shouldEvaluateRitualState, bool shouldEvaluateRestrictedItems)
        {
            IReadOnlyList<LabelOnGround>? labels = _dependencies.GetLabels();
            int labelCount = labels?.Count ?? 0;
            long now = _dependencies.GetTimestampMs();

            bool cacheStillFresh = (now - _lastRefreshTimestampMs) < CacheWindowMs
                && ReferenceEquals(labels, _cachedLabelsReference)
                && labelCount == _cachedLabelCount;

            if (!cacheStillFresh)
            {
                _cachedLabelsReference = labels;
                _cachedLabelCount = labelCount;
                _lastRefreshTimestampMs = now;
                _cachedRitualEvaluated = false;
                _cachedRestrictedItemsEvaluated = false;
            }

            if (shouldEvaluateRitualState && !_cachedRitualEvaluated)
            {
                _cachedRitualActive = _dependencies.IsRitualActive();
                _cachedRitualEvaluated = true;
            }

            if (shouldEvaluateRestrictedItems && !_cachedRestrictedItemsEvaluated)
            {
                _cachedHasLazyModeRestrictedItems = _dependencies.HasLazyModeRestrictedItems(labels);
                _cachedRestrictedItemsEvaluated = true;
            }

            bool isRitualActive = shouldEvaluateRitualState && _cachedRitualActive;
            bool hasLazyModeRestrictedItems = shouldEvaluateRestrictedItems && _cachedHasLazyModeRestrictedItems;
            return new PluginLazyModeContextSnapshot(
                IsRitualActive: isRitualActive,
                HasLazyModeRestrictedItems: hasLazyModeRestrictedItems,
                Labels: labels);
        }
    }
}