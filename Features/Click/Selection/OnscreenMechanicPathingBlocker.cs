namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct OnscreenMechanicPathingBlockerDependencies(
        ClickItSettings Settings,
        AltarAutomationService AltarAutomation,
        IVisibleMechanicQueryPort VisibleMechanics,
        ClickDebugPublicationService ClickDebugPublisher);

    internal sealed class OnscreenMechanicPathingBlocker(OnscreenMechanicPathingBlockerDependencies dependencies)
    {
        private readonly OnscreenMechanicPathingBlockerDependencies _dependencies = dependencies;

        internal bool ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable()
        {
            bool prioritizeOnscreen = _dependencies.Settings.PrioritizeOnscreenClickableMechanicsOverPathfinding?.Value == true;
            bool shouldEvaluateOnscreenMechanicChecks = OffscreenPathingMath.ShouldEvaluateOnscreenMechanicChecks(
                prioritizeOnscreen,
                _dependencies.Settings.ClickShrines.Value,
                _dependencies.Settings.ClickLostShipmentCrates.Value,
                _dependencies.Settings.ClickSettlersOre.Value,
                _dependencies.Settings.ClickEaterAltars.Value,
                _dependencies.Settings.ClickExarchAltars.Value);
            if (!shouldEvaluateOnscreenMechanicChecks)
                return false;

            bool hasClickableAltars = _dependencies.AltarAutomation.HasClickableAltars();
            bool hasClickableShrine = _dependencies.VisibleMechanics.HasClickableShrine();
            VisibleMechanicAvailabilitySnapshot visibleMechanicAvailability = _dependencies.VisibleMechanics.GetVisibleMechanicAvailabilitySnapshot();

            bool shouldAvoid = OffscreenPathingMath.ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
                prioritizeOnscreen,
                hasClickableAltars,
                hasClickableShrine,
                visibleMechanicAvailability.HasLostShipment,
                visibleMechanicAvailability.HasSettlers);

            if (shouldAvoid)
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
        "OffscreenPathingBlocked",
        $"onscreen clickable mechanic detected (altar={hasClickableAltars}, shrine={hasClickableShrine}, lost={visibleMechanicAvailability.HasLostShipment}, settlers={visibleMechanicAvailability.HasSettlers})");


            return shouldAvoid;
        }
    }
}