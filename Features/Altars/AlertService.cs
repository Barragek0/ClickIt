using ExileCore;
using System.Diagnostics;

namespace ClickIt.Features.Altars
{
    public class AlertService(
        Func<ClickItSettings?> settingsProvider,
        Func<ClickItSettings> effectiveSettingsProvider,
        Func<string> configDirectoryProvider,
        Func<GameController?> gameControllerProvider,
        Action<string, int> logMessage,
        Action<string, int> logError)
    {
        private readonly Func<ClickItSettings?> _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        private readonly Func<ClickItSettings> _effectiveSettingsProvider = effectiveSettingsProvider ?? throw new ArgumentNullException(nameof(effectiveSettingsProvider));
        private readonly Func<string> _configDirectoryProvider = configDirectoryProvider ?? throw new ArgumentNullException(nameof(configDirectoryProvider));
        private readonly Func<GameController?> _gameControllerProvider = gameControllerProvider ?? throw new ArgumentNullException(nameof(gameControllerProvider));
        private readonly Action<string, int> _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
        private readonly Action<string, int> _logError = logError ?? throw new ArgumentNullException(nameof(logError));

        private readonly Dictionary<string, DateTime> _lastAlertTimes = new(StringComparer.OrdinalIgnoreCase);
        private string? _alertSoundPath;

        internal Dictionary<string, DateTime> LastAlertTimes => _lastAlertTimes;
        internal string? CurrentAlertSoundPath => _alertSoundPath;
        internal void SetAlertSoundPathOverride(string? path) => _alertSoundPath = path;

        private const string AlertFileName = "alert.wav";
        private const string AlertDownloadUrl = "https://raw.githubusercontent.com/Barragek0/ClickIt/main/alert.wav";

        public void ReloadAlertSound()
        {
            try
            {
                var configDir = _configDirectoryProvider();
                var file = Path.Join(configDir, AlertFileName);
                if (!File.Exists(file))
                {
                    _logMessage("Alert sound not found in config directory.", 5);

                    bool tryDownload = _settingsProvider()?.AutoDownloadAlertSound?.Value == true;

                    if (tryDownload)
                    {
                        TryDownloadDefaultAlert(file);
                        if (!string.IsNullOrEmpty(_alertSoundPath))
                        {
                            return;
                        }
                    }

                    _alertSoundPath = null;
                    return;
                }

                _alertSoundPath = file;
                _logMessage($"Alert sound loaded: {file}", 5);
            }
            catch (Exception ex)
            {
                _logError("Failed to reload alert sound: " + ex.Message, 5);
            }
        }

        public void OpenConfigDirectory()
        {
            Process.Start("explorer.exe", _configDirectoryProvider());
        }

        public void TryTriggerAlertForMatchedMod(string matchedId)
        {
            if (string.IsNullOrEmpty(matchedId)) return;

            string? key = ResolveCompositeKey(matchedId);
            if (string.IsNullOrEmpty(key)) return;

            if (!IsAlertEnabledForKey(key)) return;

            if (!CanTriggerForKey(key)) return;

            EnsureAlertLoaded();
            string? path = _alertSoundPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _logError($"No alert sound loaded (expected '{AlertFileName}' in the config directory or plugin folder).", 20);
                return;
            }

            PlaySoundFile(path);
            _lastAlertTimes[key] = DateTime.UtcNow;
        }

        public string? ResolveCompositeKey(string matchedId)
        {
            string key = matchedId;
            var effectiveSettings = _effectiveSettingsProvider();
            if (!effectiveSettings.ModAlerts.ContainsKey(key))
            {
                string suffix = "|" + matchedId;
                foreach (string existingKey in effectiveSettings.ModAlerts.Keys)
                {
                    if (!existingKey.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    key = existingKey;
                    break;
                }
            }

            return key;
        }

        public bool IsAlertEnabledForKey(string key)
        {
            return _effectiveSettingsProvider().ModAlerts.TryGetValue(key, out bool enabled) && enabled;
        }

        public bool CanTriggerForKey(string key)
        {
            var now = DateTime.UtcNow;
            if (_lastAlertTimes.TryGetValue(key, out DateTime last) && (now - last).TotalSeconds < 30)
                return false;
            return true;
        }

        public void EnsureAlertLoaded()
        {
            string? path = _alertSoundPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return;
            ReloadAlertSound();
        }

        public void PlaySoundFile(string path)
        {
            var gameController = _gameControllerProvider();
            if (gameController?.SoundController != null)
            {
                gameController.SoundController.PlaySound(path, _effectiveSettingsProvider().AlertSoundVolume?.Value ?? 5);
            }
        }

        private void TryDownloadDefaultAlert(string file)
        {
            try
            {
                _logMessage("Attempting to download default alert sound from GitHub...", 10);
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(5);
                    var resp = http.GetAsync(AlertDownloadUrl).GetAwaiter().GetResult();
                    if (resp.IsSuccessStatusCode)
                    {
                        var bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        File.WriteAllBytes(file, bytes);
                        _logMessage($"Downloaded default alert sound to {file}", 20);
                        _alertSoundPath = file;
                        return;
                    }

                    _logError($"Failed to download alert sound: server returned {(int)resp.StatusCode}", 20);
                }
            }
            catch (Exception ex)
            {
                _logError($"Downloading alert sound failed: {ex.Message}", 20);
            }
        }
    }
}