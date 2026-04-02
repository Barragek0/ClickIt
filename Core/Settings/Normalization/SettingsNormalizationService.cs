namespace ClickIt
{
    internal sealed class SettingsNormalizationService : ISettingsNormalizationService
    {
        public void Apply(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.NormalizeForMigration();
        }
    }
}
