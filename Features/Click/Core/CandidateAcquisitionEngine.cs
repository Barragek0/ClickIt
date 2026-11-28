namespace ClickIt.Features.Click.Core
{
    internal sealed class CandidateAcquisitionEngine(ClickRuntimeEngineDependencies dependencies)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = dependencies;

        public ClickCandidates Collect(ClickTickContext context)
        {
            int allLabelsCount = context.AllLabels?.Count ?? 0;
            bool captureClickDebug = _dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug();
            string? firstSettlementRejection = null;
            Func<Entity?, string?, bool>? settlementEligibility = context.IsPostChestLootSettleBlocking
                ? (entity, mechanicId) =>
                {
                    bool allowed = _dependencies.Selection.ChestLootSettlement
                        .ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out string decision);
                    if (!allowed && firstSettlementRejection == null)
                        firstSettlementRejection = decision;
                    return allowed;
                }
            : null;
            string labelSourceSummary = _dependencies.Telemetry.ShouldCaptureClickDebug()
                ? _dependencies.Selection.LabelInteraction.BuildLabelSourceDebugSummary(context.AllLabels)
                : string.Empty;

            bool visible = context.GroundItemsVisible;
            long mechanicStart = GC.GetAllocatedBytesForCurrentThread();
            long mechanicTimestamp = Stopwatch.GetTimestamp();
            double mechanicSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            VisibleMechanicSelectionSnapshot mechanics = visible
                ? _dependencies.Selection.VisibleMechanics.GetVisibleMechanicSelectionSnapshotForLabels(context.AllLabels)
                : _dependencies.Selection.VisibleMechanics.GetHiddenFallbackSelectionSnapshot();
            long mechanicBytes = GC.GetAllocatedBytesForCurrentThread() - mechanicStart;
            double mechanicMs = GetElapsedMs(mechanicTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - mechanicSleepBefore);
            _dependencies.Telemetry.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickMechanicScanStageIndex, mechanicBytes, mechanicMs);

            if (captureClickDebug)
            {
                if (visible)
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("LabelSource", labelSourceSummary, null);
                else
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHidden",
                        $"{labelSourceSummary} | hiddenFallback settlers={mechanics.HasSettlers} lostShipment={mechanics.HasLostShipment}", null);
            }

            long labelScanStart = GC.GetAllocatedBytesForCurrentThread();
            long labelScanTimestamp = Stopwatch.GetTimestamp();
            double labelScanSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            (LabelOnGround? label, string? mechanicId) = _dependencies.Selection.LabelSelectionScan
                .ResolveNextLabelCandidate(context.AllLabels, settlementEligibility);
            if (label == null && firstSettlementRejection != null)
                PublishChestSettleRejection(firstSettlementRejection);
            long labelScanBytes = GC.GetAllocatedBytesForCurrentThread() - labelScanStart;
            double labelScanMs = GetElapsedMs(labelScanTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - labelScanSleepBefore);
            _dependencies.Telemetry.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickLabelScanStageIndex, labelScanBytes, labelScanMs);

            if (captureClickDebug)
            {
                if (label != null)
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(visible ? "VisibleLabelFound" : "HiddenLabelFound",
                        $"{labelSourceSummary} | mechanic={mechanicId} {ClickLabelSelectionMath.DescribeLabel(label)} {ClickLabelSelectionMath.DescribeCursorPosition()}", mechanicId);
                else
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(visible ? "VisibleLabelNull" : "HiddenLabelNull",
                        $"{labelSourceSummary} | ResolveNextLabelCandidate returned null{(visible ? string.Empty : $" despite {allLabelsCount} labels in context")}", null);
            }

            if (visible)
                mechanicId = OffscreenPathingMath.ResolveLabelMechanicIdForVisibleCandidateComparison(
                    mechanicId,
                    hasLabel: label != null,
                    isWorldItemLabel: label?.ItemOnGround?.Type == EntityType.WorldItem,
                    clickItemsEnabled: _dependencies.Policy.Settings.ClickItems.Value);

            return new ClickCandidates(mechanics.LostShipment, mechanics.Settlers, label, mechanicId);
        }

        private void PublishChestSettleRejection(string decision)
            => _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                "PostChestLootSettleBlocked",
                $"No nearby label candidate passed the settle bypass ({decision})",
                null);

        private static double GetElapsedMs(long startTimestamp)
            => StopwatchMath.ElapsedMs(startTimestamp);
    }
}
