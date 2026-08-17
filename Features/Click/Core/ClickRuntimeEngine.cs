namespace ClickIt.Features.Click.Core
{
    internal readonly record struct ClickRuntimeEngineDependencies(
        ClickTelemetryDependencies Telemetry,
        ClickPolicyDependencies Policy,
        ClickSelectionDependencies Selection,
        ClickPathingDependencies Pathing,
        ClickMechanicDependencies Mechanics);

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
        private readonly CandidateAcquisitionEngine _acquisitionPhase = new(dependencies);
        private readonly InteractionExecutionEngine _executionPhase = new(dependencies);

        private void RecordStage(int stageIndex, long bytes, double ms)
            => _dependencies.Telemetry.RecordBreakdownStage?.Invoke(stageIndex, bytes, ms);

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
            long runStart = GC.GetAllocatedBytesForCurrentThread();
            long runTimestamp = Stopwatch.GetTimestamp();
            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("TickStart", "ProcessRegularClick entered", null);

            if (_dependencies.Mechanics.AltarAutomation.HasClickableAltars())
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("AltarBranch", "Clickable altar detected; regular label click path skipped", null);
                long altarStart = GC.GetAllocatedBytesForCurrentThread();
                long altarTimestamp = Stopwatch.GetTimestamp();
                double altarSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
                IEnumerator altar = _dependencies.Mechanics.AltarAutomation.ProcessAltarClicking();
                while (altar.MoveNext())
                    yield return altar.Current;
                long altarBytes = GC.GetAllocatedBytesForCurrentThread() - altarStart;
                double altarMs = GetElapsedMs(altarTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - altarSleepBefore);
                long total = GC.GetAllocatedBytesForCurrentThread() - runStart;
                double totalMs = GetElapsedMs(runTimestamp) - ClickPipelineTiming.ReadSleepTimeMs();
                // The altar branch replaces the whole click path; its processing time is its own stage and the remainder (run prelude / iterator overhead) lands in Other so the click table accounts for the entire run even when an altar consumes it. Both exclude the intentional safety sleeps.
                _dependencies.Telemetry.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
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
            if (!_dependencies.Selection.TickContextFactory.TryCreateRegularClickContext(out ClickTickContext context))
            {
                long earlyCtxBytes = GC.GetAllocatedBytesForCurrentThread() - ctxStart;
                long earlyTotal = GC.GetAllocatedBytesForCurrentThread() - runStart;
                double earlyCtxMs = GetElapsedMs(ctxTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - ctxSleepBefore);
                double earlyTotalMs = GetElapsedMs(runTimestamp) - ClickPipelineTiming.ReadSleepTimeMs();
                _dependencies.Telemetry.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
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
            RankingResult ranking = CandidateRankingEngine.Rank(_dependencies.Selection.LabelSelectionScan, _dependencies.Selection.LabelInteraction, context, candidates);
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

            long postBytes = 0;
            double postMs = 0;
            if (executionResult.ShouldRunPostActions)
            {
                long postStart = GC.GetAllocatedBytesForCurrentThread();
                long postTimestamp = Stopwatch.GetTimestamp();
                double postSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
                IEnumerator postActions = RunPostClickActions(_dependencies.Policy.InputHandler, executionResult);
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
            // OtherMs captures the processing outside the named stages (run prelude, debug-stage publishing, iterator machinery, coroutine yields between stages) so the stage timings account for the whole run; both total and stages exclude the intentional safety sleeps.
            _dependencies.Telemetry.RecordAllocationBreakdown?.Invoke(new ClickAllocationBreakdown(
                ctxBytes, acquireBytes, rankBytes, executeBytes, postBytes,
                SystemMath.Max(0, total - stageSum), total,
                ContextMs: ctxMs, AcquireMs: acquireMs, RankMs: rankMs, ExecuteMs: executeMs, PostMs: postMs,
                OtherMs: SystemMath.Max(0, totalMs - stageMsSum)));
        }

        private static double GetElapsedMs(long startTimestamp)
            => StopwatchMath.ElapsedMs(startTimestamp);

        private static DecisionResult Gate(ClickCandidates candidates, RankingResult ranking)
            => new(
                TrySettlers: ranking.PreferSettlers && candidates.SettlersOre.HasValue,
                TryLostShipment: ranking.PreferLostShipment && candidates.LostShipment.HasValue,
                TryShrine: ranking.PreferShrine,
                GroundItemsVisible: ranking.GroundItemsVisible);
    }
}