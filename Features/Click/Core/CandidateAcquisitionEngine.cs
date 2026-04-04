namespace ClickIt.Features.Click.Core
{
    internal sealed class CandidateAcquisitionEngine(CandidateAcquisitionEngineDependencies dependencies)
    {
        private readonly CandidateAcquisitionEngineDependencies _dependencies = dependencies;

        public ClickCandidates Collect(ClickTickContext context)
        {
            LostShipmentCandidate? lostShipment;
            SettlersOreCandidate? settlersOre;

            if (!context.GroundItemsVisible)
            {
                _dependencies.VisibleMechanics.ResolveHiddenFallbackCandidates(out lostShipment, out settlersOre);
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHidden", "Ground item labels hidden; evaluating non-label fallbacks", null);
                return new ClickCandidates(lostShipment, settlersOre, null, null);
            }

            _dependencies.VisibleMechanics.ResolveVisibleMechanicCandidates(out lostShipment, out settlersOre, context.AllLabels);
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

            return new ClickCandidates(lostShipment, settlersOre, nextLabel, nextLabelMechanicId);
        }
    }
}