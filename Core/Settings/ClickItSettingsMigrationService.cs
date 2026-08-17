namespace ClickIt
{
    internal static class ClickItSettingsMigrationService
    {
        private static readonly SettingsDefaultsService DefaultsService = new();
        private static readonly SettingsNormalizationService NormalizationService = new();

        internal static void Apply(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            DefaultsService.Apply(settings);
            NormalizationService.Apply(settings);
        }
    }

    public partial class ClickItSettings
    {
        [OnDeserialized]
        internal void OnDeserializedApplySettingsMigration(StreamingContext context)
        {
            ClickItSettingsMigrationService.Apply(this);
        }
    }
}