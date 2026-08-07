namespace ClickIt.Features.Click.Core
{
    internal readonly record struct CandidateAcquisitionEngineDependencies(
        ClickItSettings Settings,
        ILabelInteractionPort LabelInteractionPort,
        IVisibleMechanicQueryPort VisibleMechanics,
        LabelSelectionScanEngine LabelSelectionScan,
        ClickDebugPublicationService ClickDebugPublisher,
        ClickLabelInteractionService LabelInteraction,
        Func<bool> ShouldCaptureClickDebug);

    internal readonly record struct CandidateRankingEngineDependencies(
        LabelSelectionScanEngine LabelSelectionScan,
        ClickLabelInteractionService LabelInteraction);

    internal readonly record struct InteractionExecutionEngineDependencies(
        ClickItSettings Settings,
        ILabelInteractionPort LabelInteractionPort,
        PathfindingService PathfindingService,
        IVisibleMechanicInteractionPort VisibleMechanics,
        SpecialLabelInteractionHandler SpecialLabelInteraction,
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
        LabelSelectionScanEngine LabelSelectionScan,
        SpecialLabelInteractionHandler SpecialLabelInteraction,
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
        Action<ClickAllocationBreakdown>? RecordAllocationBreakdown = null,
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

    internal readonly record struct ExecutionResult(bool ShouldRunPostActions, bool DidActionableWork);

    internal sealed class ClickRuntimeEngine(ClickRuntimeEngineDependencies dependencies)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = dependencies;
        private readonly CandidateAcquisitionEngine _acquisitionPhase = new(CreateCandidateAcquisitionDependencies(dependencies));
        private readonly CandidateRankingEngine _rankingPhase = new(CreateCandidateRankingDependencies(dependencies));
        private readonly InteractionExecutionEngine _executionPhase = new(CreateInteractionExecutionDependencies(dependencies));

        internal bool LastTickWasActionable { get; private set; }

        private static CandidateAcquisitionEngineDependencies CreateCandidateAcquisitionDependencies(ClickRuntimeEngineDependencies dependencies)
            => new(
                dependencies.Settings,
                dependencies.LabelInteractionPort,
                dependencies.VisibleMechanics,
                dependencies.LabelSelectionScan,
                dependencies.ClickDebugPublisher,
                dependencies.LabelInteraction,
                dependencies.ShouldCaptureClickDebug);

        private static CandidateRankingEngineDependencies CreateCandidateRankingDependencies(ClickRuntimeEngineDependencies dependencies)
            => new(
                dependencies.LabelSelectionScan,
                dependencies.LabelInteraction);

        private static InteractionExecutionEngineDependencies CreateInteractionExecutionDependencies(ClickRuntimeEngineDependencies dependencies)
            => new(
                dependencies.Settings,
                dependencies.LabelInteractionPort,
                dependencies.PathfindingService,
                dependencies.VisibleMechanics,
                dependencies.SpecialLabelInteraction,
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
            LastTickWasActionable = false;
            long runStart = GC.GetAllocatedBytesForCurrentThread();
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("TickStart", "ProcessRegularClick entered", null);

            if (_dependencies.AltarAutomation.HasClickableAltars())
            {
                LastTickWasActionable = true;
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("AltarBranch", "Clickable altar detected; regular label click path skipped", null);
                long altarStart = GC.GetAllocatedBytesForCurrentThread();
                IEnumerator altar = _dependencies.AltarAutomation.ProcessAltarClicking();
                while (altar.MoveNext())
                    yield return altar.Current;
                long altarBytes = GC.GetAllocatedBytesForCurrentThread() - altarStart;
                long total = GC.GetAllocatedBytesForCurrentThread() - runStart;
                _dependencies.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
                    ContextBytes: 0, AcquireBytes: 0, RankBytes: 0, ExecuteBytes: altarBytes, PostBytes: 0,
                    OtherBytes: SystemMath.Max(0, total - altarBytes), TotalBytes: total));
                yield break;
            }

            IEnumerator core = RunCore(runStart);
            while (core.MoveNext())
                yield return core.Current;
        }

        private IEnumerator RunCore(long runStart)
        {
            long ctxStart = GC.GetAllocatedBytesForCurrentThread();
            if (!_dependencies.TickContextFactory.TryCreateRegularClickContext(out ClickTickContext context))
            {
                long earlyCtxBytes = GC.GetAllocatedBytesForCurrentThread() - ctxStart;
                long earlyTotal = GC.GetAllocatedBytesForCurrentThread() - runStart;
                _dependencies.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
                    earlyCtxBytes, 0, 0, 0, 0, SystemMath.Max(0, earlyTotal - earlyCtxBytes), earlyTotal));
                yield break;
            }
            long ctxBytes = GC.GetAllocatedBytesForCurrentThread() - ctxStart;

            long acquireStart = GC.GetAllocatedBytesForCurrentThread();
            ClickCandidates candidates = _acquisitionPhase.Collect(context);
            long acquireBytes = GC.GetAllocatedBytesForCurrentThread() - acquireStart;

            long rankStart = GC.GetAllocatedBytesForCurrentThread();
            RankingResult ranking = _rankingPhase.Rank(context, candidates);
            long rankBytes = GC.GetAllocatedBytesForCurrentThread() - rankStart;

            DecisionResult decision = Gate(candidates, ranking);

            long executeStart = GC.GetAllocatedBytesForCurrentThread();
            ExecutionResult executionResult = _executionPhase.Execute(context, candidates, decision);
            long executeBytes = GC.GetAllocatedBytesForCurrentThread() - executeStart;
            LastTickWasActionable = executionResult.DidActionableWork;

            long postBytes = 0;
            if (executionResult.ShouldRunPostActions)
            {
                long postStart = GC.GetAllocatedBytesForCurrentThread();
                IEnumerator postActions = RunPostClickActions(_dependencies.InputHandler, executionResult);
                while (postActions.MoveNext())
                    yield return postActions.Current;
                postBytes = GC.GetAllocatedBytesForCurrentThread() - postStart;
            }

            long total = GC.GetAllocatedBytesForCurrentThread() - runStart;
            long stageSum = ctxBytes + acquireBytes + rankBytes + executeBytes + postBytes;
            _dependencies.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
                ctxBytes, acquireBytes, rankBytes, executeBytes, postBytes,
                SystemMath.Max(0, total - stageSum), total));
        }

        private static DecisionResult Gate(ClickCandidates candidates, RankingResult ranking)
            => new(
                TrySettlers: ranking.PreferSettlers && candidates.SettlersOre.HasValue,
                TryLostShipment: ranking.PreferLostShipment && candidates.LostShipment.HasValue,
                TryShrine: ranking.PreferShrine,
                GroundItemsVisible: ranking.GroundItemsVisible);
    }
}