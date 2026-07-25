namespace ClickIt.Core.Lifecycle
{
    internal sealed class PluginLifecycleButtonBindings(ClickIt owner, DebugClipboardService debugClipboardService)
    {
        private readonly ClickIt _owner = owner;
        private readonly DebugClipboardService _debugClipboardService = debugClipboardService;

        public void Subscribe(ClickItSettings settings)
        {
            settings.ReportBugButton.OnPressed += ReportBugButtonPressed;
            settings.CopyAdditionalDebugInfoButton.OnPressed += CopyAdditionalDebugInfoButtonPressed;
        }

        public void Unsubscribe(ClickItSettings runtimeSettings, ClickItSettings effectiveSettings)
        {
            runtimeSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
            runtimeSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            if (!ReferenceEquals(runtimeSettings, effectiveSettings))
            {
                effectiveSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
                effectiveSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            }
        }

        private void ReportBugButtonPressed()
        {
            _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues");
        }

        private void CopyAdditionalDebugInfoButtonPressed()
        {
            _debugClipboardService.RequestAdditionalDebugInfoCopy();
            if (_owner.GameController != null)
                _debugClipboardService.QueueDeepMemoryDumpCoroutine();
        }
    }
}