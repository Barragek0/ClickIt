namespace ClickIt.Core.Settings.Runtime
{
    internal sealed class ClickItSettingsTransientState
    {
        internal ClickItSettingsUiState UiState { get; } = new();
        internal volatile string? DistancePreview;
        internal ClickItSettingsRuntimeCacheState RuntimeCache { get; } = new();
    }
}