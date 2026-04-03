namespace ClickIt.Core.Settings.Normalization
{
    internal sealed class SettingsNormalizationService : ISettingsNormalizationService
    {
        private const int LazyModeNearbyMonsterCountMin = 0;
        private const int LazyModeNearbyMonsterCountMax = 200;
        private const int LazyModeNearbyMonsterDistanceMin = 1;
        private const int LazyModeNearbyMonsterDistanceMax = 300;

        public void Apply(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            EnsureLazyModeNearbyMonsterFiltersInitialized(settings);
        }

        internal static void EnsureLazyModeNearbyMonsterFiltersInitialized(ClickItSettings settings)
        {
            settings.LazyModeNormalMonsterBlockCount = SanitizeLazyModeNearbyMonsterCount(settings.LazyModeNormalMonsterBlockCount);
            settings.LazyModeNormalMonsterBlockDistance = SanitizeLazyModeNearbyMonsterDistance(settings.LazyModeNormalMonsterBlockDistance);

            settings.LazyModeMagicMonsterBlockCount = SanitizeLazyModeNearbyMonsterCount(settings.LazyModeMagicMonsterBlockCount);
            settings.LazyModeMagicMonsterBlockDistance = SanitizeLazyModeNearbyMonsterDistance(settings.LazyModeMagicMonsterBlockDistance);

            settings.LazyModeRareMonsterBlockCount = SanitizeLazyModeNearbyMonsterCount(settings.LazyModeRareMonsterBlockCount);
            settings.LazyModeRareMonsterBlockDistance = SanitizeLazyModeNearbyMonsterDistance(settings.LazyModeRareMonsterBlockDistance);

            settings.LazyModeUniqueMonsterBlockCount = SanitizeLazyModeNearbyMonsterCount(settings.LazyModeUniqueMonsterBlockCount);
            settings.LazyModeUniqueMonsterBlockDistance = SanitizeLazyModeNearbyMonsterDistance(settings.LazyModeUniqueMonsterBlockDistance);
        }

        internal static int SanitizeLazyModeNearbyMonsterCount(int value)
            => Math.Clamp(value, LazyModeNearbyMonsterCountMin, LazyModeNearbyMonsterCountMax);

        internal static int SanitizeLazyModeNearbyMonsterDistance(int value)
            => Math.Clamp(value, LazyModeNearbyMonsterDistanceMin, LazyModeNearbyMonsterDistanceMax);
    }
}
