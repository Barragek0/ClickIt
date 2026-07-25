namespace ClickIt.Features.Observability
{
    internal sealed record ClickFrequencyTargetTelemetrySnapshot(
        bool SettingsAvailable,
        double ClickTargetMs,
        double LazyModeTargetMs,
        bool ShowLazyModeTarget)
    {
        public double TargetIntervalMs => ShowLazyModeTarget ? LazyModeTargetMs : ClickTargetMs;

        public static readonly ClickFrequencyTargetTelemetrySnapshot Empty = new(
            SettingsAvailable: false,
            ClickTargetMs: 0,
            LazyModeTargetMs: 0,
            ShowLazyModeTarget: false);
    }
}