namespace ClickIt.Features.Click.Core
{
    internal readonly record struct CandidateAcquisitionEngineDependencies(
        ClickItSettings Settings,
        ILabelInteractionPort LabelInteractionPort,
        IVisibleMechanicQueryPort VisibleMechanics,
        LabelSelectionScanEngine LabelSelectionScan,
        ClickDebugPublicationService ClickDebugPublisher,
        ClickLabelInteractionService LabelInteraction,
        Func<bool> ShouldCaptureClickDebug,
        Action<int, long, double>? RecordBreakdownStage = null);

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
        Action<int, long, double>? RecordBreakdownStage = null,
        ClickSuccessAnchor? ClickSuccessAnchor = null,
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
        Action<int, long, double>? RecordBreakdownStage = null,
        ClickSuccessAnchor? ClickSuccessAnchor = null,
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

        private void RecordStage(int stageIndex, long bytes, double ms)
            => _dependencies.RecordBreakdownStage?.Invoke(stageIndex, bytes, ms);

        private static CandidateAcquisitionEngineDependencies CreateCandidateAcquisitionDependencies(ClickRuntimeEngineDependencies dependencies)
            => new(
                dependencies.Settings,
                dependencies.LabelInteractionPort,
                dependencies.VisibleMechanics,
                dependencies.LabelSelectionScan,
                dependencies.ClickDebugPublisher,
                dependencies.LabelInteraction,
                dependencies.ShouldCaptureClickDebug,
                dependencies.RecordBreakdownStage);

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
                dependencies.RecordBreakdownStage,
                dependencies.ClickSuccessAnchor,
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
            long runTimestamp = Stopwatch.GetTimestamp();
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("TickStart", "ProcessRegularClick entered", null);

            if (_dependencies.AltarAutomation.HasClickableAltars())
            {
                LastTickWasActionable = true;
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("AltarBranch", "Clickable altar detected; regular label click path skipped", null);
                long altarStart = GC.GetAllocatedBytesForCurrentThread();
                long altarTimestamp = Stopwatch.GetTimestamp();
                double altarSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
                IEnumerator altar = _dependencies.AltarAutomation.ProcessAltarClicking();
                while (altar.MoveNext())
                    yield return altar.Current;
                long altarBytes = GC.GetAllocatedBytesForCurrentThread() - altarStart;
                double altarMs = GetElapsedMs(altarTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - altarSleepBefore);
                long total = GC.GetAllocatedBytesForCurrentThread() - runStart;
                double totalMs = GetElapsedMs(runTimestamp) - ClickPipelineTiming.ReadSleepTimeMs();
                // The altar branch replaces the whole click path; its processing time is its own stage and the
                // remainder (run prelude / iterator overhead) lands in Other so the click table accounts for
                // the entire run even when an altar consumes it. Both exclude the intentional safety sleeps.
                _dependencies.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
                    ContextBytes: 0, AcquireBytes: 0, RankBytes: 0, ExecuteBytes: 0, PostBytes: 0,
                    OtherBytes: SystemMath.Max(0, total - altarBytes), TotalBytes: total,
                    AltarBytes: altarBytes,
                    AltarMs: altarMs,
                    OtherMs: SystemMath.Max(0, totalMs - altarMs)));
                yield break;
            }

            IEnumerator core = RunCore(runStart, runTimestamp);
            while (core.MoveNext())
                yield return core.Current;
        }

        private IEnumerator RunCore(long runStart, long runTimestamp)
        {
            long ctxStart = GC.GetAllocatedBytesForCurrentThread();
            long ctxTimestamp = Stopwatch.GetTimestamp();
            double ctxSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            if (!_dependencies.TickContextFactory.TryCreateRegularClickContext(out ClickTickContext context))
            {
                long earlyCtxBytes = GC.GetAllocatedBytesForCurrentThread() - ctxStart;
                long earlyTotal = GC.GetAllocatedBytesForCurrentThread() - runStart;
                double earlyCtxMs = GetElapsedMs(ctxTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - ctxSleepBefore);
                double earlyTotalMs = GetElapsedMs(runTimestamp) - ClickPipelineTiming.ReadSleepTimeMs();
                _dependencies.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
                    earlyCtxBytes, 0, 0, 0, 0, SystemMath.Max(0, earlyTotal - earlyCtxBytes), earlyTotal,
                    ContextMs: earlyCtxMs,
                    OtherMs: SystemMath.Max(0, earlyTotalMs - earlyCtxMs)));
                yield break;
            }
            long ctxBytes = GC.GetAllocatedBytesForCurrentThread() - ctxStart;
            double ctxMs = GetElapsedMs(ctxTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - ctxSleepBefore);
            RecordStage(PerformanceMonitor.ClickContextStageIndex, ctxBytes, ctxMs);

            long acquireStart = GC.GetAllocatedBytesForCurrentThread();
            long acquireTimestamp = Stopwatch.GetTimestamp();
            double acquireSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            ClickCandidates candidates = _acquisitionPhase.Collect(context);
            long acquireBytes = GC.GetAllocatedBytesForCurrentThread() - acquireStart;
            double acquireMs = GetElapsedMs(acquireTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - acquireSleepBefore);

            long rankStart = GC.GetAllocatedBytesForCurrentThread();
            long rankTimestamp = Stopwatch.GetTimestamp();
            double rankSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            RankingResult ranking = _rankingPhase.Rank(context, candidates);
            long rankBytes = GC.GetAllocatedBytesForCurrentThread() - rankStart;
            double rankMs = GetElapsedMs(rankTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - rankSleepBefore);
            RecordStage(PerformanceMonitor.ClickRankStageIndex, rankBytes, rankMs);

            DecisionResult decision = Gate(candidates, ranking);

            long executeStart = GC.GetAllocatedBytesForCurrentThread();
            long executeTimestamp = Stopwatch.GetTimestamp();
            double executeSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            ExecutionResult executionResult = _executionPhase.Execute(context, candidates, decision);
            long executeBytes = GC.GetAllocatedBytesForCurrentThread() - executeStart;
            double executeMs = GetElapsedMs(executeTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - executeSleepBefore);
            LastTickWasActionable = executionResult.DidActionableWork;

            long postBytes = 0;
            double postMs = 0;
            if (executionResult.ShouldRunPostActions)
            {
                long postStart = GC.GetAllocatedBytesForCurrentThread();
                long postTimestamp = Stopwatch.GetTimestamp();
                double postSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
                IEnumerator postActions = RunPostClickActions(_dependencies.InputHandler, executionResult);
                while (postActions.MoveNext())
                    yield return postActions.Current;
                postBytes = GC.GetAllocatedBytesForCurrentThread() - postStart;
                postMs = GetElapsedMs(postTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - postSleepBefore);
                RecordStage(PerformanceMonitor.ClickPostStageIndex, postBytes, postMs);
            }

            long total = GC.GetAllocatedBytesForCurrentThread() - runStart;
            long stageSum = ctxBytes + acquireBytes + rankBytes + executeBytes + postBytes;
            double totalMs = GetElapsedMs(runTimestamp) - ClickPipelineTiming.ReadSleepTimeMs();
            double stageMsSum = ctxMs + acquireMs + rankMs + executeMs + postMs;
            // OtherMs captures the processing outside the named stages (run prelude, debug-stage publishing,
            // iterator machinery, coroutine yields between stages) so the stage timings account for the whole
            // run; both total and stages exclude the intentional safety sleeps.
            _dependencies.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
                ctxBytes, acquireBytes, rankBytes, executeBytes, postBytes,
                SystemMath.Max(0, total - stageSum), total,
                ContextMs: ctxMs, AcquireMs: acquireMs, RankMs: rankMs, ExecuteMs: executeMs, PostMs: postMs,
                OtherMs: SystemMath.Max(0, totalMs - stageMsSum)));
        }

        private static double GetElapsedMs(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        private static DecisionResult Gate(ClickCandidates candidates, RankingResult ranking)
            => new(
                TrySettlers: ranking.PreferSettlers && candidates.SettlersOre.HasValue,
                TryLostShipment: ranking.PreferLostShipment && candidates.LostShipment.HasValue,
                TryShrine: ranking.PreferShrine,
                GroundItemsVisible: ranking.GroundItemsVisible);
    }
}