using ExileCore;
using ClickIt.Core.Runtime;

namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();

        private ClickItSettings? _settingsOverrideForTests;
        private DebugClipboardService? _debugClipboardService;
        private PluginLifecycleButtonBindings? _lifecycleButtonBindings;

        private ClickItSettings EffectiveSettings => Settings ?? _settingsOverrideForTests ?? new ClickItSettings();

        private DebugClipboardService DebugClipboardService
            => _debugClipboardService ??= new DebugClipboardService(new DebugClipboardServiceDependencies(
                State,
                this,
                GetEffectiveSettingsForLifecycle,
                () => GameController));

        internal PluginLifecycleButtonBindings LifecycleButtonBindings
            => _lifecycleButtonBindings ??= new PluginLifecycleButtonBindings(this, DebugClipboardService);

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

        public override void Render()
        {
            if (State.Runtime.IsShuttingDown || State.Services.PerformanceMonitor == null) return;

            // Set flag to prevent logging during render loop
            State.Rendering.IsRendering = true;
            try
            {
                RenderInternal();
            }
            finally
            {
                State.Rendering.IsRendering = false;
            }
        }

        public void LogMessage(string message, int frame = 5)
        {
            // Skip logging during render loop to prevent crashes
            if (State.Rendering.IsRendering) return;
            base.LogMessage(message, frame);
        }

        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.Rendering.IsRendering) return;
            if (!localDebug || EffectiveSettings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }
        public void LogError(string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.Rendering.IsRendering) return;
            base.LogError(message, frame);
        }

        internal Services.AlertService GetAlertService()
        {
            return PluginAlertServiceHost.GetOrCreateAlertService(this);
        }

        internal DebugClipboardService GetDebugClipboardService()
        {
            return DebugClipboardService;
        }

        internal ClickItSettings GetEffectiveSettingsForLifecycle()
        {
            return EffectiveSettings;
        }

        internal void SetSettingsForTests(ClickItSettings settings)
        {
            _settingsOverrideForTests = settings ?? throw new ArgumentNullException(nameof(settings));
        }

    }
}
