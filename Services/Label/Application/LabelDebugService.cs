using ExileCore.PoEMemory.Elements;

namespace ClickIt.Services.Label.Application
{
    internal sealed class LabelDebugService(
        Func<IReadOnlyList<LabelOnGround>?, int, int, LabelFilterService.SelectionDebugSummary> getSelectionDebugSummaryCore,
        Action<IReadOnlyList<LabelOnGround>?, int, int> logSelectionDiagnosticsCore)
    {
        private readonly Func<IReadOnlyList<LabelOnGround>?, int, int, LabelFilterService.SelectionDebugSummary> _getSelectionDebugSummaryCore = getSelectionDebugSummaryCore;
        private readonly Action<IReadOnlyList<LabelOnGround>?, int, int> _logSelectionDiagnosticsCore = logSelectionDiagnosticsCore;

        public LabelFilterService.SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => _getSelectionDebugSummaryCore(allLabels, startIndex, maxCount);

        public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => _logSelectionDiagnosticsCore(allLabels, startIndex, maxCount);
    }
}