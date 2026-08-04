namespace ClickIt.Features.Click.Core
{
    internal sealed class CandidateAcquisitionEngine(CandidateAcquisitionEngineDependencies dependencies)
    {
        private readonly CandidateAcquisitionEngineDependencies _dependencies = dependencies;

        public ClickCandidates Collect(ClickTickContext context)
        {
            int allLabelsCount = context.AllLabels?.Count ?? 0;
            bool captureClickDebug = _dependencies.ClickDebugPublisher.ShouldCaptureClickDebug();
            string labelSourceSummary = _dependencies.ShouldCaptureClickDebug()
                ? _dependencies.LabelInteraction.BuildLabelSourceDebugSummary(context.AllLabels)
                : string.Empty;

            if (!context.GroundItemsVisible)
            {
                VisibleMechanicSelectionSnapshot hiddenFallbackSelection = _dependencies.VisibleMechanics.GetHiddenFallbackSelectionSnapshot();
                if (captureClickDebug)
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHidden",
                        $"{labelSourceSummary} | hiddenFallback settlers={hiddenFallbackSelection.HasSettlers} lostShipment={hiddenFallbackSelection.HasLostShipment}", null);

                LabelOnGround? hiddenLabel = _dependencies.LabelSelection.ResolveNextLabelCandidate(context.AllLabels);
                string? hiddenLabelMechanicId = hiddenLabel != null
                    ? _dependencies.LabelInteractionPort.GetMechanicIdForLabel(hiddenLabel)
                    : null;

                if (hiddenLabel != null)
                {
                    if (captureClickDebug)
                        _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelFound",
                            $"{labelSourceSummary} | mechanic={hiddenLabelMechanicId} entity={hiddenLabel.ItemOnGround?.Path}", hiddenLabelMechanicId);
                }
                else if (captureClickDebug)
                {
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelNull",
                        $"{labelSourceSummary} | ResolveNextLabelCandidate returned null despite {allLabelsCount} labels in context", null);
                }

                return new ClickCandidates(hiddenFallbackSelection.LostShipment, hiddenFallbackSelection.Settlers, hiddenLabel, hiddenLabelMechanicId);
            }

            VisibleMechanicSelectionSnapshot visibleMechanicSelection = _dependencies.VisibleMechanics.GetVisibleMechanicSelectionSnapshotForLabels(context.AllLabels);
            if (captureClickDebug)
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("LabelSource", labelSourceSummary, null);


            LabelOnGround? nextLabel = _dependencies.LabelSelection.ResolveNextLabelCandidate(context.AllLabels);
            string? nextLabelMechanicId = nextLabel != null
                ? _dependencies.LabelInteractionPort.GetMechanicIdForLabel(nextLabel)
                : null;

            if (nextLabel != null)
            {
                if (captureClickDebug)
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleLabelFound",
                        $"{labelSourceSummary} | mechanic={nextLabelMechanicId} entity={nextLabel.ItemOnGround?.Path}", nextLabelMechanicId);
            }
            else if (captureClickDebug)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleLabelNull",
                    $"{labelSourceSummary} | ResolveNextLabelCandidate returned null", null);
            }

            nextLabelMechanicId = OffscreenPathingMath.ResolveLabelMechanicIdForVisibleCandidateComparison(
                nextLabelMechanicId,
                hasLabel: nextLabel != null,
                isWorldItemLabel: nextLabel?.ItemOnGround?.Type == EntityType.WorldItem,
                clickItemsEnabled: _dependencies.Settings.ClickItems.Value);

            return new ClickCandidates(visibleMechanicSelection.LostShipment, visibleMechanicSelection.Settlers, nextLabel, nextLabelMechanicId);
        }
    }
}