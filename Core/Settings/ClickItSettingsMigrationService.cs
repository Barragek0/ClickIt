using System;
using System.Runtime.Serialization;

namespace ClickIt
{
    internal static class ClickItSettingsMigrationService
    {
        internal const int CurrentVersion = 1;

        internal static void Apply(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            settings.ApplySettingsMigrationCore();
            settings.SettingsVersion = CurrentVersion;
        }
    }

    public partial class ClickItSettings
    {
        internal void ApplySettingsMigrationCore()
        {
            EnsureItemTypeFiltersInitialized();
            EnsureMechanicPrioritiesInitialized();
            EnsureEssenceCorruptionFiltersInitialized();
            EnsureStrongboxFiltersInitialized();
            EnsureUltimatumModifiersInitialized();
            EnsureUltimatumTakeRewardModifiersInitialized();
            EnsureLazyModeNearbyMonsterFiltersInitialized();
        }

        [OnDeserialized]
        internal void OnDeserializedApplySettingsMigration(StreamingContext context)
        {
            ClickItSettingsMigrationService.Apply(this);
        }
    }
}