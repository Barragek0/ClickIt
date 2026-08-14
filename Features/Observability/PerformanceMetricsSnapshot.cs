namespace ClickIt.Features.Observability
{
    internal readonly record struct FpsMetricsSnapshot(
        double Current,
        double Average,
        double Max);

    // GC pressure per processing section: steady-state alloc/s (bytes per run / run period) plus per-run bytes, so the debug table can rank where allocations actually happen. LastBytesPerRun and AvgPeriodMs let the summary normalize per-run bytes to per-frame last/avg/max; MaxAllocPerSecond is the highest per-sample rate in the live window (a run's bytes / its observed period, floored to a 50ms observation window) — the Max KB/s column on every surface.
    internal readonly record struct GcAllocationSnapshot(
        double AllocPerSecond,
        double AvgBytesPerRun,
        double MaxBytesPerRun,
        long SampleCount,
        double LastBytesPerRun = 0,
        double AvgPeriodMs = 0,
        double MaxAllocPerSecond = 0);

    // Per-stage allocation inside the label scan (bytes allocated by one stage of one run).
    public readonly record struct LabelScanAllocationBreakdown(
        long ListReadBytes,
        long ListAllocBytes,
        long ValidityBytes,
        long SortBytes,
        long TotalBytes);

    // Per-stage allocation snapshot over the same 10s rolling window as the main GC sections: MaxAllocPerSecond is the highest per-sample rate (a run's bytes / its observed period, floored to a 50ms observation window) — the Max KB/s column on every surface.
    public readonly record struct AllocationStageSnapshot(
        double LastBytesPerRun,
        double AvgBytesPerRun,
        double MaxBytesPerRun,
        double MaxAllocPerSecond = 0);

    // Rolling per-stage label-scan allocation stats so the debug tables can show last/avg/max bytes-per-run for each stage instead of the whole scan's aggregate.
    public readonly record struct LabelScanAllocationStats(
        AllocationStageSnapshot ListRead,
        AllocationStageSnapshot ListAlloc,
        AllocationStageSnapshot Validity,
        AllocationStageSnapshot Sort,
        long SampleCount);

    // Per-stage allocation inside the click pipeline (bytes allocated by one stage of one run). OtherBytes captures click work outside the named stages (Run() prelude, debug-stage publishing, iterator machinery) so the stages sum to the recorded TotalBytes. The *Ms fields carry the same stages' wall-clock time so the debug tables can show time AND allocation.
    public readonly record struct ClickAllocationBreakdown(
        long ContextBytes,
        long AcquireBytes,
        long RankBytes,
        long ExecuteBytes,
        long PostBytes,
        long OtherBytes = 0,
        long TotalBytes = 0,
        double ContextMs = 0,
        double AcquireMs = 0,
        double RankMs = 0,
        double ExecuteMs = 0,
        double PostMs = 0,
        long AltarBytes = 0,
        double AltarMs = 0,
        double OtherMs = 0);

    public readonly record struct TimingStageSnapshot(double LastMs, double AvgMs, double MaxMs);

    // One named stage of a feature breakdown (allocation + wall time, like the click stages).
    public readonly record struct BreakdownStageSnapshot(
        string Name,
        AllocationStageSnapshot Allocation,
        TimingStageSnapshot Time);

    // Per-stage breakdown of one processing section over the same rolling windows as the main GC table, so the debug surfaces can drill from an area into its sub-steps.
    public readonly record struct BreakdownStats(
        IReadOnlyList<BreakdownStageSnapshot> Stages,
        long SampleCount);

    // Rolling per-stage click-pipeline allocation stats for the same last/avg/max breakdown.
    public readonly record struct ClickAllocationStats(
        AllocationStageSnapshot Context,
        AllocationStageSnapshot Acquire,
        AllocationStageSnapshot Rank,
        AllocationStageSnapshot Execute,
        AllocationStageSnapshot Post,
        AllocationStageSnapshot Other,
        long SampleCount,
        TimingStageSnapshot ContextTime = default,
        TimingStageSnapshot AcquireTime = default,
        TimingStageSnapshot RankTime = default,
        TimingStageSnapshot ExecuteTime = default,
        TimingStageSnapshot PostTime = default,
        AllocationStageSnapshot Altar = default,
        TimingStageSnapshot AltarTime = default,
        TimingStageSnapshot OtherTime = default);

    // Process + managed-heap memory picture. ProcessWorkingSetMb is the WHOLE process (game + ExileCore/ExileApi + plugin), so per-feature attribution is not possible — the GC table's alloc/s column is the per-feature allocation proxy instead. GcPause* carries the recent blocking GC pause picture (last/avg/max ms + pause-time % of the last observed window), which is what actually stalls all threads when the plugin churns the heap. DlrReads* carries the recent DynamicAccess read picture — reads/sec plus the actual ms/sec spent inside the dynamic reads (the freeze-relevant cost) — with the share of reads that failed (%).
    internal readonly record struct MemoryMetricsSnapshot(
        double ProcessWorkingSetMb,
        double ManagedHeapMb,
        double Gen0Mb,
        double Gen1Mb,
        double Gen2Mb,
        double LohMb,
        double FragmentedMb,
        double MemoryLoadPercent,
        double GcPauseLastMs = 0,
        double GcPauseAvgMs = 0,
        double GcPauseMaxMs = 0,
        double GcPauseTimePercent = 0,
        double DlrReadsLastPerSec = 0,
        double DlrReadsAvgPerSec = 0,
        double DlrReadsMaxPerSec = 0,
        double DlrFailPercent = 0,
        double DlrReadsMsLastPerSec = 0,
        double DlrReadsMsAvgPerSec = 0,
        double DlrReadsMsMaxPerSec = 0,
        IReadOnlyList<DlrSectionSnapshot>? DlrSections = null,
        IReadOnlyList<GcSectionSnapshot>? GcSections = null);

    // One feature's DLR-read attribution over the recent window: reads/sec plus the actual ms/sec spent inside the dynamic reads (last/avg/max). Indexed by ProcessingSection value; the DLR table maps each entry to a feature row so the per-feature dynamic-read pressure is visible.
    internal readonly record struct DlrSectionSnapshot(
        double ReadsLastPerSec = 0,
        double ReadsAvgPerSec = 0,
        double ReadsMaxPerSec = 0,
        double MsLastPerSec = 0,
        double MsAvgPerSec = 0,
        double MsMaxPerSec = 0);

    // One feature's GC allocation-rate attribution over the recent window: bytes/sec (last/avg/max), sampled on the same 500ms cadence as the timing tables so the GC table's Last/Avg/Max matches the other tables (10s expiry, 10-sample average, max over the live window). Indexed by ProcessingSection value; the GC table maps each entry to a feature row.
    internal readonly record struct GcSectionSnapshot(
        double BytesLastPerSec = 0,
        double BytesAvgPerSec = 0,
        double BytesMaxPerSec = 0);

    // Observed interval between consecutive marks of one periodic event (click run, blight refresh, label scan), in ms.
    internal readonly record struct IntervalTimingSnapshot(double LastMs, double AvgMs, double MaxMs, long SampleCount);

    internal readonly record struct TimingMetricsSnapshot(
        double LastMs,
        double AverageMs,
        double MaxMs,
        long SampleCount,
        double AveragePeriodMs = 0)
    {
        // Fraction of wall time the coroutine was executing (AverageMs of run / run period).
        public double DutyCyclePercent
            => AveragePeriodMs > 0 ? AverageMs / AveragePeriodMs * 100 : 0;

        // Multiplier converting a per-run ms value to per-frame ms (runs per second / frames per second).
        public double PerFrameScale(double fps)
            => AveragePeriodMs > 0 && fps > 0 ? 1000.0 / AveragePeriodMs / fps : 0;

        // Cost normalized to one frame so background coroutines are comparable to the render table.
        public double PerFrameMs(double fps)
            => PerFrameScale(fps) * AverageMs;
    }

    internal readonly record struct PerformanceMetricsSnapshot(
        FpsMetricsSnapshot Fps,
        TimingMetricsSnapshot Render,
        TimingMetricsSnapshot LazyMode,
        TimingMetricsSnapshot DebugOverlay,
        TimingMetricsSnapshot AltarOverlay,
        TimingMetricsSnapshot UltimatumOverlay,
        TimingMetricsSnapshot StrongboxOverlay,
        TimingMetricsSnapshot PathfindingOverlay,
        TimingMetricsSnapshot HarvestOverlay,
        TimingMetricsSnapshot BlightOverlay,
        TimingMetricsSnapshot ClickHotkeyToggle = default,
        TimingMetricsSnapshot InventoryFullWarning = default,
        TimingMetricsSnapshot UiRegionRectangle = default,
        TimingMetricsSnapshot PerformanceOverlay = default,
        TimingMetricsSnapshot TextFlush = default,
        TimingMetricsSnapshot FrameFlush = default,
        TimingMetricsSnapshot AltarCoroutine = default,
        TimingMetricsSnapshot ClickCoroutine = default,
        TimingMetricsSnapshot FlareCoroutine = default,
        TimingMetricsSnapshot BlightCoroutine = default,
        TimingMetricsSnapshot UltimatumCoroutine = default,
        TimingMetricsSnapshot LabelOverlayCoroutine = default,
        TimingMetricsSnapshot AltarProcessing = default,
        TimingMetricsSnapshot BlightProcessing = default,
        TimingMetricsSnapshot ClickProcessing = default,
        TimingMetricsSnapshot FlareProcessing = default,
        TimingMetricsSnapshot HarvestProcessing = default,
        TimingMetricsSnapshot LabelProcessing = default,
        TimingMetricsSnapshot PathfindingProcessing = default,
        TimingMetricsSnapshot StrongboxProcessing = default,
        TimingMetricsSnapshot UltimatumProcessing = default,
        TimingMetricsSnapshot AreaBlockedUiProcessing = default,
        TimingMetricsSnapshot ManualUiHoverProcessing = default,
        TimingMetricsSnapshot GameStateDumpProcessing = default,
        double ClickTargetIntervalMs = 0,
        double AverageSuccessfulClickTimingMs = 0,
        double AverageClickIntervalMs = 0,
        double AverageClickSleepMs = 0,
        IReadOnlyDictionary<ProcessingSection, GcAllocationSnapshot>? Allocations = null,
        LabelScanAllocationStats LabelScanAllocation = default,
        ClickAllocationStats ClickAllocation = default,
        MemoryMetricsSnapshot Memory = default,
        IReadOnlyDictionary<ProcessingSection, BreakdownStats>? Breakdowns = null,
        TimingMetricsSnapshot ClickSleepTiming = default,
        IReadOnlyDictionary<IntervalKind, IntervalTimingSnapshot>? Intervals = null)
    {
        public GcAllocationSnapshot GetAllocationSection(ProcessingSection section)
            => Allocations != null && Allocations.TryGetValue(section, out GcAllocationSnapshot value)
                ? value
                : default;

        public TimingMetricsSnapshot GetRenderSection(RenderSection section)
            => section switch
            {
                RenderSection.LazyMode => LazyMode,
                RenderSection.DebugOverlay => DebugOverlay,
                RenderSection.AltarOverlay => AltarOverlay,
                RenderSection.UltimatumOverlay => UltimatumOverlay,
                RenderSection.StrongboxOverlay => StrongboxOverlay,
                RenderSection.PathfindingOverlay => PathfindingOverlay,
                RenderSection.HarvestOverlay => HarvestOverlay,
                RenderSection.BlightOverlay => BlightOverlay,
                RenderSection.ClickHotkeyToggle => ClickHotkeyToggle,
                RenderSection.InventoryFullWarning => InventoryFullWarning,
                RenderSection.UiRegionRectangle => UiRegionRectangle,
                RenderSection.PerformanceOverlay => PerformanceOverlay,
                RenderSection.TextFlush => TextFlush,
                RenderSection.FrameFlush => FrameFlush,
                RenderSection.Unknown => default,
                _ => default,
            };

        public TimingMetricsSnapshot GetProcessingSection(ProcessingSection section)
            => section switch
            {
                ProcessingSection.Altar => AltarProcessing,
                ProcessingSection.Blight => BlightProcessing,
                ProcessingSection.Click => ClickProcessing,
                ProcessingSection.Flare => FlareProcessing,
                ProcessingSection.Harvest => HarvestProcessing,
                ProcessingSection.Label => LabelProcessing,
                ProcessingSection.Pathfinding => PathfindingProcessing,
                ProcessingSection.Strongbox => StrongboxProcessing,
                ProcessingSection.Ultimatum => UltimatumProcessing,
                ProcessingSection.AreaBlockedUi => AreaBlockedUiProcessing,
                ProcessingSection.ManualUiHover => ManualUiHoverProcessing,
                ProcessingSection.GameStateDump => GameStateDumpProcessing,
                ProcessingSection.Unknown => default,
                _ => default,
            };

        public TimingMetricsSnapshot GetCoroutineTiming(TimingChannel channel)
            => channel switch
            {
                TimingChannel.Altar => AltarCoroutine,
                TimingChannel.Click => ClickCoroutine,
                TimingChannel.Flare => FlareCoroutine,
                TimingChannel.Blight => BlightCoroutine,
                TimingChannel.Ultimatum => UltimatumCoroutine,
                TimingChannel.LabelOverlay => LabelOverlayCoroutine,
                TimingChannel.Render => Render,
                TimingChannel.Unknown => default,
                _ => default,
            };

        // Combined background-coroutine cost: last/avg are summed across every channel with samples, max is the worst single spike. Mirrors the Render summary line in the debug overlay.
        public TimingMetricsSnapshot CoroutinesTotal
        {
            get
            {
                double last = 0, avg = 0, max = 0;
                long channels = 0;
                Aggregate(AltarCoroutine);
                Aggregate(ClickCoroutine);
                Aggregate(FlareCoroutine);
                Aggregate(BlightCoroutine);
                Aggregate(UltimatumCoroutine);
                Aggregate(LabelOverlayCoroutine);
                return new TimingMetricsSnapshot(last, avg, max, channels);

                void Aggregate(TimingMetricsSnapshot s)
                {
                    if (s.SampleCount == 0)
                        return;
                    last += s.LastMs;
                    avg += s.AverageMs;
                    if (s.MaxMs > max)
                        max = s.MaxMs;
                    channels++;
                }
            }
        }

        // Combined background-coroutine cost normalized per frame: last/avg/max summed across every channel with a measured run period (scaled by that channel's own period), so the totals row is the combination of all rows beneath it.
        public TimingMetricsSnapshot CoroutinesTotalPerFrameSnapshot
        {
            get
            {
                double fps = Fps.Current;
                double last = 0, avg = 0, max = 0;
                long channels = 0;
                Aggregate(AltarCoroutine);
                Aggregate(ClickCoroutine);
                Aggregate(FlareCoroutine);
                Aggregate(BlightCoroutine);
                Aggregate(UltimatumCoroutine);
                Aggregate(LabelOverlayCoroutine);
                return new TimingMetricsSnapshot(last, avg, max, channels);

                void Aggregate(TimingMetricsSnapshot s)
                {
                    double scale = s.PerFrameScale(fps);
                    if (s.SampleCount == 0 || scale <= 0)
                        return;
                    last += s.LastMs * scale;
                    avg += s.AverageMs * scale;
                    max += s.MaxMs * scale;
                    channels++;
                }
            }
        }

        public double CoroutinesTotalPerFrame => CoroutinesTotalPerFrameSnapshot.AverageMs;

        // Combined feature-processing cost normalized per frame: last/avg/max summed across every section with a measured run period (scaled by that section's own period), so the totals row is the combination of all rows beneath it.
        public TimingMetricsSnapshot ProcessingTotalPerFrameSnapshot
        {
            get
            {
                double fps = Fps.Current;
                double last = 0, avg = 0, max = 0;
                long sections = 0;
                Aggregate(AltarProcessing);
                Aggregate(BlightProcessing);
                Aggregate(ClickProcessing);
                Aggregate(FlareProcessing);
                Aggregate(HarvestProcessing);
                Aggregate(LabelProcessing);
                Aggregate(PathfindingProcessing);
                Aggregate(StrongboxProcessing);
                Aggregate(UltimatumProcessing);
                Aggregate(AreaBlockedUiProcessing);
                Aggregate(ManualUiHoverProcessing);
                Aggregate(GameStateDumpProcessing);
                return new TimingMetricsSnapshot(last, avg, max, sections);

                void Aggregate(TimingMetricsSnapshot s)
                {
                    double scale = s.PerFrameScale(fps);
                    if (s.SampleCount == 0 || scale <= 0)
                        return;
                    last += s.LastMs * scale;
                    avg += s.AverageMs * scale;
                    max += s.MaxMs * scale;
                    sections++;
                }
            }
        }

        // Render-table totals: last/avg/max summed across every render section with samples, so the totals row is the combination of all rows beneath it.
        public TimingMetricsSnapshot RenderTableTotal
        {
            get
            {
                double last = 0, avg = 0, max = 0;
                long sections = 0;
                PerformanceMetricsSnapshot self = this;
                Add(RenderSection.AltarOverlay);
                Add(RenderSection.BlightOverlay);
                Add(RenderSection.ClickHotkeyToggle);
                Add(RenderSection.DebugOverlay);
                Add(RenderSection.FrameFlush);
                Add(RenderSection.TextFlush);
                Add(RenderSection.HarvestOverlay);
                Add(RenderSection.InventoryFullWarning);
                Add(RenderSection.LazyMode);
                Add(RenderSection.PathfindingOverlay);
                Add(RenderSection.PerformanceOverlay);
                Add(RenderSection.StrongboxOverlay);
                Add(RenderSection.UiRegionRectangle);
                Add(RenderSection.UltimatumOverlay);
                return new TimingMetricsSnapshot(last, avg, max, sections);

                void Add(RenderSection section)
                {
                    TimingMetricsSnapshot s = self.GetRenderSection(section);
                    if (s.SampleCount == 0)
                        return;
                    last += s.LastMs;
                    avg += s.AverageMs;
                    max += s.MaxMs;
                    sections++;
                }
            }
        }

        // GC-table totals matching the units of each row beneath: last rate normalized to bytes per frame, total average allocation per second, and total max bytes per single run.
        public (double LastBytesPerFrame, double TotalBytesPerSecond, double TotalMaxBytesPerRun) GcTableTotalBytesPerFrame
        {
            get
            {
                double fps = Fps.Current;
                double last = 0, totalPerSecond = 0, totalMaxRun = 0;
                if (Allocations != null)
                {
                    foreach (KeyValuePair<ProcessingSection, GcAllocationSnapshot> entry in Allocations)
                    {
                        GcAllocationSnapshot s = entry.Value;
                        if (fps > 0)
                            last += s.AllocPerSecond / fps;
                        totalPerSecond += s.AllocPerSecond;
                        totalMaxRun += s.MaxBytesPerRun;
                    }
                }
                return (last, totalPerSecond, totalMaxRun);
            }
        }

        // Total peak allocation rate across every main GC row (the section rows only — the click and label-scan stage breakdown rows are sub-parts of their parent section, so including them would double-count), matching the section-only sum convention of GcTableTotalBytesPerFrame.
        public double GcTableTotalMaxBytesPerSecond
        {
            get
            {
                double total = 0;
                if (Allocations != null)
                {
                    foreach (KeyValuePair<ProcessingSection, GcAllocationSnapshot> entry in Allocations)
                        total += entry.Value.MaxAllocPerSecond;
                }
                return total;
            }
        }

        // Combined feature-processing cost: last/avg are summed across every section with samples, max is the worst single spike. Mirrors the CoroutinesTotal aggregate for the summary line.
        public TimingMetricsSnapshot ProcessingTotal
        {
            get
            {
                double last = 0, avg = 0, max = 0;
                long sections = 0;
                Aggregate(AltarProcessing);
                Aggregate(BlightProcessing);
                Aggregate(ClickProcessing);
                Aggregate(FlareProcessing);
                Aggregate(HarvestProcessing);
                Aggregate(LabelProcessing);
                Aggregate(PathfindingProcessing);
                Aggregate(StrongboxProcessing);
                Aggregate(UltimatumProcessing);
                Aggregate(AreaBlockedUiProcessing);
                Aggregate(ManualUiHoverProcessing);
                return new TimingMetricsSnapshot(last, avg, max, sections);

                void Aggregate(TimingMetricsSnapshot s)
                {
                    if (s.SampleCount == 0)
                        return;
                    last += s.LastMs;
                    avg += s.AverageMs;
                    if (s.MaxMs > max)
                        max = s.MaxMs;
                    sections++;
                }
            }
        }
    }
}
