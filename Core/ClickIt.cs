using ExileCore;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();
        private ClickItSettings? _testSettingsForTests;
        private bool _testDisableAutoDownload;
        private string? _testConfigDirectory;
        private Services.AlertService? _seamAlertService;

        private ClickItSettings EffectiveSettings => _testSettingsForTests ?? Settings;

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
        }

        internal ClickItSettings __Test_GetSettings()
        {
            return _testSettingsForTests ?? Settings ?? new ClickItSettings();
        }

        internal void __Test_SetSettings(ClickItSettings settings)
        {
            _testSettingsForTests = settings;

            if (TrySetViaSettingsProperty(settings))
                return;

            TrySetViaLikelyFields(settings);
        }

        internal void __Test_SetDisableAutoDownload(bool value)
        {
            _testDisableAutoDownload = value;
        }

        internal bool __Test_GetDisableAutoDownload()
        {
            return _testDisableAutoDownload;
        }

        internal void __Test_SetConfigDirectory(string? path)
        {
            _testConfigDirectory = path;
        }

        internal string? __Test_GetConfigDirectory()
        {
            return _testConfigDirectory;
        }

        public override void OnClose()
        {
            ClickItSettings runtimeSettings = Settings ?? EffectiveSettings;
            PluginLifecycleCoordinator.Shutdown(this, runtimeSettings);

            // In some test scenarios the Settings property isn't populated on the base class even though tests inject settings via the test seam.
            // Avoid invoking base.OnClose when the real Settings property is null to prevent ExileCore.BaseSettingsPlugin from attempting to save a null settings instance.
            if (Settings != null)
            {
                base.OnClose();
            }
        }

        public override bool Initialise()
        {
            var settings = Settings
                ?? throw new InvalidOperationException("Settings is null during plugin initialization.");

            return PluginLifecycleCoordinator.Initialise(this, settings);
        }

        internal void SubscribeLifecycleButtonHandlers(ClickItSettings settings)
        {
            settings.ReportBugButton.OnPressed += ReportBugButtonPressed;
            settings.CopyAdditionalDebugInfoButton.OnPressed += CopyAdditionalDebugInfoButtonPressed;
        }

        internal void UnsubscribeLifecycleButtonHandlers(ClickItSettings runtimeSettings)
        {
            runtimeSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
            runtimeSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            if (!ReferenceEquals(runtimeSettings, EffectiveSettings))
            {
                EffectiveSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
                EffectiveSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            }
        }

        private void ReportBugButtonPressed()
        {
            _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues");
        }

        private void CopyAdditionalDebugInfoButtonPressed()
        {
            _copyAdditionalDebugInfoRequested = true;
            if (GameController != null)
                QueueDeepMemoryDumpCoroutine();
        }

        public override void Render()
        {
            if (State.IsShuttingDown || State.PerformanceMonitor == null) return;

            // Set flag to prevent logging during render loop
            State.IsRendering = true;
            try
            {
                RenderInternal();
            }
            finally
            {
                State.IsRendering = false;
            }
        }

        public void LogMessage(string message, int frame = 5)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogMessage(message, frame);
        }

        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            if (!localDebug || Settings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }
        public void LogError(string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogError(message, frame);
        }

        internal Services.AlertService __Test_GetAlertService()
        {
            return GetOrCreateAlertService();
        }

        internal Services.AlertService GetAlertService()
        {
            return GetOrCreateAlertService();
        }

        internal ClickItSettings GetEffectiveSettingsForLifecycle()
        {
            return EffectiveSettings;
        }

        private bool TrySetViaSettingsProperty(ClickItSettings settings)
        {
            var prop = GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (prop == null)
                return false;

            var setMethod = prop.GetSetMethod(true);
            if (setMethod != null)
            {
                setMethod.Invoke(this, [settings]);
                return true;
            }

            if (!prop.CanWrite)
                return false;

            prop.SetValue(this, settings);
            return true;
        }

        private void TrySetViaLikelyFields(ClickItSettings settings)
        {
            for (Type? current = GetType(); current != null; current = current.BaseType)
            {
                if (TrySetBackingField(settings, current))
                    return;
                if (TrySetCandidateField(settings, current))
                    return;
            }
        }

        private bool TrySetBackingField(ClickItSettings settings, Type current)
        {
            var backingField = current.GetField("<Settings>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingField == null)
                return false;

            backingField.SetValue(this, settings);
            return true;
        }

        private bool TrySetCandidateField(ClickItSettings settings, Type current)
        {
            var fields = current.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (field.FieldType != null && field.FieldType.IsInstanceOfType(settings))
                {
                    field.SetValue(this, settings);
                    return true;
                }

                if (!string.IsNullOrEmpty(field.Name) && field.Name.IndexOf("setting", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    field.SetValue(this, settings);
                    return true;
                }
            }

            return false;
        }

        private Services.AlertService GetOrCreateAlertService()
        {
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

    }
}
