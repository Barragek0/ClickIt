namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();

        private ClickItSettings EffectiveSettings => Settings ?? new ClickItSettings();

        private DebugClipboardService DebugClipboardService
            => field ??= CreateDebugClipboardService();

        private DebugClipboardService CreateDebugClipboardService()
        {
            DebugClipboardService service = new(new DebugClipboardServiceDependencies(
                State,
                this,
                () => GameController));
            GameStateDumpCoordinator.SetSource(() => service.GameStateDump);
            return service;
        }

        internal PluginLifecycleButtonBindings LifecycleButtonBindings
            => field ??= new PluginLifecycleButtonBindings(DebugClipboardService);

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
            ExileCorePerformanceApplier.SetSuppressSetupUntilReload(false);
            ExileCorePerformanceApplier.SetGameControllerProvider(() => GameController);
        }

        public override void OnClose()
        {
            PerformanceSettingsPanelRenderer.SetCurrent(static () => null);
            ExileCorePerformanceApplier.SetGameControllerProvider(static () => null);
            GameStateDumpCoordinator.SetSource(static () => null);
            EntityEventHub.Instance.Dispose();

            ClickItSettings runtimeSettings = EffectiveSettings;
            PluginLifecycleCoordinator.Shutdown(this, runtimeSettings);

            if (Settings != null)
                base.OnClose();

        }

        public override bool Initialise()
        {
            ClickItSettings settings = Settings
                ?? throw new InvalidOperationException("Settings is null during plugin initialization.");

            return PluginLifecycleCoordinator.Initialise(this, settings);
        }

        public override void Render()
        {
            if (State.Runtime.IsShuttingDown || State.Services.PerformanceMonitor == null) return;

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
            ClickItSettings settings = EffectiveSettings;

            if (ImGui.TreeNodeEx("Debug/Testing##ClickItDebugTesting", ImGuiTreeNodeFlags.DefaultOpen))
            {
                settings.DebugTestingPanel.DrawDelegate?.Invoke();
                ImGui.TreePop();
            }

            // First-run setup is a popup; after confirming, this section holds the performance guide.
            if (ImGui.TreeNodeEx("Performance##ClickItPerformance"))
            {
                settings.PerformancePanel.DrawDelegate?.Invoke();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("Controls##ClickItControls", ImGuiTreeNodeFlags.DefaultOpen))
            {
                settings.ControlsPanel.DrawDelegate?.Invoke();
                ImGui.TreePop();
            }

            foreach (ISettingsHolder? drawer in Drawers)
                drawer.Draw();

        }

        public void LogMessage(string message, int frame = 5)
        {
            if (State.Rendering.IsRendering) return;
            base.LogMessage(message, frame);
        }

        public void LogError(string message, int frame = 0)
        {
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
