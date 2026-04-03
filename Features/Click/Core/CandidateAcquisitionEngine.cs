using ExileCore.Shared.Enums;
using ExileCore.PoEMemory.Elements;
using ClickIt.Features.Click.Runtime;

namespace ClickIt.Features.Click.Core
{
    internal sealed class CandidateAcquisitionEngine(ClickRuntimeEngine owner)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = owner.Dependencies;

        public ClickCandidates Collect(ClickTickContext context)
        {
            LostShipmentCandidate? lostShipment;
            SettlersOreCandidate? settlersOre;

            if (!context.GroundItemsVisible)
            {
                _dependencies.VisibleMechanics.ResolveHiddenFallbackCandidates(out lostShipment, out settlersOre);
                _dependencies.PublishClickFlowDebugStage("GroundItemsHidden", "Ground item labels hidden; evaluating non-label fallbacks", null);
                return new ClickCandidates(lostShipment, settlersOre, null, null);
            }

            _dependencies.VisibleMechanics.ResolveVisibleMechanicCandidates(out lostShipment, out settlersOre, context.AllLabels);
            if (_dependencies.ShouldCaptureClickDebug())
            {
                _dependencies.PublishClickFlowDebugStage("LabelSource", _dependencies.BuildLabelSourceDebugSummary(context.AllLabels), null);
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