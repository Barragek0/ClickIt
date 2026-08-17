namespace ClickIt.Features.Click.Core
{
    // Shared dependency clusters (S12): the common groups that recur across the Click
    // dependency records, so the large records converge on one shape instead of 26 ad-hoc
    // ones. Each cluster groups the dependencies by concern (telemetry/debug, policy/runtime,
    // selection/read-model, pathing, mechanics/callbacks).
    internal readonly record struct ClickTelemetryDependencies(
        ClickDebugPublicationService ClickDebugPublisher,
        Func<bool> ShouldCaptureClickDebug,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Action<string> DebugLog,
        Action<ClickAllocationBreakdown>? RecordAllocationBreakdown,
        Action<int, long, double>? RecordBreakdownStage);

    internal readonly record struct ClickPolicyDependencies(
        ClickItSettings Settings,
        InputHandler InputHandler,
        Func<Vector2, string, bool> PointIsInClickableArea,
        ClickSuccessAnchor? ClickSuccessAnchor);

    internal readonly record struct ClickSelectionDependencies(
        ClickTickContextFactory TickContextFactory,
        ILabelInteractionPort LabelInteractionPort,
        IVisibleMechanicRuntimePort VisibleMechanics,
        LabelSelectionScanEngine LabelSelectionScan,
        SpecialLabelInteractionHandler SpecialLabelInteraction,
        ClickLabelInteractionService LabelInteraction,
        ChestLootSettlementTracker ChestLootSettlement);

    internal readonly record struct ClickPathingDependencies(
        PathfindingService PathfindingService,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        OffscreenPathingCoordinator OffscreenPathing);

    internal readonly record struct ClickMechanicDependencies(
        AltarAutomationService AltarAutomation,
        Func<LabelOnGround?>? GetHarvestLabelToClick,
        Func<BlightBuildAction>? TryProgressBlightBuilding,
        Func<Entity?>? GetBlightPathfindTarget,
        Func<bool>? IsBlightEncounterActive);
}
