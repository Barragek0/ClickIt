namespace ClickIt.Core.Runtime
{
    internal static class PluginAlertServiceHost
    {
        public static AlertService GetOrCreateAlertService(ClickIt owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            owner.State.Services.AlertService ??= new AlertService(
                () => owner.Settings,
                owner.GetEffectiveSettingsForLifecycle,
                () => SafeGetConfigDirectory(owner),
                () => owner.GameController,
                owner.LogMessage,
                owner.LogError);

            return owner.State.Services.AlertService;
        }

        private static string SafeGetConfigDirectory(ClickIt owner)
        {
            try
            {
                return owner.ConfigDirectory;
            }
            catch
            {
                return Path.GetTempPath();
            }
        }
    }
}