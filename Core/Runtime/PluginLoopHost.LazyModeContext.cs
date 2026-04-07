namespace ClickIt.Core.Runtime
{
    public partial class PluginLoopHost
    {
        private PluginLazyModeContextCache? _lazyModeContextCache;

        private PluginLazyModeContextSnapshot ResolveRegularClickLazyModeContext(bool hotkeyActive)
        {
            bool lazyModeEnabled = _settings.LazyMode.Value;
            return LazyModeContextCache.GetContext(
                shouldEvaluateRitualState: PluginClickRuntimeStateEvaluator.ShouldEvaluateRitualState(lazyModeEnabled, hotkeyActive),
                shouldEvaluateRestrictedItems: PluginClickRuntimeStateEvaluator.ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled));
        }

        private PluginClickRuntimeStateSnapshot ResolveRitualAwareRuntimeState()
            => PluginClickRuntimeStateEvaluator.ResolveSnapshot(
                _settings,
                _state.Services.InputHandler,
                _gameController,
                LazyModeContextCache.GetContext(
                    shouldEvaluateRitualState: true,
                    shouldEvaluateRestrictedItems: false));

        private PluginLazyModeContextCache LazyModeContextCache
            => _lazyModeContextCache ??= new(new PluginLazyModeContextCacheDependencies(
                _settings,
                GetLabels: () => _state.Services.CachedLabels?.Value,
                IsRitualActive: () => PluginClickRuntimeStateEvaluator.ResolveIsRitualActive(_gameController),
                HasLazyModeRestrictedItems: labels => PluginClickRuntimeStateEvaluator.ResolveHasLazyModeRestrictedItems(_state.Services.LazyModeBlockerService, labels),
                GetTimestampMs: () => Environment.TickCount64));
    }
}