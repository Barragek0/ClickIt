namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();

        private DebugClipboardService? _debugClipboardService;
        private PluginLifecycleButtonBindings? _lifecycleButtonBindings;

        private ClickItSettings EffectiveSettings => Settings ?? new ClickItSettings();

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
            ClickItSettings runtimeSettings = EffectiveSettings;
            PluginLifecycleCoordinator.Shutdown(this, runtimeSettings);

            if (Settings != null)
                base.OnClose();

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

        public override void DrawSettings()
        {
            var settings = EffectiveSettings;

            if (ImGui.TreeNodeEx("Debug/Testing##ClickItDebugTesting", ImGuiTreeNodeFlags.DefaultOpen))
            {
                settings.DebugTestingPanel.DrawDelegate?.Invoke();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("Controls##ClickItControls", ImGuiTreeNodeFlags.DefaultOpen))
            {
                settings.ControlsPanel.DrawDelegate?.Invoke();
                ImGui.TreePop();
            }

            foreach (var drawer in Drawers)
                drawer.Draw();

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
                base.LogMessage(message, frame);

        }
        public void LogError(string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.Rendering.IsRendering) return;
            base.LogError(message, frame);
        }

        internal AlertService GetAlertService()
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

    }
}
