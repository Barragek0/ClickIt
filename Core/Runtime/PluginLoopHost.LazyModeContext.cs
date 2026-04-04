namespace ClickIt.Core.Runtime
{
    public partial class PluginLoopHost
    {
        private PluginLazyModeContextCache? _lazyModeContextCache;

        private PluginLazyModeContextCache LazyModeContextCache
            => _lazyModeContextCache ??= new(new PluginLazyModeContextCacheDependencies(
                _settings,
                GetLabels: () => _state.Services.CachedLabels?.Value,
                IsRitualActive: () => PluginClickRuntimeStateEvaluator.ResolveIsRitualActive(_gameController),
                HasLazyModeRestrictedItems: labels => PluginClickRuntimeStateEvaluator.ResolveHasLazyModeRestrictedItems(_state.Services.LabelFilterPort, labels),
                GetTimestampMs: () => Environment.TickCount64));
    }
}