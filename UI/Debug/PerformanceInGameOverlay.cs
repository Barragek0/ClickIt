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
            PerformanceMetricsSnapshot perf = _getSnapshot();
            if (perf.Fps.Max <= 0)
                return;

            const float lineHeight = 15f;
            const float originX = 5f;
            const float originY = 100f;
            const float colWidth = 400f;
            TextBlock left = new(ctx.TextQueue, originX, originY, lineHeight);
            TextBlock center = new(ctx.TextQueue, originX + colWidth, originY, lineHeight);

            // Column 1: render + coroutine + DLR + click target + memory.
            left.ColumnHeader(FourCol, "Last", "Avg", "Max");
            TimingMetricsSnapshot renderTotal = perf.RenderTableTotal;
            left.TitleRow3(FourCol, "Render ms/f",
                FrameColor(renderTotal.LastMs), FrameColor(renderTotal.AverageMs), FrameColor(renderTotal.MaxMs),
                $"{renderTotal.LastMs:F1}", $"{renderTotal.AverageMs:F2}", $"{renderTotal.MaxMs:F1}");
            RenderRenderTable(left, perf);

            left.Blank();
            left.ColumnHeader(FourCol, "Last", "Avg", "Max");
            TimingMetricsSnapshot frameTotal = perf.CoroutinesTotalPerFrameSnapshot;
            left.TitleRow3(FourCol, "Coroutine ms/f",
                FrameColor(frameTotal.LastMs), FrameColor(frameTotal.AverageMs), FrameColor(frameTotal.MaxMs),
                $"{frameTotal.LastMs:F2}", $"{frameTotal.AverageMs:F2}", $"{frameTotal.MaxMs:F2}");
            RenderFrameTable(left, perf);

            left.Blank();
            RenderDlrTable(left, perf);

            left.Blank();
            left.Header("Memory");
            RenderMemoryTable(left, perf);

            // Column 2: processing + GC.
            center.ColumnHeader(FourCol, "Last", "Avg", "Max");
            TimingMetricsSnapshot procRunTotal = perf.ProcessingTotal;
            double clickTarget = perf.ClickTargetIntervalMs;
            center.TitleRow3(FourCol, "Process ms/click",
                ClickTargetColor(procRunTotal.LastMs, clickTarget), ClickTargetColor(procRunTotal.AverageMs, clickTarget), ClickTargetColor(procRunTotal.MaxMs, clickTarget),
                $"{procRunTotal.LastMs:F1}", $"{procRunTotal.AverageMs:F1}", $"{procRunTotal.MaxMs:F1}");
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

            b.ColumnHeader(FourCol, "Last", "Avg", "Max");
            b.TitleRow3(FourCol, "DLR ms/f",
                FrameColor(m.DlrReadsMsLastPerSec / fps), FrameColor(m.DlrReadsMsAvgPerSec / fps), FrameColor(m.DlrReadsMsMaxPerSec / fps),
                $"{m.DlrReadsMsLastPerSec / fps:F2}",
                $"{m.DlrReadsMsAvgPerSec / fps:F2}",
                $"{m.DlrReadsMsMaxPerSec / fps:F2}");
            DlrRow(b, "Altar", m.DlrSections[(int)ProcessingSection.Altar], fps);
            DlrRow(b, "Area.Blocked", m.DlrSections[(int)ProcessingSection.AreaBlockedUi], fps);
            DlrRow(b, "Blight", m.DlrSections[(int)ProcessingSection.Blight], fps);
            DlrRow(b, "Click", m.DlrSections[(int)ProcessingSection.Click], fps);
            DlrRow(b, "Dump", m.DlrSections[(int)ProcessingSection.GameStateDump], fps);
            DlrRow(b, "Flare", m.DlrSections[(int)ProcessingSection.Flare], fps);
            DlrRow(b, "Harvest", m.DlrSections[(int)ProcessingSection.Harvest], fps);
            DlrRow(b, "Label Scan", m.DlrSections[(int)ProcessingSection.Label], fps);
            DlrRow(b, "Manual Hover", m.DlrSections[(int)ProcessingSection.ManualUiHover], fps);
            DlrRow(b, "Pathfinding", m.DlrSections[(int)ProcessingSection.Pathfinding], fps);
            DlrRow(b, "Strongbox", m.DlrSections[(int)ProcessingSection.Strongbox], fps);
            DlrRow(b, "Ultimatum", m.DlrSections[(int)ProcessingSection.Ultimatum], fps);
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
            TimingRow(b, "Altar", perf.GetRenderSection(RenderSection.AltarOverlay));
            TimingRow(b, "Blight", perf.GetRenderSection(RenderSection.BlightOverlay));
            TimingRow(b, "Click.Hotkey", perf.GetRenderSection(RenderSection.ClickHotkeyToggle));
            TimingRow(b, "Debug", perf.GetRenderSection(RenderSection.DebugOverlay));
            TimingRow(b, "Flush.Frame", perf.GetRenderSection(RenderSection.FrameFlush));
            TimingRow(b, "Flush.Text", perf.GetRenderSection(RenderSection.TextFlush));
            TimingRow(b, "Harvest", perf.GetRenderSection(RenderSection.HarvestOverlay));
            TimingRow(b, "Inv.Full", perf.GetRenderSection(RenderSection.InventoryFullWarning));
            TimingRow(b, "Lazy", perf.GetRenderSection(RenderSection.LazyMode));
            TimingRow(b, "Pathfinding", perf.GetRenderSection(RenderSection.PathfindingOverlay));
            TimingRow(b, "Perf.Overlay", perf.GetRenderSection(RenderSection.PerformanceOverlay));
            TimingRow(b, "Strongbox", perf.GetRenderSection(RenderSection.StrongboxOverlay));
            TimingRow(b, "UI.Rect", perf.GetRenderSection(RenderSection.UiRegionRectangle));
            TimingRow(b, "Ultimatum", perf.GetRenderSection(RenderSection.UltimatumOverlay));
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
            FrameRow(b, "Altar", perf.AltarCoroutine, fps);
            FrameRow(b, "Blight", perf.BlightCoroutine, fps);
            FrameRow(b, "Click", perf.ClickCoroutine, fps);
            FrameRow(b, "Flare", perf.FlareCoroutine, fps);
            FrameRow(b, "Label Overlay", perf.LabelOverlayCoroutine, fps);
            FrameRow(b, "Ultimatum", perf.UltimatumCoroutine, fps);
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

        private static void RenderProcessingTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double targetMs = perf.ClickTargetIntervalMs;
            RunRow(b, "Altar", perf.GetProcessingSection(ProcessingSection.Altar), targetMs);
            RenderBreakdownTiming(b, perf, ProcessingSection.Altar, targetMs);
            RunRow(b, "Area.Blocked", perf.GetProcessingSection(ProcessingSection.AreaBlockedUi), targetMs);
            RunRow(b, "Blight", perf.GetProcessingSection(ProcessingSection.Blight), targetMs);
            RenderBreakdownTiming(b, perf, ProcessingSection.Blight, targetMs);
            RenderClickProcessingRows(b, perf);
            RunRow(b, "Dump", perf.GetProcessingSection(ProcessingSection.GameStateDump), targetMs);
            RunRow(b, "Flare", perf.GetProcessingSection(ProcessingSection.Flare), targetMs);
            RenderBreakdownTiming(b, perf, ProcessingSection.Flare, targetMs);
            RunRow(b, "Harvest", perf.GetProcessingSection(ProcessingSection.Harvest), targetMs);
            RunRow(b, "Label Scan", perf.GetProcessingSection(ProcessingSection.Label), targetMs);
            RunRow(b, "Manual Hover", perf.GetProcessingSection(ProcessingSection.ManualUiHover), targetMs);
            RunRow(b, "Pathfinding", perf.GetProcessingSection(ProcessingSection.Pathfinding), targetMs);
            RenderBreakdownTiming(b, perf, ProcessingSection.Pathfinding, targetMs);
            RunRow(b, "Strongbox", perf.GetProcessingSection(ProcessingSection.Strongbox), targetMs);
            RenderBreakdownTiming(b, perf, ProcessingSection.Strongbox, targetMs);
            RunRow(b, "Ultimatum", perf.GetProcessingSection(ProcessingSection.Ultimatum), targetMs);
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
            if (click.SampleCount > 0)
            {
                b.Row3(FourCol, "Click",
                    ClickTargetColor(click.LastMs, targetMs),
                    ClickTargetColor(click.AverageMs, targetMs),
                    ClickTargetColor(click.MaxMs, targetMs),
                    $"{click.LastMs:F1}", $"{click.AverageMs:F1}", $"{click.MaxMs:F1}");
            }

            ClickAllocationStats alloc = perf.ClickAllocation;
            if (alloc.SampleCount > 0)
            {
                StageTimingRow(b, "Context", alloc.ContextTime, targetMs);
                StageTimingRow(b, "Acquire", alloc.AcquireTime, targetMs);
                StageTimingRow(b, "Rank", alloc.RankTime, targetMs);
                StageTimingRow(b, "Execute", alloc.ExecuteTime, targetMs);
                StageTimingRow(b, "Post", alloc.PostTime, targetMs);
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

            b.ColumnHeader(FourCol, "Last", "Avg", "Max");
            b.TitleRow3(FourCol, "GC byte/s",
                GcTotalColor(totalLast), GcTotalColor(totalAvg), GcTotalColor(totalMax),
                FormatBytes(totalLast), FormatBytes(totalAvg), FormatBytes(totalMax));
            GcRow(b, "Altar", m.GcSections[(int)ProcessingSection.Altar]);
            RenderBreakdown(b, perf, ProcessingSection.Altar);
            GcRow(b, "Area.Blocked", m.GcSections[(int)ProcessingSection.AreaBlockedUi]);
            GcRow(b, "Blight", m.GcSections[(int)ProcessingSection.Blight]);
            RenderBreakdown(b, perf, ProcessingSection.Blight);
            GcRow(b, "Click", m.GcSections[(int)ProcessingSection.Click]);
            RenderClickBreakdown(b, perf);
            GcRow(b, "Dump", m.GcSections[(int)ProcessingSection.GameStateDump]);
            GcRow(b, "Flare", m.GcSections[(int)ProcessingSection.Flare]);
            RenderBreakdown(b, perf, ProcessingSection.Flare);
            GcRow(b, "Harvest", m.GcSections[(int)ProcessingSection.Harvest]);
            GcRow(b, "Label Scan", m.GcSections[(int)ProcessingSection.Label]);
            RenderLabelScanBreakdown(b, perf);
            GcRow(b, "Manual Hover", m.GcSections[(int)ProcessingSection.ManualUiHover]);
            GcRow(b, "Pathfinding", m.GcSections[(int)ProcessingSection.Pathfinding]);
            RenderBreakdown(b, perf, ProcessingSection.Pathfinding);
            GcRow(b, "Strongbox", m.GcSections[(int)ProcessingSection.Strongbox]);
            RenderBreakdown(b, perf, ProcessingSection.Strongbox);
            GcRow(b, "Ultimatum", m.GcSections[(int)ProcessingSection.Ultimatum]);
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
                FormatBytes(lastPerSecond),
                FormatBytes(avgPerSecond),
                FormatBytes(maxPerSecond));
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
                FormatBytes(s.BytesLastPerSec),
                FormatBytes(s.BytesAvgPerSec),
                FormatBytes(s.BytesMaxPerSec));
        }

        private static void RenderMemoryTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            MemoryMetricsSnapshot m = perf.Memory;
            b.Row(TwoCol, SizeColor(m.ProcessWorkingSetMb), "Process", FormatMemoryMb(m.ProcessWorkingSetMb));
            b.Row(TwoCol, SizeColor(m.ManagedHeapMb), "Managed", FormatMemoryMb(m.ManagedHeapMb));
            b.Row(TwoCol, FragmentationColor(m.FragmentedMb), "Frag", FormatMemoryMb(m.FragmentedMb));
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

        private static string FormatBytes(double bytes)
        {
            if (bytes >= 1024.0 * 1024.0)
                return $"{bytes / (1024.0 * 1024.0):F0} MB";
            if (bytes >= 1024.0)
                return $"{bytes / 1024.0:F0} KB";
            return $"{bytes:F0} B";
        }

        private static string FormatMemoryMb(double mb)
            => mb >= 1024.0 ? $"{mb / 1024.0:F1} GB" : $"{mb:F0} MB";

        // Renders one aligned row of text: the label at the block's base X and each value at a fixed per-column X, so the proportional game font does not break the table alignment.
        private sealed class TextBlock(DeferredTextQueue queue, float baseX, float startY, float lineHeight)
        {
            private readonly DeferredTextQueue _queue = queue;
            private readonly float _baseX = baseX;
            private readonly float _lineHeight = lineHeight;
            private float _y = startY;

            public void Header(string text)
            {
                _queue.Enqueue(text, new Vector2(_baseX, _y), HeaderColor, 14, shadow: true);
                _y += _lineHeight;
            }

            // Column headers aligned to the VALUE columns (not the label column) so "Last" sits above the first value, "Avg" above the second, "Max" above the third.
            public void ColumnHeader(float[] colX, params string[] values)
            {
                for (int i = 0; i < values.Length; i++)
                    _queue.Enqueue(values[i], new Vector2(_baseX + colX[i], _y), LabelColor, 14, shadow: true);
                _y += _lineHeight;
            }

            // Table title (orange, label column) with the table-wide totals on the same row, each aligned above its value column.
            public void TitleRow(float[] colX, string title, Color valueColor, params string[] values)
            {
                _queue.Enqueue(title, new Vector2(_baseX, _y), HeaderColor, 14, shadow: true);
                for (int i = 0; i < values.Length; i++)
                    _queue.Enqueue(values[i], new Vector2(_baseX + colX[i], _y), valueColor, 14, shadow: true);
                _y += _lineHeight;
            }

            // Table title with a distinct color per value column, so the Last/Avg/Max cells of the ms/f and GC tables are each colored by their own value.
            public void TitleRow3(float[] colX, string title, Color c1, Color c2, Color c3, string v1, string v2, string v3)
            {
                _queue.Enqueue(title, new Vector2(_baseX, _y), HeaderColor, 14, shadow: true);
                _queue.Enqueue(v1, new Vector2(_baseX + colX[0], _y), c1, 14, shadow: true);
                _queue.Enqueue(v2, new Vector2(_baseX + colX[1], _y), c2, 14, shadow: true);
                _queue.Enqueue(v3, new Vector2(_baseX + colX[2], _y), c3, 14, shadow: true);
                _y += _lineHeight;
            }

            public void Blank()
                => _y += _lineHeight * 0.5f;

            public void Row(float[] colX, Color color, string label, params string[] values)
            {
                _queue.Enqueue(label, new Vector2(_baseX, _y), LabelColor, 14, shadow: true);
                for (int i = 0; i < values.Length; i++)
                    _queue.Enqueue(values[i], new Vector2(_baseX + colX[i], _y), color, 14, shadow: true);
                _y += _lineHeight;
            }

            // Aligned row with a distinct color per value column (ms/f and GC tables color each of the Last/Avg/Max columns by its own value).
            public void Row3(float[] colX, string label, Color c1, Color c2, Color c3, string v1, string v2, string v3)
            {
                _queue.Enqueue(label, new Vector2(_baseX, _y), LabelColor, 14, shadow: true);
                _queue.Enqueue(v1, new Vector2(_baseX + colX[0], _y), c1, 14, shadow: true);
                _queue.Enqueue(v2, new Vector2(_baseX + colX[1], _y), c2, 14, shadow: true);
                _queue.Enqueue(v3, new Vector2(_baseX + colX[2], _y), c3, 14, shadow: true);
                _y += _lineHeight;
            }

            // Indented sub-table row (indented label + fixed per-column values) for breakdowns nested under a table row, e.g. the per-stage label-scan allocation breakdown.
            public void SubRow(float[] colX, Color color, string label, params string[] values)
            {
                _queue.Enqueue(label, new Vector2(_baseX + 10f, _y), LabelColor, 14, shadow: true);
                for (int i = 0; i < values.Length; i++)
                    _queue.Enqueue(values[i], new Vector2(_baseX + colX[i], _y), color, 14, shadow: true);
                _y += _lineHeight;
            }

            // Indented three-value variant with a distinct color per value (GC stage breakdowns).
            public void SubRow3(float[] colX, string label, Color c1, Color c2, Color c3, string v1, string v2, string v3)
            {
                _queue.Enqueue(label, new Vector2(_baseX + 10f, _y), LabelColor, 14, shadow: true);
                _queue.Enqueue(v1, new Vector2(_baseX + colX[0], _y), c1, 14, shadow: true);
                _queue.Enqueue(v2, new Vector2(_baseX + colX[1], _y), c2, 14, shadow: true);
                _queue.Enqueue(v3, new Vector2(_baseX + colX[2], _y), c3, 14, shadow: true);
                _y += _lineHeight;
            }
        }
    }
}
