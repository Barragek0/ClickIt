namespace ClickIt.UI.Debug
{
    /// <summary>
    /// In-game performance overlay: renders the full performance tables (render, processing,
    /// coroutine ms/f + ms/r, GC, memory) as text on the game screen, gated by RenderPerformanceInGame.
    /// Draws into the deferred text queue every frame with per-cell X positions so the proportional
    /// game font does not break column alignment. Mirrors the debug UI's two-column layout.
    /// </summary>
    internal sealed class PerformanceInGameOverlay(Func<PerformanceMetricsSnapshot> getSnapshot, Func<bool>? isInMapProvider = null) : IOverlay
    {
        private static readonly Color HeaderColor = Color.Orange;
        private static readonly Color LabelColor = Color.White;

        // All three-value tables (render, CR ms/f, processing) share the column positions so every Last/Avg/Max value lines up vertically. The GC and DLR tables use the same columns.
        private static readonly float[] FourCol = [140f, 225f, 310f];
        private static readonly float[] TwoCol = [110f];

        private readonly Func<PerformanceMetricsSnapshot> _getSnapshot = getSnapshot ?? throw new ArgumentNullException(nameof(getSnapshot));
        private readonly Func<bool>? _isInMapProvider = isInMapProvider;

        // Rebuild the performance snapshot at most every 200ms (the buffers it aggregates only change at that cadence) so the per-frame draw does not reallocate snapshot arrays/dictionaries on the render thread.
        private const long SnapshotCacheIntervalMs = 200;
        private PerformanceMetricsSnapshot? _cachedSnapshot;
        private long _lastSnapshotAtMs;

        public string Name => "Performance";

        public RenderSection Section => RenderSection.PerformanceOverlay;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.None;

        public TimingChannel? RefreshTimingChannel => null;

        public ProcessingSection ProcessingSection => ProcessingSection.Unknown;

        public bool IsEnabled(ClickItSettings settings)
            => settings.RenderPerformanceInGame.Value
               && (!settings.OnlyShowPerformanceInGameWhileInMap.Value || (_isInMapProvider?.Invoke() ?? false));

        public void Refresh(OverlayRefreshContext ctx)
        {
        }

        public void Draw(OverlayRenderContext ctx)
        {
            long now = Environment.TickCount64;
            PerformanceMetricsSnapshot perf = _cachedSnapshot ??= _getSnapshot();
            if (now - _lastSnapshotAtMs >= SnapshotCacheIntervalMs)
            {
                perf = _getSnapshot();
                _cachedSnapshot = perf;
                _lastSnapshotAtMs = now;
            }
            if (perf.Fps.Max <= 0)
                return;

            const float lineHeight = 15f;
            const float originX = 5f;
            const float originY = 100f;
            const float colWidth = 400f;
            TextBlock left = new(ctx.DrawQueue, originX, originY, lineHeight);
            TextBlock center = new(ctx.DrawQueue, originX + colWidth, originY, lineHeight);

            // Column 1: render + coroutine + DLR + interval + memory.
            left.TitleHeader(FourCol, "Render ms/f", "Last", "Avg", "Max");
            RenderTotalRow(left, perf.RenderTableTotal, FrameColor, "F1", "F2", "F1");
            RenderRenderTable(left, perf);

            left.Blank();
            left.TitleHeader(FourCol, "Coroutine ms/f", "Last", "Avg", "Max");
            RenderTotalRow(left, perf.CoroutinesTotalPerFrameSnapshot, FrameColor, "F2", "F2", "F2");
            RenderFrameTable(left, perf);

            left.Blank();
            RenderDlrTable(left, perf);

            left.Blank();
            RenderIntervalTable(left, perf);

            left.Blank();
            left.Header("Memory");
            RenderMemoryTable(left, perf);

            // Column 2: processing + GC.
            center.TitleHeader(FourCol, "Process ms/click", "Last", "Avg", "Max");
            RenderTotalRow(center, perf.ProcessingTotal, ms => ClickTargetColor(ms, perf.ClickTargetIntervalMs), "F1", "F1", "F1");
            RenderProcessingTable(center, perf);

            center.Blank();
            RenderGcTable(center, perf);
        }

        // Per-feature DLR ms/f table; row order mirrors the GC table.
        private static void RenderDlrTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double fps = perf.Fps.Current;
            MemoryMetricsSnapshot m = perf.Memory;
            if (fps <= 0 || m.DlrSections == null)
                return;

            b.TitleHeader(FourCol, "DLR ms/f", "Last", "Avg", "Max");
            b.TotalRow3(FourCol, "Total",
                FrameColor(m.DlrReadsMsLastPerSec / fps), FrameColor(m.DlrReadsMsAvgPerSec / fps), FrameColor(m.DlrReadsMsMaxPerSec / fps),
                $"{m.DlrReadsMsLastPerSec / fps:F2}",
                $"{m.DlrReadsMsAvgPerSec / fps:F2}",
                $"{m.DlrReadsMsMaxPerSec / fps:F2}");
            foreach ((string label, ProcessingSection section) in PerformanceTableRows.Dlr)
                DlrRow(b, label, m.DlrSections[(int)section], fps);
        }

        private static void DlrRow(TextBlock b, string label, DlrSectionSnapshot s, double fps)
        {
            if (s.ReadsMaxPerSec <= 0 && s.ReadsAvgPerSec <= 0)
            {
                b.Row(FourCol, Color.LightGreen, label, "-", "-", "-");
                return;
            }
            b.Row3(FourCol, label,
                FrameColor(s.MsLastPerSec / fps), FrameColor(s.MsAvgPerSec / fps), FrameColor(s.MsMaxPerSec / fps),
                $"{s.MsLastPerSec / fps:F2}",
                $"{s.MsAvgPerSec / fps:F2}",
                $"{s.MsMaxPerSec / fps:F2}");
        }

        private static void RenderRenderTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            foreach ((string label, Func<PerformanceMetricsSnapshot, TimingMetricsSnapshot> get) in PerformanceTableRows.Render)
                TimingRow(b, label, get(perf));
        }

        private static void TimingRow(TextBlock b, string label, TimingMetricsSnapshot s)
        {
            b.Row3(FourCol, label,
                FrameColor(s.LastMs), FrameColor(s.AverageMs), FrameColor(s.MaxMs),
                $"{s.LastMs:F2}", $"{s.AverageMs:F2}", $"{s.MaxMs:F2}");
        }

        private static void RenderFrameTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double fps = perf.Fps.Current;
            double clickScale = perf.ClickCoroutine.PerFrameScale(fps);
            foreach ((string label, Func<PerformanceMetricsSnapshot, TimingMetricsSnapshot> get, bool isSub) in PerformanceTableRows.Coroutine)
            {
                TimingMetricsSnapshot s = get(perf);
                if (isSub)
                {
                    if (clickScale > 0) FrameSubRow(b, $"  {label}", s, clickScale);
                }
                else FrameRow(b, label, s, fps);
            }
        }

        private static void FrameSubRow(TextBlock b, string label, TimingMetricsSnapshot s, double scale)
        {
            b.SubRow3(FourCol, label,
                scale > 0 ? FrameColor(s.LastMs * scale) : Color.LightGreen,
                scale > 0 ? FrameColor(s.AverageMs * scale) : Color.LightGreen,
                scale > 0 ? FrameColor(s.MaxMs * scale) : Color.LightGreen,
                scale > 0 ? $"{s.LastMs * scale:F2}" : "-",
                scale > 0 ? $"{s.AverageMs * scale:F2}" : "-",
                scale > 0 ? $"{s.MaxMs * scale:F2}" : "-");
        }

        private static void FrameRow(TextBlock b, string label, TimingMetricsSnapshot s, double fps)
        {
            double scale = s.PerFrameScale(fps);
            b.Row3(FourCol, label,
                scale > 0 ? FrameColor(s.LastMs * scale) : Color.LightGreen,
                scale > 0 ? FrameColor(s.AverageMs * scale) : Color.LightGreen,
                scale > 0 ? FrameColor(s.MaxMs * scale) : Color.LightGreen,
                scale > 0 ? $"{s.LastMs * scale:F2}" : "-",
                scale > 0 ? $"{s.AverageMs * scale:F2}" : "-",
                scale > 0 ? $"{s.MaxMs * scale:F2}" : "-");
        }

        private static void RenderIntervalTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            b.TitleHeader(FourCol, "Interval ms", "Last", "Avg", "Max");
            foreach ((string label, IntervalKind kind, Func<PerformanceMetricsSnapshot, double> expected) in PerformanceTableRows.Interval)
                IntervalRow(b, label, perf, kind, expected(perf));
        }

        private static void IntervalRow(TextBlock b, string label, PerformanceMetricsSnapshot perf, IntervalKind kind, double expectedMs)
        {
            if (perf.Intervals == null || !perf.Intervals.TryGetValue(kind, out IntervalTimingSnapshot s) || s.SampleCount <= 0)
            {
                b.Row(FourCol, Color.LightGreen, label, "-", "-", "-");
                return;
            }
            b.Row3(FourCol, label,
                IntervalColor(s.LastMs, expectedMs), IntervalColor(s.AvgMs, expectedMs), IntervalColor(s.MaxMs, expectedMs),
                $"{s.LastMs:F0}", $"{s.AvgMs:F0}", $"{s.MaxMs:F0}");
        }

        // Cadence-relative coloring: up to 25% over expected is on-cadence (green, 200->250), 25-49% is delayed (yellow, 251->299), 50%+ is red (300+).
        private static Color IntervalColor(double ms, double expectedMs)
            => expectedMs <= 0 || ms <= expectedMs * 1.25 ? Color.LightGreen
            : ms < expectedMs * 1.50 ? Color.Yellow
            : Color.Red;

        private static void RenderTotalRow(TextBlock b, TimingMetricsSnapshot s, Func<double, Color> color, string fLast, string fAvg, string fMax)
        {
            b.TotalRow3(FourCol, "Total",
                color(s.LastMs), color(s.AverageMs), color(s.MaxMs),
                s.LastMs.ToString(fLast), s.AverageMs.ToString(fAvg), s.MaxMs.ToString(fMax));
        }

        private static void RenderProcessingTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double targetMs = perf.ClickTargetIntervalMs;
            foreach ((string label, ProcessingSection section, PerfBreakdownKind breakdown) in PerformanceTableRows.Processing)
            {
                if (breakdown == PerfBreakdownKind.Click)
                {
                    RenderClickProcessingRows(b, perf);
                    continue;
                }
                RunRow(b, label, perf.GetProcessingSection(section), targetMs);
                if (breakdown == PerfBreakdownKind.Generic)
                    RenderBreakdownTiming(b, perf, section, targetMs);
            }
        }

        private static void RunRow(TextBlock b, string label, TimingMetricsSnapshot s, double targetMs)
        {
            b.Row3(FourCol, label,
                ClickTargetColor(s.LastMs, targetMs),
                ClickTargetColor(s.AverageMs, targetMs),
                ClickTargetColor(s.MaxMs, targetMs),
                $"{s.LastMs:F1}", $"{s.AverageMs:F1}", $"{s.MaxMs:F1}");
        }

        private static void RenderBreakdownTiming(TextBlock b, PerformanceMetricsSnapshot perf, ProcessingSection section, double targetMs)
        {
            if (perf.Breakdowns == null || !perf.Breakdowns.TryGetValue(section, out BreakdownStats stats) || stats.SampleCount == 0)
                return;
            foreach (BreakdownStageSnapshot stage in stats.Stages)
                StageTimingRow(b, stage.Name, stage.Time, targetMs);
        }

        private static void RenderClickProcessingRows(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double targetMs = perf.ClickTargetIntervalMs;
            TimingMetricsSnapshot click = perf.GetProcessingSection(ProcessingSection.Click);
            b.Row3(FourCol, "Click",
                ClickTargetColor(click.LastMs, targetMs),
                ClickTargetColor(click.AverageMs, targetMs),
                ClickTargetColor(click.MaxMs, targetMs),
                $"{click.LastMs:F1}", $"{click.AverageMs:F1}", $"{click.MaxMs:F1}");

            ClickAllocationStats alloc = perf.ClickAllocation;
            if (alloc.SampleCount > 0)
            {
                StageTimingRow(b, "Context", alloc.ContextTime, targetMs);
                StageTimingRow(b, "Acquire", alloc.AcquireTime, targetMs);
                StageTimingRow(b, "Rank", alloc.RankTime, targetMs);
                StageTimingRow(b, "Execute", alloc.ExecuteTime, targetMs);
                StageTimingRow(b, "Post", alloc.PostTime, targetMs);
                StageTimingRow(b, "Altar", alloc.AltarTime, targetMs);
                StageTimingRow(b, "Other", alloc.OtherTime, targetMs);
            }

            RenderClickFrequencyTargetRows(b, perf);
        }

        private static void RenderClickFrequencyTargetRows(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double targetMs = perf.ClickTargetIntervalMs;
            if (targetMs <= 0)
                return;

            b.Row(FourCol, Color.Yellow, "Click Frequency", $"{targetMs:F0}");
            ClickFrequencyRow(b, "Processing", perf.GetProcessingSection(ProcessingSection.Click), targetMs);
            ClickFrequencyRow(b, "Sleep", perf.ClickSleepTiming, targetMs);
            ClickFrequencyRow(b, "Total", perf.ClickCoroutine, targetMs);
        }

        private static void ClickFrequencyRow(TextBlock b, string label, TimingMetricsSnapshot s, double targetMs)
        {
            b.SubRow3(FourCol, label,
                ClickTargetColor(s.LastMs, targetMs),
                ClickTargetColor(s.AverageMs, targetMs),
                ClickTargetColor(s.MaxMs, targetMs),
                $"{s.LastMs:F1}", $"{s.AverageMs:F1}", $"{s.MaxMs:F1}");
        }

        // ms/click coloring relative to the click frequency target: green within the target, yellow up to +25% over it, red beyond that.
        private static Color ClickTargetColor(double msPerClick, double targetMs)
            => targetMs <= 0 ? FrameColor(msPerClick)
                : msPerClick <= targetMs ? Color.LightGreen
                : msPerClick <= targetMs * 1.25 ? Color.Yellow
                : Color.Red;

        private static void StageTimingRow(TextBlock b, string label, TimingStageSnapshot s, double targetMs)
        {
            b.SubRow3(FourCol, label,
                ClickTargetColor(s.LastMs, targetMs),
                ClickTargetColor(s.AvgMs, targetMs),
                ClickTargetColor(s.MaxMs, targetMs),
                $"{s.LastMs:F1}", $"{s.AvgMs:F1}", $"{s.MaxMs:F1}");
        }

        private static void RenderGcTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            MemoryMetricsSnapshot m = perf.Memory;
            if (m.GcSections == null)
                return;

            double totalLast = 0, totalAvg = 0, totalMax = 0;
            for (int s = 1; s < m.GcSections.Count; s++)
            {
                totalLast += m.GcSections[s].BytesLastPerSec;
                totalAvg += m.GcSections[s].BytesAvgPerSec;
                totalMax += m.GcSections[s].BytesMaxPerSec;
            }

            b.TitleHeader(FourCol, "GC byte/s", "Last", "Avg", "Max");
            b.TotalRow3(FourCol, "Total",
                GcTotalColor(totalLast), GcTotalColor(totalAvg), GcTotalColor(totalMax),
                ImGuiDebugOverlay.FormatBytes(totalLast), ImGuiDebugOverlay.FormatBytes(totalAvg), ImGuiDebugOverlay.FormatBytes(totalMax));
            foreach ((string label, ProcessingSection section, PerfBreakdownKind breakdown) in PerformanceTableRows.Gc)
            {
                GcRow(b, label, m.GcSections[(int)section]);
                switch (breakdown)
                {
                    case PerfBreakdownKind.Generic: RenderBreakdown(b, perf, section); break;
                    case PerfBreakdownKind.Click: RenderClickBreakdown(b, perf); break;
                    case PerfBreakdownKind.LabelScan: RenderLabelScanBreakdown(b, perf); break;
                }
            }
        }

        private static void RenderBreakdown(TextBlock b, PerformanceMetricsSnapshot perf, ProcessingSection section)
        {
            if (perf.Breakdowns == null || !perf.Breakdowns.TryGetValue(section, out BreakdownStats stats) || stats.SampleCount == 0)
                return;
            double periodMs = perf.GetAllocationSection(section).AvgPeriodMs;
            foreach (BreakdownStageSnapshot stage in stats.Stages)
                StageRow(b, stage.Name, stage.Allocation, periodMs);
        }

        private static void RenderLabelScanBreakdown(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            LabelScanAllocationStats s = perf.LabelScanAllocation;
            if (s.SampleCount == 0)
                return;
            double periodMs = perf.GetAllocationSection(ProcessingSection.Label).AvgPeriodMs;
            StageRow(b, "ListRead", s.ListRead, periodMs);
            StageRow(b, "ListAlloc", s.ListAlloc, periodMs);
            StageRow(b, "Validity", s.Validity, periodMs);
            StageRow(b, "Sort", s.Sort, periodMs);
        }

        private static void RenderClickBreakdown(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            ClickAllocationStats s = perf.ClickAllocation;
            if (s.SampleCount == 0)
                return;
            double periodMs = perf.GetAllocationSection(ProcessingSection.Click).AvgPeriodMs;
            StageRow(b, "Context", s.Context, periodMs);
            StageRow(b, "Acquire", s.Acquire, periodMs);
            StageRow(b, "Rank", s.Rank, periodMs);
            StageRow(b, "Execute", s.Execute, periodMs);
            StageRow(b, "Post", s.Post, periodMs);
            StageRow(b, "Other", s.Other, periodMs);
        }

        // One breakdown stage as byte/s Last/Avg/Max (per-run bytes normalized by the parent period, max from the live-window peak). Stages that never allocated any bytes are skipped.
        private static void StageRow(TextBlock b, string label, AllocationStageSnapshot s, double periodMs)
        {
            double lastPerSecond = periodMs > 0 ? s.LastBytesPerRun * 1000.0 / periodMs : 0;
            double avgPerSecond = periodMs > 0 ? s.AvgBytesPerRun * 1000.0 / periodMs : 0;
            double maxPerSecond = s.MaxAllocPerSecond;
            if (s.MaxBytesPerRun <= 0)
                return;
            b.SubRow3(FourCol, label,
                GcColor(lastPerSecond), GcColor(avgPerSecond), GcColor(maxPerSecond),
                ImGuiDebugOverlay.FormatBytes(lastPerSecond),
                ImGuiDebugOverlay.FormatBytes(avgPerSecond),
                ImGuiDebugOverlay.FormatBytes(maxPerSecond));
        }

        private static void GcRow(TextBlock b, string label, GcSectionSnapshot s)
        {
            if (s.BytesMaxPerSec <= 0 && s.BytesAvgPerSec <= 0)
            {
                b.Row(FourCol, Color.LightGreen, label, "-", "-", "-");
                return;
            }
            b.Row3(FourCol, label,
                GcColor(s.BytesLastPerSec), GcColor(s.BytesAvgPerSec), GcColor(s.BytesMaxPerSec),
                ImGuiDebugOverlay.FormatBytes(s.BytesLastPerSec),
                ImGuiDebugOverlay.FormatBytes(s.BytesAvgPerSec),
                ImGuiDebugOverlay.FormatBytes(s.BytesMaxPerSec));
        }

        private static void RenderMemoryTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            MemoryMetricsSnapshot m = perf.Memory;
            b.Row(TwoCol, SizeColor(m.ProcessWorkingSetMb), "Process", ImGuiDebugOverlay.FormatMemoryMb(m.ProcessWorkingSetMb));
            b.Row(TwoCol, SizeColor(m.ManagedHeapMb), "Managed", ImGuiDebugOverlay.FormatMemoryMb(m.ManagedHeapMb));
            b.Row(TwoCol, FragmentationColor(m.FragmentedMb), "Frag", ImGuiDebugOverlay.FormatMemoryMb(m.FragmentedMb));
            b.Row(TwoCol, GcPauseColor(m.GcPauseMaxMs),
                "GC Pause",
                $"{m.GcPauseLastMs:F0}/{m.GcPauseAvgMs:F0}/{m.GcPauseMaxMs:F0} ms");
        }

        // Per-value ms/f coloring: each Last/Avg/Max column is colored by its OWN value so a single bad spike shows red even when the average looks healthy. <=3ms green, <=6ms yellow, >6ms red.
        private static Color FrameColor(double ms)
            => ms <= 3.0 ? Color.LightGreen : ms <= 6.0 ? Color.Yellow : Color.Red;

        // Per-feature allocation rate: <=10MB/s is healthy, <=25MB/s elevated but tolerable, above that one feature is eating the whole plugin's allocation budget (~50MB/s total across all).
        private static Color GcColor(double allocPerSecond)
        {
            double mb = 1024.0 * 1024.0;
            return allocPerSecond > 25 * mb ? Color.Red : allocPerSecond > 10 * mb ? Color.Yellow : Color.LightGreen;
        }

        // Whole-plugin allocation rate: <=25MB/s healthy, <=50MB/s tolerable, above that too much.
        private static Color GcTotalColor(double allocPerSecond)
        {
            double mb = 1024.0 * 1024.0;
            return allocPerSecond > 50 * mb ? Color.Red : allocPerSecond > 25 * mb ? Color.Yellow : Color.LightGreen;
        }

        private static Color SizeColor(double mb)
            => mb > 2048 ? Color.Red : mb > 1228.8 ? Color.Yellow : Color.LightGreen;

        private static Color FragmentationColor(double mb)
            => mb > 400 ? Color.Red : mb > 100 ? Color.Yellow : Color.LightGreen;

        // Blocking GC pause coloring: >100ms pauses hitch all threads; above ~16ms (a frame) is a warning.
        private static Color GcPauseColor(double maxPauseMs)
            => maxPauseMs > 100 ? Color.Red : maxPauseMs > 16 ? Color.Yellow : Color.LightGreen;

        // Renders one aligned row of text: the label at the block's base X and each value at a fixed per-column X, so the proportional game font does not break the table alignment.
        private sealed class TextBlock(DeferredDrawQueue queue, float baseX, float startY, float lineHeight)
        {
            private readonly DeferredDrawQueue _queue = queue;
            private readonly float _baseX = baseX;
            private readonly float _lineHeight = lineHeight;
            private float _y = startY;

            public void Header(string text)
            {
                _queue.EnqueueText(text, new Vector2(_baseX, _y), HeaderColor, 14, shadow: true);
                _y += _lineHeight;
            }

            // Table title (orange) with the Last/Avg/Max column headers on the SAME line, matching the debug-box header row.
            public void TitleHeader(float[] colX, string title, params string[] columnHeaders)
            {
                _queue.EnqueueText(title, new Vector2(_baseX, _y), HeaderColor, 14, shadow: true);
                for (int i = 0; i < columnHeaders.Length; i++)
                    _queue.EnqueueText(columnHeaders[i], new Vector2(_baseX + colX[i], _y), LabelColor, 14, shadow: true);
                _y += _lineHeight;
            }

            public void Blank()
                => _y += _lineHeight * 0.5f;

            public void Row(float[] colX, Color color, string label, params string[] values)
            {
                _queue.EnqueueText(label, new Vector2(_baseX, _y), LabelColor, 14, shadow: true);
                for (int i = 0; i < values.Length; i++)
                    _queue.EnqueueText(values[i], new Vector2(_baseX + colX[i], _y), color, 14, shadow: true);
                _y += _lineHeight;
            }

            // Aligned row with a distinct color per value column (ms/f and GC tables color each of the Last/Avg/Max columns by its own value).
            public void Row3(float[] colX, string label, Color c1, Color c2, Color c3, string v1, string v2, string v3)
            {
                _queue.EnqueueText(label, new Vector2(_baseX, _y), LabelColor, 14, shadow: true);
                _queue.EnqueueText(v1, new Vector2(_baseX + colX[0], _y), c1, 14, shadow: true);
                _queue.EnqueueText(v2, new Vector2(_baseX + colX[1], _y), c2, 14, shadow: true);
                _queue.EnqueueText(v3, new Vector2(_baseX + colX[2], _y), c3, 14, shadow: true);
                _y += _lineHeight;
            }

            // Aligned Total row with a yellow label so the table-wide total stands out from the child rows.
            public void TotalRow3(float[] colX, string label, Color c1, Color c2, Color c3, string v1, string v2, string v3)
            {
                _queue.EnqueueText(label, new Vector2(_baseX, _y), Color.Yellow, 14, shadow: true);
                _queue.EnqueueText(v1, new Vector2(_baseX + colX[0], _y), c1, 14, shadow: true);
                _queue.EnqueueText(v2, new Vector2(_baseX + colX[1], _y), c2, 14, shadow: true);
                _queue.EnqueueText(v3, new Vector2(_baseX + colX[2], _y), c3, 14, shadow: true);
                _y += _lineHeight;
            }

            // Indented three-value variant with a distinct color per value (GC stage breakdowns).
            public void SubRow3(float[] colX, string label, Color c1, Color c2, Color c3, string v1, string v2, string v3)
            {
                _queue.EnqueueText(label, new Vector2(_baseX + 10f, _y), LabelColor, 14, shadow: true);
                _queue.EnqueueText(v1, new Vector2(_baseX + colX[0], _y), c1, 14, shadow: true);
                _queue.EnqueueText(v2, new Vector2(_baseX + colX[1], _y), c2, 14, shadow: true);
                _queue.EnqueueText(v3, new Vector2(_baseX + colX[2], _y), c3, 14, shadow: true);
                _y += _lineHeight;
            }
        }
    }
}
