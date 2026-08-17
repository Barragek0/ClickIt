namespace ClickIt.Features.Click.Core
{
    internal sealed class CandidateAcquisitionEngine(ClickRuntimeEngineDependencies dependencies)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = dependencies;

        public ClickCandidates Collect(ClickTickContext context)
        {
            int allLabelsCount = context.AllLabels?.Count ?? 0;
            bool captureClickDebug = _dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug();
            string labelSourceSummary = _dependencies.Telemetry.ShouldCaptureClickDebug()
                ? _dependencies.Selection.LabelInteraction.BuildLabelSourceDebugSummary(context.AllLabels)
                : string.Empty;

            if (!context.GroundItemsVisible)
            {
                long mechanicStart = GC.GetAllocatedBytesForCurrentThread();
                long mechanicTimestamp = Stopwatch.GetTimestamp();
                double mechanicSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
                VisibleMechanicSelectionSnapshot hiddenFallbackSelection = _dependencies.Selection.VisibleMechanics.GetHiddenFallbackSelectionSnapshot();
                long mechanicBytes = GC.GetAllocatedBytesForCurrentThread() - mechanicStart;
                double mechanicMs = GetElapsedMs(mechanicTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - mechanicSleepBefore);
                _dependencies.Telemetry.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickMechanicScanStageIndex, mechanicBytes, mechanicMs);
                if (captureClickDebug)
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHidden",
                        $"{labelSourceSummary} | hiddenFallback settlers={hiddenFallbackSelection.HasSettlers} lostShipment={hiddenFallbackSelection.HasLostShipment}", null);

                long labelScanStart = GC.GetAllocatedBytesForCurrentThread();
                long labelScanTimestamp = Stopwatch.GetTimestamp();
                double labelScanSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
                LabelOnGround? hiddenLabel = _dependencies.Selection.LabelSelectionScan.ResolveNextLabelCandidate(context.AllLabels);
                string? hiddenLabelMechanicId = hiddenLabel != null
                    ? _dependencies.Selection.LabelInteractionPort.GetMechanicIdForLabel(hiddenLabel)
                    : null;
                long labelScanBytes = GC.GetAllocatedBytesForCurrentThread() - labelScanStart;
                double labelScanMs = GetElapsedMs(labelScanTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - labelScanSleepBefore);
                _dependencies.Telemetry.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickLabelScanStageIndex, labelScanBytes, labelScanMs);

                if (hiddenLabel != null)
                {
                    if (captureClickDebug)
                        _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelFound",
                            $"{labelSourceSummary} | mechanic={hiddenLabelMechanicId} {ClickLabelSelectionMath.DescribeLabel(hiddenLabel)} {ClickLabelSelectionMath.DescribeCursorPosition()}", hiddenLabelMechanicId);
                }
                else if (captureClickDebug)
                {
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelNull",
                        $"{labelSourceSummary} | ResolveNextLabelCandidate returned null despite {allLabelsCount} labels in context", null);
                }

                return new ClickCandidates(hiddenFallbackSelection.LostShipment, hiddenFallbackSelection.Settlers, hiddenLabel, hiddenLabelMechanicId);
            }

            long visMechanicStart = GC.GetAllocatedBytesForCurrentThread();
            long visMechanicTimestamp = Stopwatch.GetTimestamp();
            double visMechanicSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            VisibleMechanicSelectionSnapshot visibleMechanicSelection = _dependencies.Selection.VisibleMechanics.GetVisibleMechanicSelectionSnapshotForLabels(context.AllLabels);
            long visMechanicBytes = GC.GetAllocatedBytesForCurrentThread() - visMechanicStart;
            double visMechanicMs = GetElapsedMs(visMechanicTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - visMechanicSleepBefore);
            _dependencies.Telemetry.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickMechanicScanStageIndex, visMechanicBytes, visMechanicMs);
            if (captureClickDebug)
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("LabelSource", labelSourceSummary, null);


            long visLabelScanStart = GC.GetAllocatedBytesForCurrentThread();
            long visLabelScanTimestamp = Stopwatch.GetTimestamp();
            double visLabelScanSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            LabelOnGround? nextLabel = _dependencies.Selection.LabelSelectionScan.ResolveNextLabelCandidate(context.AllLabels);
            string? nextLabelMechanicId = nextLabel != null
                ? _dependencies.Selection.LabelInteractionPort.GetMechanicIdForLabel(nextLabel)
                : null;
            long visLabelScanBytes = GC.GetAllocatedBytesForCurrentThread() - visLabelScanStart;
            double visLabelScanMs = GetElapsedMs(visLabelScanTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - visLabelScanSleepBefore);
            _dependencies.Telemetry.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickLabelScanStageIndex, visLabelScanBytes, visLabelScanMs);

            if (nextLabel != null)
            {
                if (captureClickDebug)
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleLabelFound",
                        $"{labelSourceSummary} | mechanic={nextLabelMechanicId} {ClickLabelSelectionMath.DescribeLabel(nextLabel)} {ClickLabelSelectionMath.DescribeCursorPosition()}", nextLabelMechanicId);
            }
            else if (captureClickDebug)
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleLabelNull",
                    $"{labelSourceSummary} | ResolveNextLabelCandidate returned null", null);
            }

            nextLabelMechanicId = OffscreenPathingMath.ResolveLabelMechanicIdForVisibleCandidateComparison(
                nextLabelMechanicId,
                hasLabel: nextLabel != null,
                isWorldItemLabel: nextLabel?.ItemOnGround?.Type == EntityType.WorldItem,
                clickItemsEnabled: _dependencies.Policy.Settings.ClickItems.Value);

            return new ClickCandidates(visibleMechanicSelection.LostShipment, visibleMechanicSelection.Settlers, nextLabel, nextLabelMechanicId);
        }

        private static double GetElapsedMs(long startTimestamp)
            => StopwatchMath.ElapsedMs(startTimestamp);
    }
}