namespace ClickIt.Features.Click.Core
{
    internal sealed class CandidateAcquisitionEngine(CandidateAcquisitionEngineDependencies dependencies)
    {
        private readonly CandidateAcquisitionEngineDependencies _dependencies = dependencies;

        public ClickCandidates Collect(ClickTickContext context)
        {
            int allLabelsCount = context.AllLabels?.Count ?? 0;
            bool captureClickDebug = _dependencies.ClickDebugPublisher.ShouldCaptureClickDebug();
            string labelSourceSummary = _dependencies.ShouldCaptureClickDebug()
                ? _dependencies.LabelInteraction.BuildLabelSourceDebugSummary(context.AllLabels)
                : string.Empty;

            if (!context.GroundItemsVisible)
            {
                long mechanicStart = GC.GetAllocatedBytesForCurrentThread();
                long mechanicTimestamp = Stopwatch.GetTimestamp();
                VisibleMechanicSelectionSnapshot hiddenFallbackSelection = _dependencies.VisibleMechanics.GetHiddenFallbackSelectionSnapshot();
                long mechanicBytes = GC.GetAllocatedBytesForCurrentThread() - mechanicStart;
                double mechanicMs = GetElapsedMs(mechanicTimestamp);
                _dependencies.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickMechanicScanStageIndex, mechanicBytes, mechanicMs);
                if (captureClickDebug)
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHidden",
                        $"{labelSourceSummary} | hiddenFallback settlers={hiddenFallbackSelection.HasSettlers} lostShipment={hiddenFallbackSelection.HasLostShipment}", null);

                long labelScanStart = GC.GetAllocatedBytesForCurrentThread();
                long labelScanTimestamp = Stopwatch.GetTimestamp();
                LabelOnGround? hiddenLabel = _dependencies.LabelSelectionScan.ResolveNextLabelCandidate(context.AllLabels);
                string? hiddenLabelMechanicId = hiddenLabel != null
                    ? _dependencies.LabelInteractionPort.GetMechanicIdForLabel(hiddenLabel)
                    : null;
                long labelScanBytes = GC.GetAllocatedBytesForCurrentThread() - labelScanStart;
                double labelScanMs = GetElapsedMs(labelScanTimestamp);
                _dependencies.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickLabelScanStageIndex, labelScanBytes, labelScanMs);

                if (hiddenLabel != null)
                {
                    if (captureClickDebug)
                        _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelFound",
                            $"{labelSourceSummary} | mechanic={hiddenLabelMechanicId} {ClickLabelSelectionMath.DescribeLabel(hiddenLabel)} {ClickLabelSelectionMath.DescribeCursorPosition()}", hiddenLabelMechanicId);
                }
                else if (captureClickDebug)
                {
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelNull",
                        $"{labelSourceSummary} | ResolveNextLabelCandidate returned null despite {allLabelsCount} labels in context", null);
                }

                return new ClickCandidates(hiddenFallbackSelection.LostShipment, hiddenFallbackSelection.Settlers, hiddenLabel, hiddenLabelMechanicId);
            }

            long visMechanicStart = GC.GetAllocatedBytesForCurrentThread();
            long visMechanicTimestamp = Stopwatch.GetTimestamp();
            VisibleMechanicSelectionSnapshot visibleMechanicSelection = _dependencies.VisibleMechanics.GetVisibleMechanicSelectionSnapshotForLabels(context.AllLabels);
            long visMechanicBytes = GC.GetAllocatedBytesForCurrentThread() - visMechanicStart;
            double visMechanicMs = GetElapsedMs(visMechanicTimestamp);
            _dependencies.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickMechanicScanStageIndex, visMechanicBytes, visMechanicMs);
            if (captureClickDebug)
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("LabelSource", labelSourceSummary, null);


            long visLabelScanStart = GC.GetAllocatedBytesForCurrentThread();
            long visLabelScanTimestamp = Stopwatch.GetTimestamp();
            LabelOnGround? nextLabel = _dependencies.LabelSelectionScan.ResolveNextLabelCandidate(context.AllLabels);
            string? nextLabelMechanicId = nextLabel != null
                ? _dependencies.LabelInteractionPort.GetMechanicIdForLabel(nextLabel)
                : null;
            long visLabelScanBytes = GC.GetAllocatedBytesForCurrentThread() - visLabelScanStart;
            double visLabelScanMs = GetElapsedMs(visLabelScanTimestamp);
            _dependencies.RecordBreakdownStage?.Invoke(PerformanceMonitor.ClickLabelScanStageIndex, visLabelScanBytes, visLabelScanMs);

            if (nextLabel != null)
            {
                if (captureClickDebug)
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleLabelFound",
                        $"{labelSourceSummary} | mechanic={nextLabelMechanicId} {ClickLabelSelectionMath.DescribeLabel(nextLabel)} {ClickLabelSelectionMath.DescribeCursorPosition()}", nextLabelMechanicId);
            }
            else if (captureClickDebug)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleLabelNull",
                    $"{labelSourceSummary} | ResolveNextLabelCandidate returned null", null);
            }

            nextLabelMechanicId = OffscreenPathingMath.ResolveLabelMechanicIdForVisibleCandidateComparison(
                nextLabelMechanicId,
                hasLabel: nextLabel != null,
                isWorldItemLabel: nextLabel?.ItemOnGround?.Type == EntityType.WorldItem,
                clickItemsEnabled: _dependencies.Settings.ClickItems.Value);

            return new ClickCandidates(visibleMechanicSelection.LostShipment, visibleMechanicSelection.Settlers, nextLabel, nextLabelMechanicId);
        }

        private static double GetElapsedMs(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}