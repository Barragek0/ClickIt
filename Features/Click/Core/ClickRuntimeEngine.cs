namespace ClickIt.Features.Click.Core
{
    internal readonly record struct CandidateAcquisitionEngineDependencies(
        ClickItSettings Settings,
        ILabelInteractionPort LabelInteractionPort,
        IVisibleMechanicQueryPort VisibleMechanics,
        LabelSelectionCoordinator LabelSelection,
        ClickDebugPublicationService ClickDebugPublisher,
        ClickLabelInteractionService LabelInteraction,
        Func<bool> ShouldCaptureClickDebug);

    internal readonly record struct CandidateRankingEngineDependencies(
        LabelSelectionCoordinator LabelSelection,
        ClickLabelInteractionService LabelInteraction);

    internal readonly record struct InteractionExecutionEngineDependencies(
        ClickItSettings Settings,
        ILabelInteractionPort LabelInteractionPort,
        PathfindingService PathfindingService,
        IVisibleMechanicInteractionPort VisibleMechanics,
        LabelSelectionCoordinator LabelSelection,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        ChestLootSettlementTracker ChestLootSettlement,
        OffscreenPathingCoordinator OffscreenPathing,
        ClickDebugPublicationService ClickDebugPublisher,
        ClickLabelInteractionService LabelInteraction,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Func<bool> ShouldCaptureClickDebug,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Action<string> DebugLog,
        Func<LabelOnGround?>? GetHarvestLabelToClick = null,
        Func<BlightBuildAction>? TryProgressBlightBuilding = null,
        Func<Entity?>? GetBlightPathfindTarget = null,
        Func<bool>? IsBlightEncounterActive = null);

    internal readonly record struct ClickRuntimeEngineDependencies(
        ClickTickContextFactory TickContextFactory,
        AltarAutomationService AltarAutomation,
        ClickDebugPublicationService ClickDebugPublisher,
        ClickItSettings Settings,
        ILabelInteractionPort LabelInteractionPort,
        IVisibleMechanicRuntimePort VisibleMechanics,
        LabelSelectionCoordinator LabelSelection,
        ClickLabelInteractionService LabelInteraction,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Func<bool> ShouldCaptureClickDebug,
        PathfindingService PathfindingService,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        ChestLootSettlementTracker ChestLootSettlement,
        OffscreenPathingCoordinator OffscreenPathing,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Action<string> DebugLog,
        InputHandler InputHandler,
        Func<LabelOnGround?>? GetHarvestLabelToClick = null,
        Func<BlightBuildAction>? TryProgressBlightBuilding = null,
        Func<Entity?>? GetBlightPathfindTarget = null,
        Func<bool>? IsBlightEncounterActive = null);

    internal readonly record struct ClickCandidates(
        LostShipmentCandidate? LostShipment,
        SettlersOreCandidate? SettlersOre,
        LabelOnGround? NextLabel,
        string? NextLabelMechanicId);

    internal readonly record struct RankingResult(
        bool PreferSettlers,
        bool PreferLostShipment,
        bool PreferShrine,
        bool GroundItemsVisible);

    internal readonly record struct DecisionResult(
        bool TrySettlers,
        bool TryLostShipment,
        bool TryShrine,
        bool GroundItemsVisible);

    internal readonly record struct ExecutionResult(bool ShouldRunPostActions);

    internal sealed class ClickRuntimeEngine(ClickRuntimeEngineDependencies dependencies)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = dependencies;
        private readonly CandidateAcquisitionEngine _acquisitionPhase = new(CreateCandidateAcquisitionDependencies(dependencies));
        private readonly CandidateRankingEngine _rankingPhase = new(CreateCandidateRankingDependencies(dependencies));
        private readonly InteractionExecutionEngine _executionPhase = new(CreateInteractionExecutionDependencies(dependencies));

        private static CandidateAcquisitionEngineDependencies CreateCandidateAcquisitionDependencies(ClickRuntimeEngineDependencies dependencies)
            => new(
                dependencies.Settings,
                dependencies.LabelInteractionPort,
                dependencies.VisibleMechanics,
                dependencies.LabelSelection,
                dependencies.ClickDebugPublisher,
                dependencies.LabelInteraction,
                dependencies.ShouldCaptureClickDebug);

        private static CandidateRankingEngineDependencies CreateCandidateRankingDependencies(ClickRuntimeEngineDependencies dependencies)
            => new(
                dependencies.LabelSelection,
                dependencies.LabelInteraction);

        private static InteractionExecutionEngineDependencies CreateInteractionExecutionDependencies(ClickRuntimeEngineDependencies dependencies)
            => new(
                dependencies.Settings,
                dependencies.LabelInteractionPort,
                dependencies.PathfindingService,
                dependencies.VisibleMechanics,
                dependencies.LabelSelection,
                dependencies.PathfindingLabelSuppression,
                dependencies.ChestLootSettlement,
                dependencies.OffscreenPathing,
                dependencies.ClickDebugPublisher,
                dependencies.LabelInteraction,
                dependencies.PointIsInClickableArea,
                dependencies.ShouldCaptureClickDebug,
                dependencies.HoldDebugTelemetryAfterSuccess,
                dependencies.DebugLog,
                dependencies.GetHarvestLabelToClick,
                dependencies.TryProgressBlightBuilding,
                dependencies.GetBlightPathfindTarget,
                dependencies.IsBlightEncounterActive);

        private static IEnumerator RunPostClickActions(InputHandler inputHandler, ExecutionResult executionResult)
        {
            if (!executionResult.ShouldRunPostActions)
                yield break;

            if (inputHandler.TriggerToggleItems())
            {
                int blockMs = inputHandler.GetToggleItemsPostClickBlockMs();
                if (blockMs > 0)
                    yield return new WaitTime(blockMs);
            }
        }

        public IEnumerator Run()
        {
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("TickStart", "ProcessRegularClick entered", null);

            if (_dependencies.AltarAutomation.HasClickableAltars())
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("AltarBranch", "Clickable altar detected; regular label click path skipped", null);
                return _dependencies.AltarAutomation.ProcessAltarClicking();
            }

            return RunCore();
        }

        private IEnumerator RunCore()
        {
            if (!_dependencies.TickContextFactory.TryCreateRegularClickContext(out ClickTickContext context))
                yield break;

            ClickCandidates candidates = _acquisitionPhase.Collect(context);
            RankingResult ranking = _rankingPhase.Rank(context, candidates);
            DecisionResult decision = Gate(candidates, ranking);
            ExecutionResult executionResult = _executionPhase.Execute(context, candidates, decision);

            // Only allocate the post-click iterator when it can actually do something.
            if (executionResult.ShouldRunPostActions)
            {
                IEnumerator postActions = RunPostClickActions(_dependencies.InputHandler, executionResult);
                while (postActions.MoveNext())
                    yield return postActions.Current;
            }
        }

        private static DecisionResult Gate(ClickCandidates candidates, RankingResult ranking)
            => new(
                TrySettlers: ranking.PreferSettlers && candidates.SettlersOre.HasValue,
                TryLostShipment: ranking.PreferLostShipment && candidates.LostShipment.HasValue,
                TryShrine: ranking.PreferShrine,
                GroundItemsVisible: ranking.GroundItemsVisible);
    }
}