using System;
using System.Runtime.Serialization;

namespace ClickIt
{
    internal static class ClickItSettingsMigrationService
    {
        internal const int CurrentVersion = 1;
        private static readonly ISettingsDefaultsService DefaultsService = new SettingsDefaultsService();
        private static readonly ISettingsNormalizationService NormalizationService = new SettingsNormalizationService();

        internal static void Apply(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            DefaultsService.Apply(settings);
            NormalizationService.Apply(settings);
            settings.SettingsVersion = CurrentVersion;
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