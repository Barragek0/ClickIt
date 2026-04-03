namespace ClickIt.Core.Runtime
{
    public partial class PluginLoopHost
    {
        private const int LazyModeContextCacheMs = 50;
        private long _lastLazyModeContextRefreshTimestampMs = long.MinValue;
        private bool _cachedRitualActive;
        private bool _cachedHasLazyModeRestrictedItems;
        private bool _cachedRitualEvaluated;
        private bool _cachedRestrictedItemsEvaluated;
        private IReadOnlyList<LabelOnGround>? _cachedLazyModeLabelsRef;
        private int _cachedLazyModeLabelCount = -1;

        private bool IsRitualActive()
            => EntityHelpers.IsRitualActive(_gameController);

        private (bool IsRitualActive, bool HasLazyModeRestrictedItems, IReadOnlyList<LabelOnGround>? Labels) GetCachedLazyModeContext(
            bool shouldEvaluateRitualState,
            bool shouldEvaluateRestrictedItems)
        {
            IReadOnlyList<LabelOnGround>? labels = _state.Services.CachedLabels?.Value;
            int labelCount = labels?.Count ?? 0;
            long now = Environment.TickCount64;

            bool cacheStillFresh = (now - _lastLazyModeContextRefreshTimestampMs) < LazyModeContextCacheMs
                && ReferenceEquals(labels, _cachedLazyModeLabelsRef)
                && labelCount == _cachedLazyModeLabelCount;

            if (!cacheStillFresh)
            {
                _cachedLazyModeLabelsRef = labels;
                _cachedLazyModeLabelCount = labelCount;
                _lastLazyModeContextRefreshTimestampMs = now;
                _cachedRitualEvaluated = false;
                _cachedRestrictedItemsEvaluated = false;
            }

            if (shouldEvaluateRitualState && !_cachedRitualEvaluated)
            {
                _cachedRitualActive = IsRitualActive();
                _cachedRitualEvaluated = true;
            }

            if (shouldEvaluateRestrictedItems && !_cachedRestrictedItemsEvaluated)
            {
                _cachedHasLazyModeRestrictedItems = _state.Services.LabelFilterPort?.HasLazyModeRestrictedItemsOnScreen(labels) ?? false;
                _cachedRestrictedItemsEvaluated = true;
            }

            return (
                shouldEvaluateRitualState ? _cachedRitualActive : false,
                shouldEvaluateRestrictedItems ? _cachedHasLazyModeRestrictedItems : false,
                labels);
        }
    }
}