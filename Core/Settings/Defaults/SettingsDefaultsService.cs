namespace ClickIt
{
    internal sealed class SettingsDefaultsService : ISettingsDefaultsService
    {
        public void Apply(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.InitializeDefaultsForMigration();
        }
    }
}
