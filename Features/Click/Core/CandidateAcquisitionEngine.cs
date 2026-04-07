namespace ClickIt.Features.Click.Core
{
    internal sealed class CandidateAcquisitionEngine(CandidateAcquisitionEngineDependencies dependencies)
    {
        private readonly CandidateAcquisitionEngineDependencies _dependencies = dependencies;

        public ClickCandidates Collect(ClickTickContext context)
        {
            if (!context.GroundItemsVisible)
            {
                VisibleMechanicSelectionSnapshot hiddenFallbackSelection = _dependencies.VisibleMechanics.GetHiddenFallbackSelectionSnapshot();
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHidden", "Ground item labels hidden; evaluating non-label fallbacks", null);
                return new ClickCandidates(hiddenFallbackSelection.LostShipment, hiddenFallbackSelection.Settlers, null, null);
            }

            VisibleMechanicSelectionSnapshot visibleMechanicSelection = _dependencies.VisibleMechanics.GetVisibleMechanicSelectionSnapshotForLabels(context.AllLabels);
            if (_dependencies.ShouldCaptureClickDebug())
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("LabelSource", _dependencies.LabelInteraction.BuildLabelSourceDebugSummary(context.AllLabels), null);
            }

            LabelOnGround? nextLabel = _dependencies.LabelSelection.ResolveNextLabelCandidate(context.AllLabels);
            string? nextLabelMechanicId = nextLabel != null
                ? _dependencies.LabelInteractionPort.GetMechanicIdForLabel(nextLabel)
                : null;

            nextLabelMechanicId = OffscreenPathingMath.ResolveLabelMechanicIdForVisibleCandidateComparison(
                nextLabelMechanicId,
                hasLabel: nextLabel != null,
                isWorldItemLabel: nextLabel?.ItemOnGround?.Type == EntityType.WorldItem,
                clickItemsEnabled: _dependencies.Settings.ClickItems.Value);

            return new ClickCandidates(visibleMechanicSelection.LostShipment, visibleMechanicSelection.Settlers, nextLabel, nextLabelMechanicId);
        }
    }
}