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

        // All three-value tables (render, CR ms/f, processing, GC) share the GC table's column
        // positions so every Last/Avg/Max value lines up vertically.
        private static readonly float[] FourCol = [140f, 225f, 310f];
        private static readonly float[] GcCol = [140f, 225f, 310f];
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
            const float rightColX = 400f;
            TextBlock left = new(ctx.TextQueue, originX, originY, lineHeight);
            TextBlock right = new(ctx.TextQueue, originX + rightColX, originY, lineHeight);

            // Left column: render + coroutine ms/f + GC.
            left.ColumnHeader(FourCol, "Last", "Avg", "Max");
            TimingMetricsSnapshot renderTotal = perf.RenderTableTotal;
            left.TitleRow(FourCol, "Render ms/f", FrameColor(renderTotal.AverageMs), $"{renderTotal.LastMs:F1}", $"{renderTotal.AverageMs:F2}", $"{renderTotal.MaxMs:F1}");
            RenderRenderTable(left, perf);

            left.Blank();
            left.ColumnHeader(FourCol, "Last", "Avg", "Max");
            TimingMetricsSnapshot frameTotal = perf.CoroutinesTotalPerFrameSnapshot;
            left.TitleRow(FourCol, "Coroutine ms/f", FrameColor(frameTotal.AverageMs), $"{frameTotal.LastMs:F2}", $"{frameTotal.AverageMs:F2}", $"{frameTotal.MaxMs:F2}");
            RenderFrameTable(left, perf);

            left.Blank();
            left.ColumnHeader(GcCol, "KB/f", "KB/s", "KB/s");
            (double gcLastKf, double gcTotalRate, _) = perf.GcTableTotalBytesPerFrame;
            left.TitleRow(GcCol, "GC", GcTotalColor(gcTotalRate), FormatBytes(gcLastKf), FormatAllocRate(gcTotalRate), FormatAllocRate(perf.GcTableTotalMaxBytesPerSecond));
            RenderGcTable(left, perf);

            // Right column: processing + memory + click frequency target.
            right.ColumnHeader(FourCol, "Last", "Avg", "Max");
            TimingMetricsSnapshot procFrameTotal = perf.ProcessingTotalPerFrameSnapshot;
            right.TitleRow(FourCol, "Process ms/f", FrameColor(procFrameTotal.AverageMs), $"{procFrameTotal.LastMs:F2}", $"{procFrameTotal.AverageMs:F2}", $"{procFrameTotal.MaxMs:F2}");
            RenderProcessingTable(right, perf);

            right.Blank();
            right.Header("Memory");
            RenderMemoryTable(right, perf);

            if (perf.ClickTargetIntervalMs > 0)
            {
                right.Blank();
                right.Header("Click Frequency Target");
                RenderClickFrequencyTarget(right, perf);
            }
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
            b.Row(FourCol, FrameColor(s.AverageMs), label, $"{s.LastMs:F2}", $"{s.AverageMs:F2}", $"{s.MaxMs:F2}");
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
            b.Row(FourCol, FrameColor(s.AverageMs * scale), label,
                scale > 0 ? $"{s.LastMs * scale:F2}" : "-",
                scale > 0 ? $"{s.AverageMs * scale:F2}" : "-",
                scale > 0 ? $"{s.MaxMs * scale:F2}" : "-");
        }

        private static void RenderProcessingTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double fps = perf.Fps.Current;
            FrameRow(b, "Altar", perf.GetProcessingSection(ProcessingSection.Altar), fps);
            FrameRow(b, "Area.Blocked", perf.GetProcessingSection(ProcessingSection.AreaBlockedUi), fps);
            FrameRow(b, "Blight", perf.GetProcessingSection(ProcessingSection.Blight), fps);
            FrameRow(b, "Click", perf.GetProcessingSection(ProcessingSection.Click), fps);
            FrameRow(b, "Flare", perf.GetProcessingSection(ProcessingSection.Flare), fps);
            FrameRow(b, "Harvest", perf.GetProcessingSection(ProcessingSection.Harvest), fps);
            FrameRow(b, "Label Scan", perf.GetProcessingSection(ProcessingSection.Label), fps);
            FrameRow(b, "Manual Hover", perf.GetProcessingSection(ProcessingSection.ManualUiHover), fps);
            FrameRow(b, "Pathfinding", perf.GetProcessingSection(ProcessingSection.Pathfinding), fps);
            FrameRow(b, "Strongbox", perf.GetProcessingSection(ProcessingSection.Strongbox), fps);
            FrameRow(b, "Ultimatum", perf.GetProcessingSection(ProcessingSection.Ultimatum), fps);
        }

        private static void RenderGcTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double fps = perf.Fps.Current;
            GcRow(b, "Altar", perf.GetAllocationSection(ProcessingSection.Altar), fps);
            GcRow(b, "Area.Blocked", perf.GetAllocationSection(ProcessingSection.AreaBlockedUi), fps);
            GcRow(b, "Blight", perf.GetAllocationSection(ProcessingSection.Blight), fps);
            GcRow(b, "Click", perf.GetAllocationSection(ProcessingSection.Click), fps);
            RenderClickBreakdown(b, perf);
            GcRow(b, "Flare", perf.GetAllocationSection(ProcessingSection.Flare), fps);
            GcRow(b, "Harvest", perf.GetAllocationSection(ProcessingSection.Harvest), fps);
            GcRow(b, "Label Scan", perf.GetAllocationSection(ProcessingSection.Label), fps);
            RenderLabelScanBreakdown(b, perf);
            GcRow(b, "Manual Hover", perf.GetAllocationSection(ProcessingSection.ManualUiHover), fps);
            GcRow(b, "Pathfinding", perf.GetAllocationSection(ProcessingSection.Pathfinding), fps);
            GcRow(b, "Strongbox", perf.GetAllocationSection(ProcessingSection.Strongbox), fps);
            GcRow(b, "Ultimatum", perf.GetAllocationSection(ProcessingSection.Ultimatum), fps);
        }

        private static void RenderLabelScanBreakdown(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            LabelScanAllocationStats s = perf.LabelScanAllocation;
            if (s.SampleCount == 0)
                return;
            double fps = perf.Fps.Current;
            double periodMs = perf.GetAllocationSection(ProcessingSection.Label).AvgPeriodMs;
            StageRow(b, "Validity", s.Validity, fps, periodMs);
            StageRow(b, "Sort", s.Sort, fps, periodMs);
        }

        private static void RenderClickBreakdown(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            ClickAllocationStats s = perf.ClickAllocation;
            if (s.SampleCount == 0)
                return;
            double fps = perf.Fps.Current;
            double periodMs = perf.GetAllocationSection(ProcessingSection.Click).AvgPeriodMs;
            StageRow(b, "Context", s.Context, fps, periodMs);
            StageRow(b, "Acquire", s.Acquire, fps, periodMs);
            StageRow(b, "Rank", s.Rank, fps, periodMs);
            StageRow(b, "Execute", s.Execute, fps, periodMs);
            StageRow(b, "Post", s.Post, fps, periodMs);
            StageRow(b, "Other", s.Other, fps, periodMs);
        }

        private static void StageRow(TextBlock b, string label, AllocationStageSnapshot s, double fps, double periodMs)
        {
            if (s.AvgBytesPerRun <= 0 && s.MaxBytesPerRun <= 0)
                return;
            double allocPerSecond = periodMs > 0 ? s.AvgBytesPerRun * 1000.0 / periodMs : 0;
            double maxPerSecond = periodMs > 0 ? s.MaxBytesPerRun * 1000.0 / periodMs : 0;
            Color rateColor = GcColor(allocPerSecond);
            Color maxColor = GcColor(maxPerSecond);
            b.SubRow3(GcCol, label, rateColor, rateColor, maxColor,
                fps > 0 ? FormatBytes(allocPerSecond / fps) : "-",
                FormatAllocRate(allocPerSecond),
                FormatAllocRate(maxPerSecond));
        }

        private static void GcRow(TextBlock b, string label, GcAllocationSnapshot s, double fps)
        {
            Color rateColor = GcColor(s.AllocPerSecond);
            Color maxColor = GcColor(s.MaxAllocPerSecond);
            b.Row3(GcCol, label, rateColor, rateColor, maxColor,
                s.SampleCount > 0 && fps > 0 ? FormatBytes(s.AllocPerSecond / fps) : "-",
                s.SampleCount > 0 ? FormatAllocRate(s.AllocPerSecond) : "-",
                s.SampleCount > 0 ? FormatAllocRate(s.MaxAllocPerSecond) : "-");
        }

        private static void RenderMemoryTable(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            MemoryMetricsSnapshot m = perf.Memory;
            b.Row(TwoCol, SizeColor(m.ProcessWorkingSetMb), "Process", FormatMemoryMb(m.ProcessWorkingSetMb));
            b.Row(TwoCol, SizeColor(m.ManagedHeapMb), "Managed", FormatMemoryMb(m.ManagedHeapMb));
            b.Row(TwoCol, FragmentationColor(m.FragmentedMb), "Frag", FormatMemoryMb(m.FragmentedMb));
        }

        private static void RenderClickFrequencyTarget(TextBlock b, PerformanceMetricsSnapshot perf)
        {
            double targetMs = perf.ClickTargetIntervalMs;
            double fullTickMs = perf.ClickCoroutine.AverageMs;
            if (fullTickMs <= 0) fullTickMs = perf.AverageSuccessfulClickTimingMs;
            double sleepMs = SystemMath.Min(perf.AverageClickSleepMs, fullTickMs);
            double processingMs = SystemMath.Max(0, fullTickMs - sleepMs);
            double observedMs = perf.AverageClickIntervalMs;

            double delayMs = SystemMath.Max(0, targetMs - fullTickMs);
            double modeledTotalMs = delayMs + fullTickMs;
            double observedTotalMs = observedMs > 0 ? observedMs : modeledTotalMs;
            double schedulerDeltaMs = observedTotalMs - modeledTotalMs;
            double deviation = observedTotalMs > 0
                ? (observedTotalMs - targetMs) / targetMs
                : 0;

            string targetStatus = deviation <= 0.10
                ? (observedMs > 0 ? "meeting target" : "estimating")
                : "not meeting target";

            b.Row(TwoCol, Color.Yellow, "Target", $"{targetMs:F0} ms");
            b.Row(TwoCol, processingMs > targetMs ? Color.Red : processingMs >= targetMs * 0.75 ? Color.Yellow : Color.LightGreen, "Processing", $"{processingMs:F0} ms");
            b.Row(TwoCol, sleepMs > 0 ? Color.Yellow : Color.LightGreen, "Sleep", $"{sleepMs:F0} ms");
            b.Row(TwoCol, deviation <= 0.05 ? Color.LightGreen : deviation <= 0.10 ? Color.Yellow : Color.Red, "Total (model)", $"{modeledTotalMs:F0} ms");
            b.Row(TwoCol, SystemMath.Abs(schedulerDeltaMs) <= 5 ? Color.LightGreen : SystemMath.Abs(schedulerDeltaMs) <= 20 ? Color.Yellow : Color.OrangeRed, "Scheduler", $"{schedulerDeltaMs:+0;-0;0} ms");
            b.Row(TwoCol, deviation <= 0.05 ? Color.LightGreen : deviation <= 0.10 ? Color.Yellow : Color.Red, "Observed", $"{observedTotalMs:F0} ms ({targetStatus})");
        }

        private static Color FrameColor(double avgMs)
            => avgMs <= 6.94 ? Color.LightGreen : avgMs <= 16.67 ? Color.Yellow : Color.Red;

        // Per-feature allocation rate: a single feature consuming the whole acceptable plugin budget
        // (~50MB/s total) is the red line; elevated but tolerable below that.
        private static Color GcColor(double allocPerSecond)
        {
            double mb = 1024.0 * 1024.0;
            return allocPerSecond >= 50 * mb ? Color.Red : allocPerSecond >= 20 * mb ? Color.Yellow : Color.LightGreen;
        }

        // Whole-plugin allocation rate: ~50MB/s across everything is acceptable in practice.
        private static Color GcTotalColor(double allocPerSecond)
        {
            double mb = 1024.0 * 1024.0;
            return allocPerSecond >= 100 * mb ? Color.Red : allocPerSecond >= 50 * mb ? Color.Yellow : Color.LightGreen;
        }

        private static Color SizeColor(double mb)
            => mb > 2048 ? Color.Red : mb > 1228.8 ? Color.Yellow : Color.LightGreen;

        private static Color FragmentationColor(double mb)
            => mb > 400 ? Color.Red : mb > 100 ? Color.Yellow : Color.LightGreen;

        private static string FormatBytes(double bytes)
        {
            if (bytes >= 1024.0 * 1024.0)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            if (bytes >= 1024.0)
                return $"{bytes / 1024.0:F0} KB";
            return $"{bytes:F0} B";
        }

        private static string FormatAllocRate(double bytesPerSecond)
            => $"{FormatBytes(bytesPerSecond)}/s";

        private static string FormatMemoryMb(double mb)
            => mb >= 1024.0 ? $"{mb / 1024.0:F1} GB" : $"{mb:F0} MB";

        // Renders one aligned row of text: the label at the block's base X and each value at a
        // fixed per-column X, so the proportional game font does not break the table alignment.
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

            // Column headers aligned to the VALUE columns (not the label column) so "Last" sits
            // above the first value, "Avg" above the second, "Max" above the third.
            public void ColumnHeader(float[] colX, params string[] values)
            {
                for (int i = 0; i < values.Length; i++)
                    _queue.Enqueue(values[i], new Vector2(_baseX + colX[i], _y), LabelColor, 14, shadow: true);
                _y += _lineHeight;
            }

            // Table title (orange, label column) with the table-wide totals on the same row, each
            // aligned above its value column.
            public void TitleRow(float[] colX, string title, Color valueColor, params string[] values)
            {
                _queue.Enqueue(title, new Vector2(_baseX, _y), HeaderColor, 14, shadow: true);
                for (int i = 0; i < values.Length; i++)
                    _queue.Enqueue(values[i], new Vector2(_baseX + colX[i], _y), valueColor, 14, shadow: true);
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

            // Three-value row with a distinct color per value (GC table: rate color for last/avg,
            // single-run spike color for max). Avoids a per-row color array allocation.
            public void Row3(float[] colX, string label, Color c1, Color c2, Color c3, string v1, string v2, string v3)
            {
                _queue.Enqueue(label, new Vector2(_baseX, _y), LabelColor, 14, shadow: true);
                _queue.Enqueue(v1, new Vector2(_baseX + colX[0], _y), c1, 14, shadow: true);
                _queue.Enqueue(v2, new Vector2(_baseX + colX[1], _y), c2, 14, shadow: true);
                _queue.Enqueue(v3, new Vector2(_baseX + colX[2], _y), c3, 14, shadow: true);
                _y += _lineHeight;
            }

            // Indented sub-table row (indented label + fixed per-column values) for breakdowns
            // nested under a table row, e.g. the per-stage label-scan allocation breakdown.
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
