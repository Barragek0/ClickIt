namespace ClickIt.Features.Click.Core
{
    internal readonly record struct CandidateAcquisitionEngineDependencies(
        ClickItSettings Settings,
        ILabelInteractionPort LabelInteractionPort,
        IVisibleMechanicSelectionSource VisibleMechanics,
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
        IVisibleMechanicManualInteractionPort VisibleMechanics,
        LabelSelectionCoordinator LabelSelection,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        ChestLootSettlementTracker ChestLootSettlement,
        OffscreenPathingCoordinator OffscreenPathing,
        ClickDebugPublicationService ClickDebugPublisher,
        ClickLabelInteractionService LabelInteraction,
        Func<bool> ShouldCaptureClickDebug,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Action<string> DebugLog);

    internal readonly record struct PostInteractionStateEngineDependencies(InputHandler InputHandler);

    internal readonly record struct ClickRuntimeEngineDependencies(
        ClickTickContextFactory TickContextFactory,
        AltarAutomationService AltarAutomation,
        ClickDebugPublicationService ClickDebugPublisher,
        ClickItSettings Settings,
        ILabelInteractionPort LabelInteractionPort,
        IVisibleMechanicManualInteractionPort VisibleMechanics,
        LabelSelectionCoordinator LabelSelection,
        ClickLabelInteractionService LabelInteraction,
        Func<bool> ShouldCaptureClickDebug,
        PathfindingService PathfindingService,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        ChestLootSettlementTracker ChestLootSettlement,
        OffscreenPathingCoordinator OffscreenPathing,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Action<string> DebugLog,
        InputHandler InputHandler);

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

    internal sealed class ClickRuntimeEngine
    {
        private readonly ClickRuntimeEngineDependencies _dependencies;
        private readonly CandidateAcquisitionEngine _acquisitionPhase;
        private readonly CandidateRankingEngine _rankingPhase;
        private readonly CandidateGatingPhase _gatingPhase;
        private readonly InteractionExecutionEngine _executionPhase;
        private readonly PostInteractionStateEngine _postActionsPhase;

        public ClickRuntimeEngine(ClickRuntimeEngineDependencies dependencies)
        {
            _dependencies = dependencies;
            _acquisitionPhase = new CandidateAcquisitionEngine(CreateCandidateAcquisitionDependencies(dependencies));
            _rankingPhase = new CandidateRankingEngine(CreateCandidateRankingDependencies(dependencies));
            _gatingPhase = new CandidateGatingPhase();
            _executionPhase = new InteractionExecutionEngine(CreateInteractionExecutionDependencies(dependencies));
            _postActionsPhase = new PostInteractionStateEngine(CreatePostInteractionStateDependencies(dependencies));
        }

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
                dependencies.ShouldCaptureClickDebug,
                dependencies.HoldDebugTelemetryAfterSuccess,
                dependencies.DebugLog);

        private static PostInteractionStateEngineDependencies CreatePostInteractionStateDependencies(ClickRuntimeEngineDependencies dependencies)
            => new(dependencies.InputHandler);

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
            DecisionResult decision = _gatingPhase.Gate(candidates, ranking);
            ExecutionResult executionResult = _executionPhase.Execute(context, candidates, decision);

            IEnumerator postActions = _postActionsPhase.Run(executionResult);
            while (postActions.MoveNext())
            {
                yield return postActions.Current;
            }
        }

        private sealed class CandidateGatingPhase
        {
            public DecisionResult Gate(ClickCandidates candidates, RankingResult ranking)
            {
                return new DecisionResult(
                    TrySettlers: ranking.PreferSettlers && candidates.SettlersOre.HasValue,
                    TryLostShipment: ranking.PreferLostShipment && candidates.LostShipment.HasValue,
                    TryShrine: ranking.PreferShrine,
                    GroundItemsVisible: ranking.GroundItemsVisible);
            }
        }
    }
}