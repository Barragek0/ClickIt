using System.IO;

namespace ClickIt
{
    public partial class ClickIt
    {
        private bool _testDisableAutoDownload = false;
        internal void __Test_SetDisableAutoDownload(bool value)
        {
            _testDisableAutoDownload = value;
        }

        internal bool __Test_GetDisableAutoDownload()
        {
            return _testDisableAutoDownload;
        }

        private string? _testConfigDirectory = null;
        private Services.AlertService? _seamAlertService;

        internal void __Test_SetConfigDirectory(string? path)
        {
            _testConfigDirectory = path;
        }

        internal string? __Test_GetConfigDirectory()
        {
            return _testConfigDirectory;
        }

        private Services.AlertService GetOrCreateAlertService()
        {
            // Some tests instantiate ClickIt via RuntimeHelpers.GetUninitializedObject where instance initializers don't run.
            // In that case State can be null, so use a seam-local service fallback just for tests.
            if (State == null)
            {
                _seamAlertService ??= new Services.AlertService(
                    () => Settings,
                    () => EffectiveSettings,
                    SafeGetConfigDirectory,
                    __Test_GetConfigDirectory,
                    __Test_GetDisableAutoDownload,
                    () => GameController,
                    LogMessage,
                    LogError);

                return _seamAlertService;
            }

            State.AlertService ??= new Services.AlertService(
                () => Settings,
                () => EffectiveSettings,
                SafeGetConfigDirectory,
                __Test_GetConfigDirectory,
                __Test_GetDisableAutoDownload,
                () => GameController,
                LogMessage,
                LogError);

            return State.AlertService;
        }

        private string SafeGetConfigDirectory()
        {
            try
            {
                return ConfigDirectory;
            }
            catch
            {
                return Path.GetTempPath();
            }
        }

        internal Services.AlertService __Test_GetAlertService()
        {
            return GetOrCreateAlertService();
        }

        internal Services.AlertService GetAlertService()
        {
            return GetOrCreateAlertService();
        }
    }
}
