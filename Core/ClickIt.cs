using ExileCore;
using System.IO;
using System.Diagnostics;

namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();

        private ClickItSettings EffectiveSettings => Settings ?? new ClickItSettings();

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
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

        internal Services.AlertService GetAlertService()
        {
            return GetOrCreateAlertService();
        }

        internal ClickItSettings GetEffectiveSettingsForLifecycle()
        {
            return EffectiveSettings;
        }

        private Services.AlertService GetOrCreateAlertService()
        {
            State.AlertService ??= new Services.AlertService(
                () => Settings,
                () => EffectiveSettings,
                SafeGetConfigDirectory,
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
