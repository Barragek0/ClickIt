using ClickIt.Services.Observability;
using ClickIt.Services.Label.Diagnostics;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const int LabelDebugTrailCapacity = 24;
        private readonly LabelSelectionDiagnostics _labelSelectionDiagnostics = new(LabelDebugTrailCapacity);

        public LabelDebugSnapshot GetLatestLabelDebug()
        {
            return _labelSelectionDiagnostics.GetLatest();
        }

        public IReadOnlyList<string> GetLatestLabelDebugTrail()
        {
            return _labelSelectionDiagnostics.GetTrail();
        }

        private void SetLatestLabelDebug(LabelDebugSnapshot snapshot)
        {
            if (!ShouldCaptureLabelDebug())
                return;

            _labelSelectionDiagnostics.PublishSnapshot(snapshot);
        }

        private bool ShouldCaptureLabelDebug()
        {
            return _settings.DebugMode.Value && _settings.DebugShowLabels.Value;
        }

        private void PublishLabelDebugStage(in LabelDebugEvent debugEvent)
        {
            if (!ShouldCaptureLabelDebug())
                return;

            _labelSelectionDiagnostics.PublishEvent(debugEvent);
        }
    }
}
