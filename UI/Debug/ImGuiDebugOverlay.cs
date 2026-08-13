using System.Globalization;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;

namespace ClickIt.UI.Debug;

internal sealed class ImGuiDebugOverlay(
    ClickItSettings settings,
    PerformanceMonitor? performanceMonitor = null,
    BlightService? blight = null,
    IDebugTelemetrySource? telemetrySource = null,
    HarvestService? harvest = null)
{
    private const string WindowTitle = "ClickIt Debug Info";
    private const float WindowMinWidth = 1240f;
    private const float WindowMinHeight = 400f;

    private static readonly NumVec4 CWarn = Vec4(Color.Yellow);
    private static readonly NumVec4 CError = Vec4(Color.Red);
    private static readonly NumVec4 CInfo = Vec4(Color.Cyan);
    private static readonly NumVec4 CMuted = Vec4(Color.LightGray);
    private static readonly NumVec4 CDim = Vec4(Color.DarkGray);
    private static readonly NumVec4 COrange = Vec4(Color.Orange);
    private static readonly NumVec4 CHeader = Vec4(Color.Orange);
    private static readonly NumVec4 CWhite = Vec4(Color.White);
    private static readonly NumVec4 CLime = Vec4(Color.Lime);
    private static readonly NumVec4 CGold = Vec4(Color.Gold);
    private static readonly NumVec4 CGreen = Vec4(Color.LightGreen);
    private static readonly NumVec4 CLightBlue = Vec4(Color.LightBlue);
    private static readonly NumVec4 COrangeRed = Vec4(Color.OrangeRed);
    private static readonly NumVec4 CBuildAction = Vec4(new Color(0, 190, 0));     // deep green — plan BUILD
    private static readonly NumVec4 CUpgradeAction = Vec4(new Color(255, 80, 80)); // red — plan UPGRADE
    private static readonly NumVec4 CSpecialAction = Vec4(new Color(0, 200, 200)); // cyan — plan 3->4 SPECIALIZATION

    private readonly ClickItSettings _settings = settings;
    private readonly PerformanceMonitor? _performanceMonitor = performanceMonitor;
    private readonly BlightService? _blight = blight;
    private readonly IDebugTelemetrySource? _telemetrySource = telemetrySource;
    private readonly HarvestService? _harvest = harvest;
    private DebugTelemetrySnapshot? _lastSnapshot;
    private PerformanceMetricsSnapshot _lastPerformance;
    private float _leftColWidth;
    private int _blightChestDebugSelectedIndex;
    private readonly List<(string Label, string Value, NumVec4 Color)> _kvRows = [];

    private enum DebugSection
    {
        Status,
        Performance,
        Errors,
        Clicking,
        Labels,
        Pathfinding,
        Ultimatum,
        Altar,
        HoveredItem,
        Inventory,
        Blight
    }

    private readonly DebugSection[] _sectionOrder = new DebugSection[11];

    private int CollectEnabledSections()
    {
        int count = 0;
        if (_settings.DebugShowStatus.Value) _sectionOrder[count++] = DebugSection.Status;
        if (_settings.DebugShowPerformance.Value) _sectionOrder[count++] = DebugSection.Performance;
        if (_settings.DebugShowRecentErrors.Value) _sectionOrder[count++] = DebugSection.Errors;
        if (_settings.DebugShowClicking.Value) _sectionOrder[count++] = DebugSection.Clicking;
        if (_settings.DebugShowLabels.Value) _sectionOrder[count++] = DebugSection.Labels;
        if (_settings.DebugShowPathfinding.Value) _sectionOrder[count++] = DebugSection.Pathfinding;
        if (_settings.DebugShowUltimatum.Value) _sectionOrder[count++] = DebugSection.Ultimatum;
        if (_settings.DebugShowAltarDetection.Value || _settings.DebugShowAltarService.Value)
            _sectionOrder[count++] = DebugSection.Altar;
        if (_settings.DebugShowHoveredItemMetadata.Value) _sectionOrder[count++] = DebugSection.HoveredItem;
        if (_settings.DebugShowInventoryPickup.Value) _sectionOrder[count++] = DebugSection.Inventory;
        if (_settings.DebugShowBlight.Value) _sectionOrder[count++] = DebugSection.Blight;
        return count;
    }

    private void RenderDebugSection(DebugSection section)
    {
        switch (section)
        {
            case DebugSection.Status: RenderStatusSection(); break;
            case DebugSection.Performance: RenderPerformanceSection(); break;
            case DebugSection.Errors: RenderErrorsSection(); break;
            case DebugSection.Clicking: RenderClickSection(); break;
            case DebugSection.Labels: RenderLabelsSection(); break;
            case DebugSection.Pathfinding: RenderPathfindingSection(); break;
            case DebugSection.Ultimatum: RenderUltimatumSection(); break;
            case DebugSection.Altar: RenderAltarSection(); break;
            case DebugSection.HoveredItem: RenderHoveredItemSection(); break;
            case DebugSection.Inventory: RenderInventorySection(); break;
            case DebugSection.Blight: RenderBlightSection(); break;
        }
    }

    internal void Draw()
    {
        if (!_settings.DebugWindowVisible.Value)
            return;

        if (_telemetrySource != null)
            _lastSnapshot = _telemetrySource.GetSnapshot();
        if (_lastSnapshot == null)
            _lastSnapshot = DebugTelemetrySnapshot.Empty;

        if (_performanceMonitor != null)
            _lastPerformance = _performanceMonitor.GetDebugSnapshot();

        ImGui.SetNextWindowSizeConstraints(
            new NumVec2(WindowMinWidth, WindowMinHeight),
            new NumVec2(2000f, 2000f));

        bool visible = _settings.DebugWindowVisible.Value;
        if (!ImGui.Begin(WindowTitle, ref visible, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }
        _settings.DebugWindowVisible.Value = visible;

        DrawToolbar();
        ImGui.Separator();

        int sectionCount = CollectEnabledSections();
        if (sectionCount == 0) { ImGui.End(); return; }

        int split = Math.Min(4, sectionCount);
        bool hasRight = split < sectionCount;

        float availW = ImGui.GetContentRegionAvail().X;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float colStartY = ImGui.GetCursorPosY();
        float availH = ImGui.GetContentRegionAvail().Y;

        if (!hasRight)
        {
            ImGui.BeginChild("LeftCol", new NumVec2(availW, 0));
            for (int i = 0; i < sectionCount; i++)
            {
                SectionAccent();
                RenderDebugSection(_sectionOrder[i]);
            }
            ImGui.EndChild();
            ImGui.End();
            return;
        }

        const float splitterW = 6f;
        const float minColW = 280f;
        if (_leftColWidth <= 0f)
            _leftColWidth = TryLoadSplitterWidth(out float savedWidth)
                ? savedWidth
                : (availW - spacing) * 0.6f;
        float leftW = Math.Clamp(_leftColWidth, minColW, availW - minColW - spacing * 2);

        ImGui.BeginChild("LeftCol", new NumVec2(leftW, 0));
        for (int i = 0; i < split; i++)
        {
            SectionAccent();
            RenderDebugSection(_sectionOrder[i]);
        }
        ImGui.EndChild();

        ImGui.SetCursorPos(new NumVec2(leftW + spacing, colStartY));
        NumVec2 splitterOrigin = ImGui.GetWindowPos() + ImGui.GetCursorPos();
        ImGui.InvisibleButton("##colSplitter", new NumVec2(splitterW, availH));
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
        if (ImGui.IsItemActive())
        {
            float newLeftW = Math.Clamp(_leftColWidth + ImGui.GetIO().MouseDelta.X, minColW, availW - minColW - spacing * 2);
            if (SystemMath.Abs(newLeftW - _leftColWidth) >= 1f)
            {
                _leftColWidth = newLeftW;
                _settings.DebugWindowSplitterWidth.Value = _leftColWidth.ToString("F0");
            }
        }

        float splitterLineX = splitterOrigin.X + splitterW * 0.5f;
        ImGui.GetWindowDrawList().AddLine(
            new NumVec2(splitterLineX, splitterOrigin.Y),
            new NumVec2(splitterLineX, splitterOrigin.Y + availH),
            ImGui.GetColorU32(ImGuiCol.Separator));

        float rightX = leftW + spacing + splitterW + spacing;
        float rightW = availW - rightX;
        ImGui.SetCursorPos(new NumVec2(rightX, colStartY));
        ImGui.BeginChild("RightCol", new NumVec2(rightW, 0));
        for (int i = split; i < sectionCount; i++)
        {
            SectionAccent();
            RenderDebugSection(_sectionOrder[i]);
        }
        ImGui.EndChild();

        ImGui.End();
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("Copy All"))
            CopyAllToClipboard();
        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ToolbarToggle("Status", _settings.DebugShowStatus);
        ToolbarToggle("Performance", _settings.DebugShowPerformance);
        ToolbarToggle("Errors", _settings.DebugShowRecentErrors);
        ToolbarToggle("Click", _settings.DebugShowClicking);
        ToolbarToggle("Labels", _settings.DebugShowLabels);
        ToolbarToggle("Pathfinding", _settings.DebugShowPathfinding);
        ToolbarToggle("Ultimatum", _settings.DebugShowUltimatum);
        ToolbarToggle("Hovered", _settings.DebugShowHoveredItemMetadata);
        ToolbarToggle("Inventory", _settings.DebugShowInventoryPickup);
        ToolbarToggle("Blight", _settings.DebugShowBlight);

        ImGui.SameLine();
        bool v = _settings.DebugShowAltarDetection.Value || _settings.DebugShowAltarService.Value;
        if (ImGui.Checkbox("Altar", ref v))
        {
            _settings.DebugShowAltarDetection.Value = v;
            _settings.DebugShowAltarService.Value = v;
        }
    }

    private static void ToolbarToggle(string label, ToggleNode node)
    {
        ImGui.SameLine();
        bool v = node.Value;
        if (ImGui.Checkbox(label, ref v))
            node.Value = v;
    }

    private static void DataRows(List<(string Label, string Value, NumVec4 Color)> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            (string Label, string Value, Vector4 Color) it = items[i];
            ImGui.TextColored(it.Color, $"{it.Label}: {it.Value}");
        }
    }

    private static void InlineRow(params (string Label, string Value, NumVec4 Color)[] items)
    {
        if (items.Length == 0) return;
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
                ImGui.TextColored(CDim, " | ");
                ImGui.SameLine();
            }
            ImGui.TextColored(items[i].Color, $"{items[i].Label}: {items[i].Value}");
        }
    }

    private static void RenderTrail(string label, IReadOnlyList<string> trail, int maxRows = 20)
    {
        if (trail.Count == 0) return;

        ImGui.Spacing();
        ImGui.TextColored(CHeader, $"{label} ({trail.Count}):");
        int start = Math.Max(0, trail.Count - maxRows);

        for (int i = start; i < trail.Count; i++)
        {
            string s = trail[i];
            s = SanitizeText(s);
            ImGui.TextWrapped(s);
        }
    }

    private static string SanitizeText(string text)
    {
        // Bound the stackalloc buffer and fall back to the heap for very long strings.
        const int MaxStackChars = 4096;
        char[]? heap = null;
        Span<char> buffer = text.Length <= MaxStackChars
            ? stackalloc char[text.Length]
            : (heap = new char[text.Length]);
        int written = 0;
        bool changed = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char replacement = c switch
            {
                '\u2190' => '<',
                '\u2191' => '^',
                '\u2192' => '>',
                '\u2193' => 'v',
                '\u2713' => '+',
                '\u2717' => 'x',
                '\u25CF' => '*',
                '\u25B6' => '>',
                '\u25C0' => '<',
                '\u2665' => 'H',
                '\u2605' => '*',
                _ => c
            };
            buffer[written++] = replacement;
            if (replacement != c)
                changed = true;
        }

        return changed ? new string(buffer[..written]) : text;
    }

    private void RenderStatusSection()
    {
        if (!ImGui.CollapsingHeader("Status", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        StatusTelemetrySnapshot s = snap.Status;

        ImGui.Spacing();
        InlineRow(
            ("Game Controller", BoolStr(s.GameControllerAvailable), BoolColor(s.GameControllerAvailable)),
            ("In Game", BoolStr(s.InGame), BoolColor(s.InGame)),
            ("Entity List", BoolStr(s.EntityListValid), BoolColor(s.EntityListValid))
        );
        InlineRow(
            ("Player Valid", BoolStr(s.PlayerValid), BoolColor(s.PlayerValid)),
            ("Area", !string.IsNullOrEmpty(s.CurrentAreaName) && s.CurrentAreaName != "Unknown" ? s.CurrentAreaName : "--", CInfo),
            ("Items", s.VisibleItemCount.ToString(), CWhite)
        );
        if (s.PlayerPositionAvailable)
            InlineRow(
                ("Cached Labels", s.CachedLabelsAvailable ? s.CachedLabelCount.ToString() : "N/A", s.CachedLabelsAvailable ? CWhite : CMuted),
                ("Player Pos", $"({s.PlayerPositionX:F1}, {s.PlayerPositionY:F1})", CWhite)
            );
        else
            ImGui.TextColored(s.CachedLabelsAvailable ? CWhite : CMuted,
                $"Cached Labels: {(s.CachedLabelsAvailable ? s.CachedLabelCount.ToString() : "N/A")}");
    }

    private void RenderPerformanceSection()
    {
        if (!ImGui.CollapsingHeader("Performance", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        PerformanceMetricsSnapshot perf = _lastPerformance;

        // Two independent columns: summary lines pinned to a fixed X, fixed-fit tables adjacent (Col1 = render/coroutine/DLR, Col2 = process/GC).
        const float summaryColX = 370f;

        ImGui.Spacing();
        if (perf.Fps.Max > 0)
        {
            double fps = perf.Fps.Current;
            NumVec4 fpsCol = fps >= 144f ? CGreen : fps >= 60f ? CWarn : CError;
            ImGui.TextColored(fpsCol, $"FPS: {fps:F1} (avg: {perf.Fps.Average:F1}, max: {perf.Fps.Max:F1})");
        }
        else
        {
            ImGui.TextColored(CMuted, "FPS: --");
        }
        ImGui.SameLine(summaryColX);
        MemoryMetricsSnapshot mem = perf.Memory;
        ImGui.TextColored(SizeColor(mem.ProcessWorkingSetMb), $"Memory: {FormatMemoryMb(mem.ProcessWorkingSetMb)}");
        ImGui.SameLine();
        ImGui.TextColored(SizeColor(mem.ManagedHeapMb), $"  Managed: {FormatMemoryMb(mem.ManagedHeapMb)}");
        ImGui.SameLine();
        ImGui.TextColored(FragmentationColor(mem.FragmentedMb), $"  Frag: {FormatMemoryMb(mem.FragmentedMb)}");

        ImGui.Spacing();
        bool renderInGame = _settings.RenderPerformanceInGame.Value;
        if (ImGui.Checkbox("Render in-game", ref renderInGame))
            _settings.RenderPerformanceInGame.Value = renderInGame;
        ImGui.SameLine();
        bool onlyInMap = _settings.OnlyShowPerformanceInGameWhileInMap.Value;
        if (ImGui.Checkbox("Only in map", ref onlyInMap))
            _settings.OnlyShowPerformanceInGameWhileInMap.Value = onlyInMap;
        ImGui.SameLine(summaryColX);
        if (ImGui.Button("Reset setup flag"))
        {
            _settings.ShownPerformanceConfirmation.Value = false;
            ExileCorePerformanceApplier.SetSuppressSetupUntilReload(true);
            PerformanceSettingsPanelRenderer.ForceShowSetup();
        }
        ImGui.SameLine();
        ImGui.TextColored(GcPauseColor(mem.GcPauseMaxMs), $"  GC Pause: {mem.GcPauseLastMs:F0}/{mem.GcPauseAvgMs:F0}/{mem.GcPauseMaxMs:F0} ms");

        RenderRenderTable(perf);
        float renderBottom = ImGui.GetCursorPosY();
        ImGui.SameLine(0, 0);
        ImGui.SetCursorPosX(summaryColX);
        RenderProcessingTable(perf);
        float processBottom = ImGui.GetCursorPosY();

        // Column 1 stack (render -> coroutine -> DLR -> click target) flows flush below render ms/f.
        ImGui.SetCursorPosY(renderBottom);
        ImGui.SetCursorPosX(0);
        double coroutineFps = perf.Fps.Current;
        BeginFixedTable("CoroutinesPerFrame", "Coroutine ms/f", 175f, "Last", 38f, "Avg", 38f, "Max", 38f);
        RenderTimingTotalRow("Total", perf.CoroutinesTotalPerFrameSnapshot);
        RenderScaledTimingRow("Altar", perf.AltarCoroutine, coroutineFps);
        RenderScaledTimingRow("Blight", perf.BlightCoroutine, coroutineFps);
        RenderScaledTimingRow("Click", perf.ClickCoroutine, coroutineFps);
        RenderScaledTimingRow("Flare", perf.FlareCoroutine, coroutineFps);
        RenderScaledTimingRow("Label Overlay", perf.LabelOverlayCoroutine, coroutineFps);
        RenderScaledTimingRow("Ultimatum", perf.UltimatumCoroutine, coroutineFps);
        ImGui.EndTable();
        float coroutineBottom = ImGui.GetCursorPosY();

        ImGui.SetCursorPosY(coroutineBottom);
        ImGui.SetCursorPosX(0);
        RenderDlrTable(perf);
        float dlrBottom = ImGui.GetCursorPosY();

        ImGui.SetCursorPosY(dlrBottom);
        ImGui.SetCursorPosX(0);

        // Column 2: GC byte/s sits flush right below process ms/f.
        ImGui.SetCursorPosY(processBottom);
        ImGui.SetCursorPosX(summaryColX);
        RenderGcTable(perf);
        float gcBottom = ImGui.GetCursorPosY();

        // Resume below the taller column so following content never overlaps.
        ImGui.SetCursorPosY(gcBottom);
        ImGui.SetCursorPosX(0);
    }

    private static void BeginFixedTable(string id, string col1, float w1, string col2, float w2, string col3, float w3, string col4, float w4)
    {
        ImGui.BeginTable(id, 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX);
        ImGui.TableSetupColumn(col1, ImGuiTableColumnFlags.WidthFixed, w1);
        ImGui.TableSetupColumn(col2, ImGuiTableColumnFlags.WidthFixed, w2);
        ImGui.TableSetupColumn(col3, ImGuiTableColumnFlags.WidthFixed, w3);
        ImGui.TableSetupColumn(col4, ImGuiTableColumnFlags.WidthFixed, w4);
        ImGui.TableHeadersRow();
    }

    private static void RenderRenderTable(PerformanceMetricsSnapshot perf)
    {
        BeginFixedTable("RenderBreakdown", "Render ms/f", 175f, "Last", 38f, "Avg", 38f, "Max", 38f);

        RenderTimingTotalRow("Total", perf.RenderTableTotal);
        RenderTimingRow("Altar", perf.GetRenderSection(RenderSection.AltarOverlay));
        RenderTimingRow("Blight", perf.GetRenderSection(RenderSection.BlightOverlay));
        RenderTimingRow("Click.Hotkey", perf.GetRenderSection(RenderSection.ClickHotkeyToggle));
        RenderTimingRow("Debug", perf.GetRenderSection(RenderSection.DebugOverlay));
        RenderTimingRow("Flush.Frame", perf.GetRenderSection(RenderSection.FrameFlush));
        RenderTimingRow("Flush.Text", perf.GetRenderSection(RenderSection.TextFlush));
        RenderTimingRow("Harvest", perf.GetRenderSection(RenderSection.HarvestOverlay));
        RenderTimingRow("Inv.Full", perf.GetRenderSection(RenderSection.InventoryFullWarning));
        RenderTimingRow("Lazy", perf.GetRenderSection(RenderSection.LazyMode));
        RenderTimingRow("Pathfinding", perf.GetRenderSection(RenderSection.PathfindingOverlay));
        RenderTimingRow("Perf.Overlay", perf.GetRenderSection(RenderSection.PerformanceOverlay));
        RenderTimingRow("Strongbox", perf.GetRenderSection(RenderSection.StrongboxOverlay));
        RenderTimingRow("UI.Rect", perf.GetRenderSection(RenderSection.UiRegionRectangle));
        RenderTimingRow("Ultimatum", perf.GetRenderSection(RenderSection.UltimatumOverlay));
        ImGui.EndTable();
    }

    private static void RenderProcessingTable(PerformanceMetricsSnapshot perf)
    {
        double targetMs = perf.ClickTargetIntervalMs;
        BeginFixedTable("ProcessingBreakdown", "Process ms/click", 110f, "Last", 58f, "Avg", 58f, "Max", 58f);

        RenderTimingTotalRow("Total", perf.ProcessingTotal);
        RenderRunTimingRow("Altar", perf.GetProcessingSection(ProcessingSection.Altar), targetMs);
        RenderBreakdownTiming(perf, ProcessingSection.Altar, targetMs);
        RenderRunTimingRow("Area.Blocked", perf.GetProcessingSection(ProcessingSection.AreaBlockedUi), targetMs);
        RenderRunTimingRow("Blight", perf.GetProcessingSection(ProcessingSection.Blight), targetMs);
        RenderBreakdownTiming(perf, ProcessingSection.Blight, targetMs);
        RenderClickProcessingRows(perf);
        RenderRunTimingRow("Dump", perf.GetProcessingSection(ProcessingSection.GameStateDump), targetMs);
        RenderRunTimingRow("Flare", perf.GetProcessingSection(ProcessingSection.Flare), targetMs);
        RenderBreakdownTiming(perf, ProcessingSection.Flare, targetMs);
        RenderRunTimingRow("Harvest", perf.GetProcessingSection(ProcessingSection.Harvest), targetMs);
        RenderRunTimingRow("Label Scan", perf.GetProcessingSection(ProcessingSection.Label), targetMs);
        RenderRunTimingRow("Manual Hover", perf.GetProcessingSection(ProcessingSection.ManualUiHover), targetMs);
        RenderRunTimingRow("Pathfinding", perf.GetProcessingSection(ProcessingSection.Pathfinding), targetMs);
        RenderBreakdownTiming(perf, ProcessingSection.Pathfinding, targetMs);
        RenderRunTimingRow("Strongbox", perf.GetProcessingSection(ProcessingSection.Strongbox), targetMs);
        RenderBreakdownTiming(perf, ProcessingSection.Strongbox, targetMs);
        RenderRunTimingRow("Ultimatum", perf.GetProcessingSection(ProcessingSection.Ultimatum), targetMs);
        ImGui.EndTable();
    }

    private static void RenderRunTimingRow(string label, TimingMetricsSnapshot s, double targetMs)
    {
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.LastMs, targetMs), $"{s.LastMs:F1}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.AverageMs, targetMs), $"{s.AverageMs:F1}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.MaxMs, targetMs), $"{s.MaxMs:F1}");
    }

    // Per-section sub-stage TIME rows for the process ms/click table, per-run like the parent rows.
    private static void RenderBreakdownTiming(PerformanceMetricsSnapshot perf, ProcessingSection section, double targetMs)
    {
        if (perf.Breakdowns == null || !perf.Breakdowns.TryGetValue(section, out BreakdownStats stats) || stats.SampleCount == 0)
            return;
        foreach (BreakdownStageSnapshot stage in stats.Stages)
            RenderRunStageTimingRow($"  {stage.Name}", stage.Time, targetMs);
    }

    private static void RenderRunStageTimingRow(string label, TimingStageSnapshot s, double targetMs)
    {
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.LastMs, targetMs), $"{s.LastMs:F1}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.AvgMs, targetMs), $"{s.AvgMs:F1}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.MaxMs, targetMs), $"{s.MaxMs:F1}");
    }

    private static void RenderClickProcessingRows(PerformanceMetricsSnapshot perf)
    {
        double targetMs = perf.ClickTargetIntervalMs;
        TimingMetricsSnapshot click = perf.GetProcessingSection(ProcessingSection.Click);
        if (click.SampleCount > 0)
            RenderRunTimingRow("Click", click, targetMs);

        ClickAllocationStats alloc = perf.ClickAllocation;
        if (alloc.SampleCount > 0)
        {
            RenderRunStageTimingRow("  Context", alloc.ContextTime, targetMs);
            RenderRunStageTimingRow("  Acquire", alloc.AcquireTime, targetMs);
            RenderRunStageTimingRow("  Rank", alloc.RankTime, targetMs);
            RenderRunStageTimingRow("  Execute", alloc.ExecuteTime, targetMs);
            RenderRunStageTimingRow("  Post", alloc.PostTime, targetMs);
        }

        RenderClickFrequencyTargetRows(perf);
    }

    private static void RenderClickFrequencyTargetRows(PerformanceMetricsSnapshot perf)
    {
        double targetMs = perf.ClickTargetIntervalMs;
        if (targetMs <= 0)
            return;
        FreqTargetRow("Click Frequency", $"{targetMs:F0}", CWarn);
        FreqRunRow("  Processing", perf.GetProcessingSection(ProcessingSection.Click), targetMs);
        FreqRunRow("  Sleep", perf.ClickSleepTiming, targetMs);
        FreqRunRow("  Total", perf.ClickCoroutine, targetMs);
    }

    private static void FreqRunRow(string label, TimingMetricsSnapshot s, double targetMs)
    {
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.LastMs, targetMs), $"{s.LastMs:F1}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.AverageMs, targetMs), $"{s.AverageMs:F1}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(ClickTargetColor(s.MaxMs, targetMs), $"{s.MaxMs:F1}");
    }

    // ms/click coloring relative to the click frequency target: green within the target, yellow up to +25% over it, red beyond that.
    private static NumVec4 ClickTargetColor(double msPerClick, double targetMs)
        => targetMs <= 0 ? FrameColor(msPerClick)
            : msPerClick <= targetMs ? CGreen
            : msPerClick <= targetMs * 1.25 ? CWarn
            : CError;

    private static void RenderGcTable(PerformanceMetricsSnapshot perf)
    {
        MemoryMetricsSnapshot m = perf.Memory;
        if (m.GcSections == null)
            return;

        BeginFixedTable("GcBreakdown", "GC byte/s", 110f, "Last", 58f, "Avg", 58f, "Max", 58f);
        RenderGcTotalRow(m);
        RenderGcRow("Altar", m.GcSections[(int)ProcessingSection.Altar]);
        RenderBreakdown(perf, ProcessingSection.Altar);
        RenderGcRow("Area.Blocked", m.GcSections[(int)ProcessingSection.AreaBlockedUi]);
        RenderGcRow("Blight", m.GcSections[(int)ProcessingSection.Blight]);
        RenderBreakdown(perf, ProcessingSection.Blight);
        RenderGcRow("Click", m.GcSections[(int)ProcessingSection.Click]);
        RenderClickBreakdown(perf);
        RenderGcRow("Dump", m.GcSections[(int)ProcessingSection.GameStateDump]);
        RenderGcRow("Flare", m.GcSections[(int)ProcessingSection.Flare]);
        RenderBreakdown(perf, ProcessingSection.Flare);
        RenderGcRow("Harvest", m.GcSections[(int)ProcessingSection.Harvest]);
        RenderGcRow("Label Scan", m.GcSections[(int)ProcessingSection.Label]);
        RenderLabelScanBreakdown(perf);
        RenderGcRow("Manual Hover", m.GcSections[(int)ProcessingSection.ManualUiHover]);
        RenderGcRow("Pathfinding", m.GcSections[(int)ProcessingSection.Pathfinding]);
        RenderBreakdown(perf, ProcessingSection.Pathfinding);
        RenderGcRow("Strongbox", m.GcSections[(int)ProcessingSection.Strongbox]);
        RenderBreakdown(perf, ProcessingSection.Strongbox);
        RenderGcRow("Ultimatum", m.GcSections[(int)ProcessingSection.Ultimatum]);
        ImGui.EndTable();
    }

    // Per-feature DLR ms/f table; row order mirrors the GC table.
    private static void RenderDlrTable(PerformanceMetricsSnapshot perf)
    {
        double fps = perf.Fps.Current;
        MemoryMetricsSnapshot mem = perf.Memory;
        if (fps <= 0 || mem.DlrSections == null)
            return;

        BeginFixedTable("DlrBreakdown", "DLR ms/f", 175f, "Last", 38f, "Avg", 38f, "Max", 38f);
        RenderDlrTotalRow(mem, fps);
        RenderDlrRow("Altar", mem.DlrSections[(int)ProcessingSection.Altar], fps);
        RenderDlrRow("Area.Blocked", mem.DlrSections[(int)ProcessingSection.AreaBlockedUi], fps);
        RenderDlrRow("Blight", mem.DlrSections[(int)ProcessingSection.Blight], fps);
        RenderDlrRow("Click", mem.DlrSections[(int)ProcessingSection.Click], fps);
        RenderDlrRow("Dump", mem.DlrSections[(int)ProcessingSection.GameStateDump], fps);
        RenderDlrRow("Flare", mem.DlrSections[(int)ProcessingSection.Flare], fps);
        RenderDlrRow("Harvest", mem.DlrSections[(int)ProcessingSection.Harvest], fps);
        RenderDlrRow("Label Scan", mem.DlrSections[(int)ProcessingSection.Label], fps);
        RenderDlrRow("Manual Hover", mem.DlrSections[(int)ProcessingSection.ManualUiHover], fps);
        RenderDlrRow("Pathfinding", mem.DlrSections[(int)ProcessingSection.Pathfinding], fps);
        RenderDlrRow("Strongbox", mem.DlrSections[(int)ProcessingSection.Strongbox], fps);
        RenderDlrRow("Ultimatum", mem.DlrSections[(int)ProcessingSection.Ultimatum], fps);
        ImGui.EndTable();
    }

    private static void RenderDlrTotalRow(MemoryMetricsSnapshot mem, double fps)
    {
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.TextColored(CHeader, "Total");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(mem.DlrReadsMsLastPerSec / fps), $"{mem.DlrReadsMsLastPerSec / fps:F2}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(mem.DlrReadsMsAvgPerSec / fps), $"{mem.DlrReadsMsAvgPerSec / fps:F2}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(mem.DlrReadsMsMaxPerSec / fps), $"{mem.DlrReadsMsMaxPerSec / fps:F2}");
    }

    private static void RenderDlrRow(string label, DlrSectionSnapshot s, double fps)
    {
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        if (s.ReadsMaxPerSec <= 0 && s.ReadsAvgPerSec <= 0)
        {
            _ = ImGui.TableNextColumn(); ImGui.Text("-");
            _ = ImGui.TableNextColumn(); ImGui.Text("-");
            _ = ImGui.TableNextColumn(); ImGui.Text("-");
            return;
        }
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(s.MsLastPerSec / fps), $"{s.MsLastPerSec / fps:F2}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(s.MsAvgPerSec / fps), $"{s.MsAvgPerSec / fps:F2}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(s.MsMaxPerSec / fps), $"{s.MsMaxPerSec / fps:F2}");
    }

    // Generic per-section sub-stage ALLOCATION rows for the GC table (time rows live in the process table).
    private static void RenderBreakdown(PerformanceMetricsSnapshot perf, ProcessingSection section)
    {
        if (perf.Breakdowns == null || !perf.Breakdowns.TryGetValue(section, out BreakdownStats stats) || stats.SampleCount == 0)
            return;
        double periodMs = perf.GetAllocationSection(section).AvgPeriodMs;
        foreach (BreakdownStageSnapshot stage in stats.Stages)
            RenderGcStageRow($"  {stage.Name}", stage.Allocation, periodMs);
    }

    private static void RenderLabelScanBreakdown(PerformanceMetricsSnapshot perf)
    {
        LabelScanAllocationStats s = perf.LabelScanAllocation;
        if (s.SampleCount == 0)
            return;
        double periodMs = perf.GetAllocationSection(ProcessingSection.Label).AvgPeriodMs;
        RenderGcStageRow("ListRead", s.ListRead, periodMs);
        RenderGcStageRow("ListAlloc", s.ListAlloc, periodMs);
        RenderGcStageRow("Validity", s.Validity, periodMs);
        RenderGcStageRow("Sort", s.Sort, periodMs);
    }

    private static void RenderClickBreakdown(PerformanceMetricsSnapshot perf)
    {
        ClickAllocationStats s = perf.ClickAllocation;
        if (s.SampleCount == 0)
            return;
        double periodMs = perf.GetAllocationSection(ProcessingSection.Click).AvgPeriodMs;
        RenderGcStageRow("Context", s.Context, periodMs);
        RenderGcStageRow("Acquire", s.Acquire, periodMs);
        RenderGcStageRow("Rank", s.Rank, periodMs);
        RenderGcStageRow("Execute", s.Execute, periodMs);
        RenderGcStageRow("Post", s.Post, periodMs);
        RenderGcStageRow("Other", s.Other, periodMs);
    }

    // One breakdown stage as byte/s Last/Avg/Max; stages that never allocated are skipped.
    private static void RenderGcStageRow(string label, AllocationStageSnapshot s, double periodMs)
    {
        double lastPerSecond = periodMs > 0 ? s.LastBytesPerRun * 1000.0 / periodMs : 0;
        double avgPerSecond = periodMs > 0 ? s.AvgBytesPerRun * 1000.0 / periodMs : 0;
        double maxPerSecond = s.MaxAllocPerSecond;
        if (s.MaxBytesPerRun <= 0)
            return;
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text($"  {label}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcColor(lastPerSecond), FormatBytes(lastPerSecond));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcColor(avgPerSecond), FormatBytes(avgPerSecond));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcColor(maxPerSecond), FormatBytes(maxPerSecond));
    }

    private static void RenderGcRow(string label, GcSectionSnapshot s)
    {
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        if (s.BytesMaxPerSec <= 0 && s.BytesAvgPerSec <= 0)
        {
            _ = ImGui.TableNextColumn(); ImGui.Text("-");
            _ = ImGui.TableNextColumn(); ImGui.Text("-");
            _ = ImGui.TableNextColumn(); ImGui.Text("-");
            return;
        }
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcColor(s.BytesLastPerSec), FormatBytes(s.BytesLastPerSec));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcColor(s.BytesAvgPerSec), FormatBytes(s.BytesAvgPerSec));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcColor(s.BytesMaxPerSec), FormatBytes(s.BytesMaxPerSec));
    }

    // Per-value ms/f coloring: each Last/Avg/Max column is colored by its own value (<=3 green, <=6 yellow, >6 red).
    private static NumVec4 FrameColor(double ms)
        => ms <= 3.0 ? CGreen : ms <= 6.0 ? CWarn : CError;

    // Per-feature allocation rate: <=10MB/s healthy, <=25MB/s tolerable, above that eats the whole budget.
    private static NumVec4 GcColor(double allocPerSecond)
    {
        double mb = 1024.0 * 1024.0;
        return allocPerSecond > 25 * mb ? CError : allocPerSecond > 10 * mb ? CWarn : CGreen;
    }

    // Table-wide timing totals: last/avg/max summed across every row beneath.
    private static void RenderTimingTotalRow(string label, TimingMetricsSnapshot total)
    {
        bool hasData = total.SampleCount > 0;
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.TextColored(CHeader, label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(hasData ? total.LastMs : 0), hasData ? $"{total.LastMs:F2}" : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(hasData ? total.AverageMs : 0), hasData ? $"{total.AverageMs:F2}" : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(hasData ? total.MaxMs : 0), hasData ? $"{total.MaxMs:F2}" : "-");
    }

    // Table-wide GC totals as the first data row: byte/s Last/Avg/Max summed across every row beneath.
    private static void RenderGcTotalRow(MemoryMetricsSnapshot m)
    {
        double totalLast = 0, totalAvg = 0, totalMax = 0;
        if (m.GcSections != null)
        {
            for (int s = 1; s < m.GcSections.Count; s++)
            {
                totalLast += m.GcSections[s].BytesLastPerSec;
                totalAvg += m.GcSections[s].BytesAvgPerSec;
                totalMax += m.GcSections[s].BytesMaxPerSec;
            }
        }
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.TextColored(CHeader, "Total");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcTotalColor(totalLast), FormatBytes(totalLast));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcTotalColor(totalAvg), FormatBytes(totalAvg));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(GcTotalColor(totalMax), FormatBytes(totalMax));
    }

    // Whole-plugin allocation rate: <=25MB/s healthy, <=50MB/s tolerable, above that too much.
    private static NumVec4 GcTotalColor(double allocPerSecond)
    {
        double mb = 1024.0 * 1024.0;
        return allocPerSecond > 50 * mb ? CError : allocPerSecond > 25 * mb ? CWarn : CGreen;
    }

    private static string FormatMemoryMb(double mb)
        => mb >= 1024.0 ? $"{mb / 1024.0:F1} GB" : $"{mb:F0} MB";

    private static NumVec4 SizeColor(double mb)
        => mb > 2048 ? CError : mb > 1228.8 ? CWarn : CGreen;

    private static NumVec4 FragmentationColor(double mb)
        => mb > 400 ? CError : mb > 100 ? CWarn : CGreen;

    // Blocking GC pause coloring: >100ms pauses hitch all threads; above ~16ms (a frame) is a warning.
    private static NumVec4 GcPauseColor(double maxPauseMs)
        => maxPauseMs > 100 ? CError : maxPauseMs > 16 ? CWarn : CGreen;

    private static string FormatBytes(double bytes)
    {
        if (bytes >= 1024.0 * 1024.0)
            return $"{bytes / (1024.0 * 1024.0):F0} MB";
        if (bytes >= 1024.0)
            return $"{bytes / 1024.0:F0} KB";
        return $"{bytes:F0} B";
    }

    private static string FormatAllocRate(double bytesPerSecond)
        => $"{FormatBytes(bytesPerSecond)}/s";

    private static void RenderScaledTimingRow(string label, TimingMetricsSnapshot stats, double fps)
    {
        double scale = stats.PerFrameScale(fps);
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(scale > 0 ? FrameColor(stats.LastMs * scale) : CGreen, scale > 0 ? $"{stats.LastMs * scale:F2}" : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(scale > 0 ? FrameColor(stats.AverageMs * scale) : CGreen, scale > 0 ? $"{stats.AverageMs * scale:F2}" : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(scale > 0 ? FrameColor(stats.MaxMs * scale) : CGreen, scale > 0 ? $"{stats.MaxMs * scale:F2}" : "-");
    }

    private static void RenderTimingRow(string label, TimingMetricsSnapshot stats)
    {
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(stats.LastMs), $"{stats.LastMs:F2}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(stats.AverageMs), $"{stats.AverageMs:F2}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(FrameColor(stats.MaxMs), $"{stats.MaxMs:F2}");
    }

    private static void FreqTargetRow(string label, string value, NumVec4 color)
    {
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(color, value);
    }

    private void RenderErrorsSection()
    {
        if (!ImGui.CollapsingHeader("Recent Errors", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        IReadOnlyList<string> errors = snap.Errors.RecentErrors;
        if (errors.Count == 0)
        {
            ImGui.TextColored(CGreen, "No recent errors");
            return;
        }

        ImGui.TextColored(CWarn, $"Error Count: {errors.Count}");
        int start = Math.Max(0, errors.Count - 3);
        for (int i = start; i < errors.Count; i++)
            ImGui.TextColored(CError, errors[i]);
    }

    private void RenderClickSection()
    {
        if (!ImGui.CollapsingHeader("Click", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        ClickTelemetrySnapshot click = snap.Click;

        if (!click.ServiceAvailable)
        {
            ImGui.TextColored(CMuted, "Click service unavailable");
            return;
        }

        if (_telemetrySource != null
            && _telemetrySource.TryGetFreezeState(out long freezeMs, out string freezeReason))
        {
            string freezeText = string.IsNullOrWhiteSpace(freezeReason)
                ? $"Telemetry Hold Active: {freezeMs}ms remaining"
                : $"Telemetry Hold Active: {freezeMs}ms remaining | {freezeReason}";
            ImGui.TextColored(COrange, freezeText);
            ImGui.Spacing();
        }

        RenderClickSettingsFull();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ClickDebugSnapshot cd = click.Click;
        if (cd.HasData)
        {
            NumVec4 stageColor = cd.Resolved && cd.ResolvedClickable ? CGreen : CWarn;
            ImGui.TextColored(stageColor, $"Stage: {cd.Stage}  Seq: {cd.Sequence}");

            InlineRow(
                ("Mechanic", cd.MechanicId, CWhite),
                ("Distance", $"{cd.Distance:F1}", CWhite)
            );

            ImGui.TextWrapped($"Entity: {cd.EntityPath}");

            if (!string.IsNullOrEmpty(cd.Notes))
                ImGui.TextColored(CMuted, $"Note: {cd.Notes}");

            ImGui.Spacing();
            InlineRow(
                ("World Raw", $"({cd.WorldScreenRaw.X:F1},{cd.WorldScreenRaw.Y:F1})", CLightBlue),
                ("World Abs", $"({cd.WorldScreenAbsolute.X:F1},{cd.WorldScreenAbsolute.Y:F1})", CLightBlue)
            );
            InlineRow(
                ("Click Pos", $"({cd.ResolvedClickPoint.X:F1},{cd.ResolvedClickPoint.Y:F1})", CLime),
                ("Center", $"InWnd={cd.CenterInWindow} Clk={cd.CenterClickable}", cd.CenterClickable ? CGreen : CWarn)
            );
            InlineRow(
                ("Resolved", $"InWnd={cd.ResolvedInWindow} Clk={cd.ResolvedClickable}", cd.ResolvedClickable ? CGreen : CWarn)
            );
        }
        else
        {
            ImGui.TextColored(CMuted, "No click data yet");
        }

        RenderTrail("Runtime Log", click.RuntimeLogTrail, 10);

        RenderTrail("Recent Stages", click.ClickTrail, 15);
    }

    private void RenderClickSettingsFull()
    {
        ClickItSettings s = _settings;

        ImGui.TextColored(CHeader, "Settings:");

        static string Fmt(bool v) => BoolStr(v);
        static NumVec4 Col(bool v) => BoolColor(v);

        ImGui.Spacing();
        InlineRow(
            ("ToggleMode", Fmt(s.ClickHotkeyToggleMode.Value), Col(s.ClickHotkeyToggleMode.Value)),
            ("ManualUIHover", Fmt(s.ClickOnManualUiHoverOnly.Value), Col(s.ClickOnManualUiHoverOnly.Value)),
            ("LazyMode", Fmt(s.LazyMode.Value), Col(s.LazyMode.Value))
        );
        InlineRow(
            ("LeftHanded", Fmt(s.LeftHanded.Value), Col(s.LeftHanded.Value)),
            ("ClickDist", s.ClickDistance.Value.ToString(), CWhite),
            ("FreqTarget", $"{s.ClickFrequencyTarget.Value}ms", CWhite)
        );
        InlineRow(
            ("VerifyCursor", Fmt(s.VerifyCursorInGameWindowBeforeClick.Value), Col(s.VerifyCursorInGameWindowBeforeClick.Value)),
            ("VerifyUIHover", Fmt(s.VerifyUIHoverWhenNotLazy.Value), Col(s.VerifyUIHoverWhenNotLazy.Value)),
            ("AvoidOverlap", Fmt(s.AvoidOverlappingLabelClickPoints.Value), Col(s.AvoidOverlappingLabelClickPoints.Value))
        );
        InlineRow(
            ("BlockPanel", Fmt(s.BlockOnOpenLeftRightPanel.Value), Col(s.BlockOnOpenLeftRightPanel.Value)),
            ("ToggleItems", Fmt(s.ToggleItems.Value), Col(s.ToggleItems.Value)),
            ("TogInterval", $"{s.ToggleItemsIntervalMs.Value}ms", CWhite)
        );
        InlineRow(
            ("TogPostBlock", $"{s.ToggleItemsPostToggleClickBlockMs.Value}ms", CWhite),
            ("WalkOffscreen", Fmt(s.WalkTowardOffscreenLabels.Value), Col(s.WalkTowardOffscreenLabels.Value)),
            ("PrioritizeOnscreen", Fmt(s.PrioritizeOnscreenClickableMechanicsOverPathfinding.Value), Col(s.PrioritizeOnscreenClickableMechanicsOverPathfinding.Value))
        );
        InlineRow(
            ("SearchBudget", $"{s.OffscreenPathfindingSearchBudget.Value}ms", CWhite),
            ("PauseBasic", Fmt(s.PauseAfterOpeningBasicChests.Value), Col(s.PauseAfterOpeningBasicChests.Value)),
            ("PauseLeague", Fmt(s.PauseAfterOpeningLeagueChests.Value), Col(s.PauseAfterOpeningLeagueChests.Value))
        );
        InlineRow(
            ("PauseHeist", Fmt(s.PauseAfterOpeningHeistChests.Value), Col(s.PauseAfterOpeningHeistChests.Value)),
            ("AllowNearby", Fmt(s.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value), Col(s.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value))
        );
    }

    private void RenderLabelsSection()
    {
        if (!ImGui.CollapsingHeader("Labels", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        LabelTelemetrySnapshot label = snap.Label;

        if (!label.ServiceAvailable)
        {
            ImGui.TextColored(CMuted, "Label filter service unavailable");
            return;
        }

        ImGui.Spacing();
        InlineRow(
            ("Total Visible", label.TotalVisibleLabels.ToString(), CWhite),
            ("Valid Labels", label.ValidVisibleLabels.ToString(), label.ValidVisibleLabels > 0 ? CGreen : CMuted)
        );

        LabelDebugSnapshot ld = label.Label;
        if (ld.HasData)
        {
            NumVec4 stageColor = ld.Stage is "SelectionReturned" or "SelectionScanSelected" ? CGreen : CWarn;
            InlineRow(
                ("Stage", ld.Stage, stageColor),
                ("Seq", ld.Sequence.ToString(), CWhite)
            );
            InlineRow(
                ("Range", $"{ld.StartIndex}-{ld.EndExclusive}", CWhite),
                ("Total Labels", ld.TotalLabels.ToString(), CWhite)
            );
            InlineRow(
                ("Considered", ld.ConsideredCandidates.ToString(), CWhite),
                ("Selected", ld.SelectedMechanicId, CInfo)
            );
        }

        if (ld.HasData && !string.IsNullOrEmpty(ld.Notes))
            ImGui.TextColored(CMuted, $"Note: {ld.Notes}");

        RenderTrail("Recent Stages", label.LabelTrail, 15);
    }

    private void RenderPathfindingSection()
    {
        if (!ImGui.CollapsingHeader("Pathfinding", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        PathfindingTelemetrySnapshot pf = snap.Pathfinding;

        if (!pf.ServiceAvailable)
        {
            ImGui.TextColored(CMuted, "Pathfinding service unavailable");
            return;
        }

        PathfindingDebugSnapshot data = pf.Pathfinding;

        ImGui.Spacing();
        InlineRow(
            ("Terrain", BoolStr(data.TerrainLoaded), BoolColor(data.TerrainLoaded)),
            ("Grid", $"{data.AreaWidth}x{data.AreaHeight}", CWhite),
            ("Nodes", data.LastExpandedNodes.ToString(), CWhite),
            ("Length", data.LastPathLength.ToString(), CWhite)
        );
        InlineRow(
            ("Compute", $"{data.LastComputeMs} ms", CWhite),
            ("Goal", data.LastGoalResolutionUsedFallback ? "Fallback" : "Direct",
                data.LastGoalResolutionUsedFallback ? CWarn : CGreen),
            ("Start", $"({data.LastStart.X},{data.LastStart.Y})", CWhite),
            ("Req", $"({data.LastRequestedGoal.X},{data.LastRequestedGoal.Y})", CWhite),
            ("Res", $"({data.LastResolvedGoal.X},{data.LastResolvedGoal.Y})", CInfo)
        );

        if (!string.IsNullOrWhiteSpace(data.LastGoalResolutionNote))
            ImGui.TextColored(CWarn, $"Goal Note: {data.LastGoalResolutionNote}");

        if (!string.IsNullOrWhiteSpace(data.LastFailureReason))
            ImGui.TextColored(CError, $"Failure: {data.LastFailureReason}");

        ImGui.TextWrapped($"Target Path: {data.LastTargetPath ?? "<none>"}");

        ImGui.Spacing();
        OffscreenMovementDebugSnapshot offscreen = pf.OffscreenMovement;
        if (offscreen.HasData)
        {
            ImGui.TextColored(CInfo, "Offscreen Movement:");

            _kvRows.Clear();
            _kvRows.Add(("Stage", offscreen.Stage, CWhite));
            _kvRows.Add(("Built Path", BoolStr(offscreen.BuiltPath), BoolColor(offscreen.BuiltPath)));
            _kvRows.Add(("From Path", BoolStr(offscreen.ResolvedFromPath), BoolColor(offscreen.ResolvedFromPath)));
            _kvRows.Add(("Click Point", BoolStr(offscreen.ResolvedClickPoint), BoolColor(offscreen.ResolvedClickPoint)));
            DataRows(_kvRows);

            if (!string.IsNullOrWhiteSpace(offscreen.MovementSkillDebug))
                ImGui.TextColored(CWarn, $"Skill: {offscreen.MovementSkillDebug}");

            ImGui.TextWrapped($"Target: {TrimPath(offscreen.TargetPath)}");

            _kvRows.Clear();
            _kvRows.Add(("Grid P", $"({offscreen.PlayerGrid.X:F0},{offscreen.PlayerGrid.Y:F0})", COrange));
            _kvRows.Add(("Grid T", $"({offscreen.TargetGrid.X:F0},{offscreen.TargetGrid.Y:F0})", COrange));
            _kvRows.Add(("Delta", $"({(offscreen.TargetGrid - offscreen.PlayerGrid).X:F0},{(offscreen.TargetGrid - offscreen.PlayerGrid).Y:F0})", COrange));
            DataRows(_kvRows);

            _kvRows.Clear();
            _kvRows.Add(("Center", $"({offscreen.WindowCenter.X:F1},{offscreen.WindowCenter.Y:F1})", CMuted));
            _kvRows.Add(("Target", $"({offscreen.TargetScreen.X:F1},{offscreen.TargetScreen.Y:F1})", CInfo));
            _kvRows.Add(("Click", $"({offscreen.ClickScreen.X:F1},{offscreen.ClickScreen.Y:F1})", CLime));
            DataRows(_kvRows);

            _kvRows.Clear();
            _kvRows.Add(("Target Dir", ToCompass(offscreen.TargetScreen - offscreen.WindowCenter), CInfo));
            _kvRows.Add(("Click Dir", ToCompass(offscreen.ClickScreen - offscreen.WindowCenter), CLime));
            DataRows(_kvRows);
        }
        else
        {
            ImGui.TextColored(CMuted, "Offscreen Movement: no data");
        }

        RenderTrail("Recent Stages", pf.OffscreenMovementTrail, 15);
        RenderTrail("Recent Events", pf.RecentEvents, 20);
    }

    private void RenderUltimatumSection()
    {
        if (!ImGui.CollapsingHeader("Ultimatum", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        ClickTelemetrySnapshot click = snap.Click;

        if (!click.ServiceAvailable)
        {
            ImGui.TextColored(CMuted, "Click service unavailable");
            return;
        }

        ImGui.Spacing();
        InlineRow(
            ("Initial", BoolStr(click.Settings.InitialUltimatumClickEnabled), BoolColor(click.Settings.InitialUltimatumClickEnabled)),
            ("Other", BoolStr(click.Settings.OtherUltimatumClickEnabled), BoolColor(click.Settings.OtherUltimatumClickEnabled))
        );

        UltimatumDebugSnapshot u = click.Ultimatum;
        if (!u.HasData)
        {
            IReadOnlyList<UltimatumOptionPreviewSnapshot> previews = click.UltimatumOptionPreview;
            if (previews.Count > 0)
                ImGui.TextColored(CWarn, "No click-flow snapshot yet; showing live panel preview.");
            else
                ImGui.TextColored(CMuted, "No ultimatum debug data yet");

            if (previews.Count > 0) RenderUltimatumPreviews(previews);
            RenderTrail("Recent Stages", click.UltimatumTrail, 10);
            return;
        }

        NumVec4 stageColor = u.ClickedTakeRewards
            ? CGold
            : (u.ClickedConfirm || u.ClickedChoice) ? CGreen : CWarn;
        ImGui.TextColored(stageColor, $"Stage: {u.Stage}  Seq: {u.Sequence}  Source: {u.Source}");

        ImGui.Spacing();
        InlineRow(
            ("Panel Visible", BoolStr(u.IsPanelVisible), BoolColor(u.IsPanelVisible)),
            ("GG Active", BoolStr(u.IsGruelingGauntletActive), u.IsGruelingGauntletActive ? CWarn : CMuted),
            ("Saturated", BoolStr(u.HasSaturatedChoice), u.HasSaturatedChoice ? CInfo : CMuted),
            ("TakeReward", BoolStr(u.ShouldTakeReward), u.ShouldTakeReward ? CGold : CMuted)
        );

        if (!string.IsNullOrEmpty(u.SaturatedModifier))
            ImGui.TextColored(CInfo, $"Saturated Modifier: {u.SaturatedModifier}");

        ImGui.TextColored(CWhite, $"Action: {u.Action}");
        ImGui.TextColored(CWhite, $"Candidates: {u.CandidateCount}  Saturated: {u.SaturatedCandidateCount}");
        ImGui.TextColored(CInfo, $"Best: {u.BestModifier} (priority={u.BestPriority})");

        InlineRow(
            ("Choice", BoolStr(u.ClickedChoice), BoolColor(u.ClickedChoice)),
            ("Confirm", BoolStr(u.ClickedConfirm), BoolColor(u.ClickedConfirm)),
            ("Take Rewards", BoolStr(u.ClickedTakeRewards), u.ClickedTakeRewards ? CGold : CMuted)
        );

        if (!string.IsNullOrEmpty(u.Notes))
            ImGui.TextColored(CMuted, $"Note: {u.Notes}");

        RenderUltimatumPreviews(click.UltimatumOptionPreview);
        RenderTrail("Recent Stages", click.UltimatumTrail, 10);
    }

    private static void RenderUltimatumPreviews(IReadOnlyList<UltimatumOptionPreviewSnapshot> previews)
    {
        if (previews.Count == 0) return;
        ImGui.Spacing();
        ImGui.TextColored(CHeader, $"Visible Options: {previews.Count}");

        int maxShow = Math.Min(3, previews.Count);
        if (ImGui.BeginTable("UltimatumPreviews", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 15f);
            ImGui.TableSetupColumn("Modifier", ImGuiTableColumnFlags.WidthFixed, 180f);
            ImGui.TableSetupColumn("Priority", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Center", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            for (int i = 0; i < maxShow; i++)
            {
                UltimatumOptionPreviewSnapshot opt = previews[i];
                ImGui.TableNextRow();
                _ = ImGui.TableNextColumn();
                ImGui.Text(opt.IsSelected ? "*" : "-");
                _ = ImGui.TableNextColumn();
                ImGui.TextColored(opt.IsSelected ? CGreen : CMuted, opt.ModifierName);
                _ = ImGui.TableNextColumn();
                ImGui.Text(opt.PriorityIndex.ToString());
                _ = ImGui.TableNextColumn();
                ImGui.Text($"({opt.Rect.Center.X:F0},{opt.Rect.Center.Y:F0})");
            }
            ImGui.EndTable();
        }
    }

    private void RenderAltarSection()
    {
        if (!ImGui.CollapsingHeader("Altar", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        AltarTelemetrySnapshot altar = snap.Altar;

        if (_settings.DebugShowAltarDetection.Value)
        {
            ImGui.TextColored(altar.ComponentCount > 0 ? CGreen : CMuted,
                $"Altar Components: {altar.ComponentCount}");

            if (altar.Components.Count > 0)
            {
                ImGui.Spacing();
                for (int i = 0; i < altar.Components.Count; i++)
                {
                    AltarComponentTelemetrySnapshot comp = altar.Components[i];
                    ImGui.TextColored(CInfo, $"Altar #{i + 1}:");
                    RenderAltarSectionTable("Top", comp.Top);
                    RenderAltarSectionTable("Bottom", comp.Bottom);
                }
            }
        }

        if (_settings.DebugShowAltarService.Value && altar.ServiceAvailable)
        {
            ImGui.Spacing();
            if (_settings.DebugShowAltarDetection.Value)
                ImGui.Separator();

            AltarServiceDebugTelemetrySnapshot d = altar.ServiceDebug;
            ImGui.TextColored(CHeader, "Altar Service:");

            ImGui.Spacing();
            InlineRow(
                ("Scan Exarch", d.LastScanExarchLabels.ToString(), CWhite),
                ("Scan Eater", d.LastScanEaterLabels.ToString(), CWhite),
                ("Elements Found", d.ElementsFound.ToString(), CWhite)
            );
            InlineRow(
                ("Processed", d.ComponentsProcessed.ToString(), CWhite),
                ("Added", d.ComponentsAdded.ToString(), CWhite),
                ("Duplicated", d.ComponentsDuplicated.ToString(), CWhite)
            );
            InlineRow(
                ("Matched", d.ModsMatched.ToString(), CGreen),
                ("Unmatched", d.ModsUnmatched.ToString(), d.ModsUnmatched > 0 ? CWarn : CMuted)
            );

            InlineRow(
                ("Altar Type", d.LastProcessedAltarType, CWhite),
                ("Last Scan", d.LastScanTime != DateTime.MinValue ? d.LastScanTime.ToString("HH:mm:ss") : "never", CMuted)
            );

            if (!string.IsNullOrEmpty(d.LastError))
                ImGui.TextColored(CError, $"Last Error: {d.LastError}");
        }
        else if (_settings.DebugShowAltarService.Value)
        {
            ImGui.TextColored(CError, "Altar Service: NULL");
        }
    }

    private static void RenderAltarSectionTable(string label, AltarModSectionTelemetrySnapshot section)
    {
        if (section.Upsides.Count == 0 && section.Downsides.Count == 0) return;
        ImGui.TextColored(CInfo, $"  {label} ({section.SectionName}):");
        if (ImGui.BeginTable($"Altar{label}Mods", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX))
        {
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 65f);
            ImGui.TableSetupColumn("Mods", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            if (section.Upsides.Count > 0)
            {
                ImGui.TableNextRow();
                _ = ImGui.TableNextColumn(); ImGui.Text("Upsides");
                _ = ImGui.TableNextColumn(); ImGui.Text(JoinModTexts(section.Upsides));
            }
            if (section.Downsides.Count > 0)
            {
                ImGui.TableNextRow();
                _ = ImGui.TableNextColumn(); ImGui.Text("Downsides");
                _ = ImGui.TableNextColumn(); ImGui.Text(JoinModTexts(section.Downsides));
            }
            ImGui.EndTable();
        }
    }

    private static string JoinModTexts(IReadOnlyList<AltarWeightedModTelemetrySnapshot> mods)
    {
        if (mods.Count == 0) return string.Empty;
        if (mods.Count == 1) return mods[0].Text;
        StringBuilder sb = new();
        for (int i = 0; i < mods.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(mods[i].Text);
        }
        return sb.ToString();
    }

    private void RenderHoveredItemSection()
    {
        if (!ImGui.CollapsingHeader("Hovered Item", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        HoveredItemMetadataTelemetrySnapshot h = snap.HoveredItem;

        ImGui.Spacing();
        InlineRow(
            ("Labels Available", BoolStr(h.LabelsAvailable), BoolColor(h.LabelsAvailable)),
            ("Cursor In Window", BoolStr(h.CursorInsideWindow), BoolColor(h.CursorInsideWindow)),
            ("Has Item", BoolStr(h.HasHoveredItem), BoolColor(h.HasHoveredItem))
        );

        if (h.HasHoveredItem)
        {
            ImGui.Spacing();
            ImGui.TextWrapped($"Item: {h.GroundItemName}");
            ImGui.TextWrapped($"Entity: {h.EntityPath}");
            ImGui.TextWrapped($"Metadata: {h.MetadataPath}");
        }
    }

    private void RenderInventorySection()
    {
        if (!ImGui.CollapsingHeader("Inventory", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DebugTelemetrySnapshot snap = _lastSnapshot ?? DebugTelemetrySnapshot.Empty;
        InventoryTelemetrySnapshot inv = snap.Inventory;

        InventoryDebugSnapshot id = inv.Inventory;
        if (!id.HasData)
        {
            ImGui.TextColored(CMuted, "No inventory debug data yet");
            return;
        }

        ImGui.TextColored(CInfo, $"Stage: {id.Stage}  Seq: {id.Sequence}");

        ImGui.Spacing();
        InlineRow(
            ("Full", BoolStr(id.InventoryFull), id.InventoryFull ? CWarn : CGreen),
            ("Slots", $"{id.OccupiedCells}/{id.CapacityCells}", CWhite),
            ("Allow Pickup", BoolStr(id.DecisionAllowPickup), BoolColor(id.DecisionAllowPickup))
        );

        if (!string.IsNullOrEmpty(id.GroundItemName))
            ImGui.TextWrapped($"Ground: {id.GroundItemName}");

        if (!string.IsNullOrEmpty(id.Notes))
            ImGui.TextColored(CMuted, $"Note: {id.Notes}");

        RenderTrail("Recent Stages", inv.InventoryTrail, 10);
    }

    private void RenderBlightSection()
    {
        if (_blight == null) return;
        if (!ImGui.CollapsingHeader("Blight", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        IReadOnlyList<string> stages = _blight.DebugStages;

        BlightPlan? plan = _blight.CurrentPlan;
        if (plan != null)
        {
            string summary = plan.DebugSummary;
            int bracketIdx = summary.IndexOf(" [", StringComparison.Ordinal);
            if (bracketIdx > 0)
            {
                ImGui.TextColored(CInfo, $"Plan v{plan.Version} ({summary[..bracketIdx]})");
                ImGui.PushTextWrapPos(0);
                ImGui.TextColored(CDim, $"{summary[bracketIdx..]}");
                ImGui.PopTextWrapPos();
            }
            else
            {
                ImGui.TextColored(CInfo, $"Plan v{plan.Version} ({summary})");
            }

            ImGui.TextColored(plan.IsComplete ? CGreen : CWarn, plan.IsComplete ? " Plan complete" : " Plan in progress");

            if (ImGui.BeginTable("PlanSteps", 5, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 52f);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 75f);
                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthFixed, 40f);
                ImGui.TableSetupColumn("Position", ImGuiTableColumnFlags.WidthFixed, 130f);
                ImGui.TableHeadersRow();

                int maxShow = Math.Min(plan.Steps.Count, 10);
                for (int i = 0; i < maxShow; i++)
                {
                    BlightPlanStep s = plan.Steps[i];
                    ImGui.TableNextRow();
                    _ = ImGui.TableNextColumn();
                    if (i == plan.CurrentStepIndex) ImGui.TextColored(CInfo, $"> {i + 1}");
                    else ImGui.Text($"  {i + 1}");
                    _ = ImGui.TableNextColumn();
                    ImGui.TextColored(s.IsSpecializationStep ? CSpecialAction : s.Action == BlightPlanAction.Build ? CBuildAction : CUpgradeAction,
                        s.ActionLabel);
                    _ = ImGui.TableNextColumn();
                    string targetName = _blight.GetStepTargetName(s);
                    ImGui.TextColored(BlightTowerColors.AsVector4(s.TowerType), targetName);
                    _ = ImGui.TableNextColumn();
                    ImGui.TextColored(CGreen, $"{s.TargetLevel}");
                    _ = ImGui.TableNextColumn();
                    ImGui.Text($"({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                }
                if (plan.Steps.Count > maxShow)
                {
                    ImGui.TableNextRow();
                    _ = ImGui.TableNextColumn();
                    ImGui.TextColored(CDim, $"... {plan.Steps.Count - maxShow} more");
                }
                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextColored(CMuted, "Plan not yet computed");
        }

        if (_blight.TowerEntities.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(CHeader, "Towers:");
            if (ImGui.BeginTable("TowerEntities", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX))
            {
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f);
                ImGui.TableSetupColumn("Pos", ImGuiTableColumnFlags.WidthFixed, 125f);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100f);
                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthFixed, 55f);
                ImGui.TableHeadersRow();

                for (int t = 0; t < _blight.TowerEntities.Count; t++)
                {
                    (Entity e, string tid) = _blight.TowerEntities[t];
                    BlightTowerType? mapped = BlightService.MapTowerIdToType(tid);
                    if (mapped == null) continue;
                    int level = BlightHelpers.DetectUpgradeRankFromEntityPath(e);

                    BlightTowerInfo? towerInfo = BlightTowerData.FindByDatId(tid);
                    string typeLabel = towerInfo is { Specialization: not TowerSpecialization.None } && level >= BlightTowerData.MaxUpgradeLevel && level >= 2
                        ? BlightTowerData.SpecDisplayName(towerInfo.Value)
                        : BlightTowerData.DisplayName(mapped.Value);

                    ImGui.TableNextRow();
                    _ = ImGui.TableNextColumn();
                    ImGui.Text(t.ToString());
                    _ = ImGui.TableNextColumn();
                    ImGui.Text($"({e.GridPosNum.X:F0},{e.GridPosNum.Y:F0})");
                    _ = ImGui.TableNextColumn();
                    ImGui.TextColored(BlightTowerColors.AsVector4(mapped.Value), typeLabel);
                    _ = ImGui.TableNextColumn();
                    ImGui.TextColored(level >= 3 ? CGreen : CWhite, $"{level}");
                }
                ImGui.EndTable();
            }
        }

        IReadOnlyList<BlightCachedTower> knownTowers = _blight.KnownTowers;
        int foundationCount = 0;
        for (int i = 0; i < knownTowers.Count; i++)
            if (knownTowers[i].UpgradeLevel == 0)
                foundationCount++;
        if (foundationCount > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(CHeader, $"Foundations: {foundationCount}");
            if (ImGui.BeginTable("BlightFoundations", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX))
            {
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f);
                ImGui.TableSetupColumn("Pos", ImGuiTableColumnFlags.WidthFixed, 125f);
                ImGui.TableSetupColumn("Planned", ImGuiTableColumnFlags.WidthFixed, 100f);
                ImGui.TableHeadersRow();

                int idx = 0;
                for (int i = 0; i < knownTowers.Count; i++)
                {
                    BlightCachedTower t = knownTowers[i];
                    if (t.UpgradeLevel > 0)
                        continue;
                    ImGui.TableNextRow();
                    _ = ImGui.TableNextColumn();
                    ImGui.Text(idx.ToString());
                    _ = ImGui.TableNextColumn();
                    ImGui.Text($"({t.WorldPosition.X:F0},{t.WorldPosition.Y:F0})");
                    _ = ImGui.TableNextColumn();
                    bool planned = t.PlannedTowerType != t.TowerType;
                    if (planned)
                        ImGui.TextColored(BlightTowerColors.AsVector4(t.PlannedTowerType), t.PlannedTowerType.ToString());
                    else
                        ImGui.TextColored(CDim, "—");
                    idx++;
                }
                ImGui.EndTable();
            }
        }

        try
        {
            LaneCoverageResult[]? coverage = _blight.TryGetCachedCoverage();
            if (coverage is { Length: > 0 })
            {
                ImGui.Spacing();
                int segTotal = 0, segCovered = 0, segUncovered = 0;
                foreach (LaneCoverageResult r in coverage)
                {
                    if (!BlightLaneTopology.IsRealLaneSegment(r))
                        continue;
                    segTotal++;
                    if (r.IsFullyCovered) segCovered++;
                    else segUncovered++;
                }
                ImGui.TextColored(CHeader, $"Coverage: {segCovered}/{segTotal} covered, {segUncovered} uncovered");
                ImGui.SameLine();
                if (ImGui.Button(_settings.BlightDebugShowLaneLabels.Value ? "Lane labels: ON" : "Lane labels: OFF"))
                    _settings.BlightDebugShowLaneLabels.Value = !_settings.BlightDebugShowLaneLabels.Value;

                if (!_covTreeHasCache
                    || !ReferenceEquals(coverage, _covTreeCoverage))
                {
                    _covTreeCoverage = coverage;
                    _covTreePositions.Clear();
                    _covTreeBranchData.Clear();
                    _covTreeChildren.Clear();
                    _covTreeForests.Clear();
                    _covTreeUnassigned.Clear();

                    (List<NumVector2> positions, List<(PumpBranch Branch, List<int> Segments)> branchData, List<int> unassignedSegments, List<List<int>>? children, List<List<BlightLaneNode>>? forests) = _blight.GetBranchDebug();
                    _covTreePositions.AddRange(positions);
                    _covTreeBranchData.AddRange(branchData);
                    _covTreeUnassigned.AddRange(unassignedSegments);
                    if (children != null && forests != null)
                    {
                        _covTreeChildren.AddRange(children);
                        _covTreeForests.AddRange(forests);
                    }
                    _covTreeHasCache = true;
                }
                RenderCoverageTree(coverage, _covTreePositions, _covTreeBranchData, _covTreeForests, _covTreeUnassigned);
            }
        }
        catch { }

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Tower Dat (radius table)"))
        {
            long now = Environment.TickCount64;
            if (now - _lastTowerDatDumpMs > 1000)
            {
                _lastTowerDatDump = _blight.DumpBlightTowerDat();
                _lastTowerDatDumpMs = now;
            }
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(CDim, _lastTowerDatDump ?? "(Blight dat not loaded)");
            ImGui.PopTextWrapPos();
        }

        RenderTrail("Recent Stages", stages, 20);

        RenderTrail("Executor Events", _blight.ExecutorEvents, 30);

        RenderBlightChestDebug();
    }

    private void RenderBlightChestDebug()
    {
        ElementTreeInspector? inspector = _blight?.BlightChestDebug;
        if (inspector == null)
            return;

        ElementTreeDebugUi.DrawSection(inspector, "Blight Chest Debug", ref _blightChestDebugSelectedIndex);
    }

    private void RenderCoverageTree(
        LaneCoverageResult[] coverage,
        List<NumVector2> positions,
        List<(PumpBranch Branch, List<int> Segments)> branchData,
        List<List<BlightLaneNode>> forests,
        List<int> unassigned)
    {
        if (branchData.Count == 0 && unassigned.Count == 0)
            return;

        bool havePositions = positions.Count == coverage.Length;
        IReadOnlySet<BlightTowerType> coverageTypes = BlightCoverageFlags.ForStrategy(_blight.CurrentStrategy);
        string Pt(NumVector2 p) => havePositions ? $"({p.X:F0},{p.Y:F0})" : "";
        string Flags(LaneCoverageResult r) => BlightCoverageFlags.Format(r, coverageTypes);
        NumVec4 SegTextColor(LaneCoverageResult seg)
        {
            if (seg.IsPhantom) return CWhite;
            Color c = _blight.CurrentStrategy.GetLaneColor(seg);
            return Vec4(new Color(c.R, c.G, c.B, (byte)255));
        }

        // Only the top-most branch headers are collapsed by default; their sub-lanes stay open.
        void RenderLane(BlightLaneNode lane)
        {
            if (lane.Segments.Count == 0)
            {
                for (int c = 0; c < lane.Children.Count; c++)
                    RenderLane(lane.Children[c]);
                return;
            }

            LaneCoverageResult first = coverage[lane.Segments[0]];
            LaneCoverageResult last = coverage[lane.Segments[lane.Segments.Count - 1]];
            LaneCoverageResult aggregate = BlightLaneTopology.AggregateLane(lane, coverage);
            ImGui.PushStyleColor(ImGuiCol.Text, SegTextColor(aggregate));
            string label = $"{lane.Name} {Pt(first.Midpoint)}->{Pt(last.Midpoint)} {Flags(aggregate)}";
            if (ImGui.TreeNodeEx($"{label}##covrow{lane.Name}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                for (int s = 0; s < lane.Segments.Count; s++)
                {
                    LaneCoverageResult seg = coverage[lane.Segments[s]];
                    ImGui.PushStyleColor(ImGuiCol.Text, SegTextColor(seg));
                    ImGui.TreeNodeEx(
                        $"{lane.Name}.{s + 1} {Pt(seg.Midpoint)} {Flags(seg)}##covseg{lane.Name}.{s}",
                        ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                    ImGui.PopStyleColor();
                }
                for (int c = 0; c < lane.Children.Count; c++)
                    RenderLane(lane.Children[c]);
                ImGui.TreePop();
            }
            ImGui.PopStyleColor();
        }

        for (int b = 0; b < branchData.Count; b++)
        {
            (PumpBranch branch, List<int> segments) = branchData[b];
            char branchLetter = (char)('A' + (b % 26));
            if (branch.CoverageSegment < 0)
            {
                ImGui.TreeNodeEx(
                    $"Branch {branchLetter} (cached) ({branch.Anchor.X:F0},{branch.Anchor.Y:F0})",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                continue;
            }

            if (ImGui.TreeNodeEx($"Branch {branchLetter} ({segments.Count} segs)##covbranch{branchLetter}"))
            {
                List<BlightLaneNode> forest = forests[b];
                for (int l = 0; l < forest.Count; l++)
                    RenderLane(forest[l]);
                ImGui.TreePop();
            }
        }

        // Lane segments no branch claims (incl. unattached chain heads) stay visible for diagnosis.
        if (unassigned.Count > 0)
        {
            if (ImGui.TreeNodeEx($"Unassigned ({unassigned.Count})##covunassigned"))
            {
                for (int u = 0; u < unassigned.Count; u++)
                {
                    int segment = unassigned[u];
                    LaneCoverageResult seg = coverage[segment];
                    NumVector2 pos = havePositions ? positions[segment] : seg.Midpoint;
                    ImGui.PushStyleColor(ImGuiCol.Text, SegTextColor(seg));
                    ImGui.TreeNodeEx(
                        $"U{u + 1} {Pt(pos)} {Flags(seg)}##covsegU{u}",
                        ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                    ImGui.PopStyleColor();
                }
                ImGui.TreePop();
            }
        }
    }

    private void CopyAllToClipboard()
    {
        // Build + copy on a background task so a big blight web never freezes the render thread.
        _ = System.Threading.Tasks.Task.Run(CopyAllToClipboardCore);
    }

    private void CopyAllToClipboardCore()
    {
        long start = Stopwatch.GetTimestamp();
        long allocStart = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                sb.AppendLine($"ClickIt Debug Information");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                // Copy All dumps every section regardless of which toggles are visible on screen, so the clipboard always carries a complete overview of the whole plugin state.
                AppendStatus(sb);
                AppendPerformance(sb);
                AppendErrors(sb);
                AppendClick(sb);
                AppendLabels(sb);
                AppendPathfinding(sb);
                AppendUltimatum(sb);
                AppendAltar(sb);
                AppendHoveredItem(sb);
                AppendInventory(sb);
                AppendBlight(sb);
                AppendHarvest(sb);
            }
            catch
            {
                // One unreadable section (e.g. an entity streamed out mid-read) must not drop the whole report; copy whatever was built before the failure.
            }

            _ = ClipboardText.TryCopy(sb.ToString());
        }
        finally
        {
            PerformanceMonitor? perf = _performanceMonitor;
            if (perf != null)
            {
                perf.RecordProcessingTiming(ProcessingSection.GameStateDump,
                    (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                perf.RecordAllocation(ProcessingSection.GameStateDump,
                    GC.GetAllocatedBytesForCurrentThread() - allocStart);
            }
        }
    }

    private void AppendStatus(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Status ---");
        StatusTelemetrySnapshot? s = _lastSnapshot?.Status;
        if (s == null) { sb.AppendLine("  (no data)"); sb.AppendLine(); return; }
        sb.AppendLine($"  Game Controller: {s.GameControllerAvailable}");
        sb.AppendLine($"  In Game: {s.InGame}");
        sb.AppendLine($"  Entity List Valid: {s.EntityListValid}");
        sb.AppendLine($"  Player Valid: {s.PlayerValid}");
        sb.AppendLine($"  Area: {s.CurrentAreaName}");
        sb.AppendLine($"  Items on Ground: {s.VisibleItemCount}");
        sb.AppendLine($"  Cached Labels: {(s.CachedLabelsAvailable ? s.CachedLabelCount.ToString() : "N/A")}");
        if (s.PlayerPositionAvailable)
            sb.AppendLine($"  Player Pos: ({s.PlayerPositionX:F1}, {s.PlayerPositionY:F1})");
        sb.AppendLine();
    }

    private void AppendPerformance(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Performance ---");
        PerformanceMetricsSnapshot perf = _lastPerformance;
        double fps = perf.Fps.Current;
        if (perf.Fps.Max > 0)
            sb.AppendLine($"  FPS: {perf.Fps.Current:F1} (avg: {perf.Fps.Average:F1}, max: {perf.Fps.Max:F1})");
        sb.AppendLine($"  Memory: {FormatMemoryMb(perf.Memory.ProcessWorkingSetMb)} (managed {FormatMemoryMb(perf.Memory.ManagedHeapMb)}, gen2 {FormatMemoryMb(perf.Memory.Gen2Mb)}, loh {FormatMemoryMb(perf.Memory.LohMb)}, frag {FormatMemoryMb(perf.Memory.FragmentedMb)}, load {perf.Memory.MemoryLoadPercent:F0}%)");
        sb.AppendLine($"  GC Pause: last={perf.Memory.GcPauseLastMs:F0}ms avg={perf.Memory.GcPauseAvgMs:F0}ms max={perf.Memory.GcPauseMaxMs:F0}ms time%={perf.Memory.GcPauseTimePercent:F1}");
        if (perf.Render.SampleCount > 0)
        {
            sb.AppendLine($"  Render: {perf.Render.LastMs:F0} ms (avg: {perf.Render.AverageMs:F2}, max: {perf.Render.MaxMs:F0})");
            AppendTimingTotalLine(sb, "  Render Total", perf.RenderTableTotal);
            AppendTimingLine(sb, "    Altar", perf.GetRenderSection(RenderSection.AltarOverlay));
            AppendTimingLine(sb, "    Blight", perf.GetRenderSection(RenderSection.BlightOverlay));
            AppendTimingLine(sb, "    Click.Hotkey", perf.GetRenderSection(RenderSection.ClickHotkeyToggle));
            AppendTimingLine(sb, "    Debug", perf.GetRenderSection(RenderSection.DebugOverlay));
            AppendTimingLine(sb, "    Flush.Frame", perf.GetRenderSection(RenderSection.FrameFlush));
            AppendTimingLine(sb, "    Flush.Text", perf.GetRenderSection(RenderSection.TextFlush));
            AppendTimingLine(sb, "    Harvest", perf.GetRenderSection(RenderSection.HarvestOverlay));
            AppendTimingLine(sb, "    Inv.Full", perf.GetRenderSection(RenderSection.InventoryFullWarning));
            AppendTimingLine(sb, "    Lazy", perf.GetRenderSection(RenderSection.LazyMode));
            AppendTimingLine(sb, "    Pathfinding", perf.GetRenderSection(RenderSection.PathfindingOverlay));
            AppendTimingLine(sb, "    Perf.Overlay", perf.GetRenderSection(RenderSection.PerformanceOverlay));
            AppendTimingLine(sb, "    Strongbox", perf.GetRenderSection(RenderSection.StrongboxOverlay));
            AppendTimingLine(sb, "    UI.Rect", perf.GetRenderSection(RenderSection.UiRegionRectangle));
            AppendTimingLine(sb, "    Ultimatum", perf.GetRenderSection(RenderSection.UltimatumOverlay));
        }
        RenderingTelemetrySnapshot? r = _lastSnapshot?.Rendering;
        if (r != null)
            sb.AppendLine($"  Queue: text={r.PendingTextCount}, frames={r.PendingFrameCount}");
        if (perf.ProcessingTotal.SampleCount > 0)
        {
            AppendTimingTotalLine(sb, "  Processing Total (ms/click)", perf.ProcessingTotal);
            AppendRunTimingLine(sb, "    Altar", perf.GetProcessingSection(ProcessingSection.Altar));
            AppendBreakdownTimingLines(sb, perf, ProcessingSection.Altar);
            AppendRunTimingLine(sb, "    Area.Blocked", perf.GetProcessingSection(ProcessingSection.AreaBlockedUi));
            AppendRunTimingLine(sb, "    Blight", perf.GetProcessingSection(ProcessingSection.Blight));
            AppendBreakdownTimingLines(sb, perf, ProcessingSection.Blight);
            AppendRunTimingLine(sb, "    Click", perf.GetProcessingSection(ProcessingSection.Click));
            AppendClickBreakdownTimingLines(sb, perf);
            AppendClickFrequencyTargetLines(sb, perf);
            AppendRunTimingLine(sb, "    Dump", perf.GetProcessingSection(ProcessingSection.GameStateDump));
            AppendRunTimingLine(sb, "    Flare", perf.GetProcessingSection(ProcessingSection.Flare));
            AppendBreakdownTimingLines(sb, perf, ProcessingSection.Flare);
            AppendRunTimingLine(sb, "    Harvest", perf.GetProcessingSection(ProcessingSection.Harvest));
            AppendRunTimingLine(sb, "    Label Scan", perf.GetProcessingSection(ProcessingSection.Label));
            AppendRunTimingLine(sb, "    Manual Hover", perf.GetProcessingSection(ProcessingSection.ManualUiHover));
            AppendRunTimingLine(sb, "    Pathfinding", perf.GetProcessingSection(ProcessingSection.Pathfinding));
            AppendBreakdownTimingLines(sb, perf, ProcessingSection.Pathfinding);
            AppendRunTimingLine(sb, "    Strongbox", perf.GetProcessingSection(ProcessingSection.Strongbox));
            AppendBreakdownTimingLines(sb, perf, ProcessingSection.Strongbox);
            AppendRunTimingLine(sb, "    Ultimatum", perf.GetProcessingSection(ProcessingSection.Ultimatum));
        }
        if (perf.Allocations != null && perf.Allocations.Count > 0)
        {
            sb.AppendLine("  GC Allocations:");
            AppendGcTotalLine(sb, "    Total", perf);
            foreach (ProcessingSection section in Enum.GetValues<ProcessingSection>())
            {
                if (section == ProcessingSection.Unknown)
                    continue;
                GcAllocationSnapshot stats = perf.GetAllocationSection(section);
                if (stats.SampleCount > 0)
                {
                    AppendGcLine(sb, $"    {section}", stats, fps);
                    AppendBreakdownLines(sb, perf, section);
                }
            }
            LabelScanAllocationStats labelScan = perf.LabelScanAllocation;
            if (labelScan.SampleCount > 0)
            {
                double labelPeriodMs = perf.GetAllocationSection(ProcessingSection.Label).AvgPeriodMs;
                sb.AppendLine("    Label Scan breakdown:");
                AppendGcStageLine(sb, "      ListRead", labelScan.ListRead, labelPeriodMs);
                AppendGcStageLine(sb, "      ListAlloc", labelScan.ListAlloc, labelPeriodMs);
                AppendGcStageLine(sb, "      Validity", labelScan.Validity, labelPeriodMs);
                AppendGcStageLine(sb, "      Sort", labelScan.Sort, labelPeriodMs);
            }
            ClickAllocationStats click = perf.ClickAllocation;
            if (click.SampleCount > 0)
            {
                double clickPeriodMs = perf.GetAllocationSection(ProcessingSection.Click).AvgPeriodMs;
                sb.AppendLine("    Click breakdown:");
                AppendGcStageLine(sb, "      Context", click.Context, clickPeriodMs);
                AppendGcStageLine(sb, "      Acquire", click.Acquire, clickPeriodMs);
                AppendGcStageLine(sb, "      Rank", click.Rank, clickPeriodMs);
                AppendGcStageLine(sb, "      Execute", click.Execute, clickPeriodMs);
                AppendGcStageLine(sb, "      Post", click.Post, clickPeriodMs);
                AppendGcStageLine(sb, "      Other", click.Other, clickPeriodMs);
            }
        }
        if (perf.CoroutinesTotal.SampleCount > 0)
        {
            TimingMetricsSnapshot frameTotal = perf.CoroutinesTotalPerFrameSnapshot;
            TimingMetricsSnapshot runTotal = perf.CoroutinesTotal;
            sb.AppendLine($"  Coroutines: {frameTotal.LastMs:F2} ms/frame (avg: {frameTotal.AverageMs:F2}, max: {frameTotal.MaxMs:F2}) | ms/run: {runTotal.LastMs:F0} (avg: {runTotal.AverageMs:F1}, max: {runTotal.MaxMs:F0})");
        }
        AppendCoroLine(sb, "  Altar Coroutine", perf.AltarCoroutine, perf.Fps.Current);
        AppendCoroLine(sb, "  Blight Coroutine", perf.BlightCoroutine, perf.Fps.Current);
        AppendCoroLine(sb, "  Click Coroutine", perf.ClickCoroutine, perf.Fps.Current);
        AppendCoroLine(sb, "  Flare Coroutine", perf.FlareCoroutine, perf.Fps.Current);
        AppendCoroLine(sb, "  Label Overlay Coroutine", perf.LabelOverlayCoroutine, perf.Fps.Current);
        AppendCoroLine(sb, "  Ultimatum Coroutine", perf.UltimatumCoroutine, perf.Fps.Current);

        sb.AppendLine();
    }

    private static void AppendRunTimingLine(StringBuilder sb, string label, TimingMetricsSnapshot stats)
    {
        if (stats.SampleCount <= 0)
            return;
        sb.AppendLine($"{label}: {stats.LastMs:F1}/{stats.AverageMs:F1}/{stats.MaxMs:F1} ms/click");
    }

    private static void AppendClickFrequencyTargetLines(StringBuilder sb, PerformanceMetricsSnapshot perf)
    {
        if (perf.ClickTargetIntervalMs <= 0)
            return;
        sb.AppendLine($"    Click Frequency: {perf.ClickTargetIntervalMs:F0}");
        AppendRunTimingLine(sb, "      Processing", perf.GetProcessingSection(ProcessingSection.Click));
        AppendRunTimingLine(sb, "      Sleep", perf.ClickSleepTiming);
        AppendRunTimingLine(sb, "      Total", perf.ClickCoroutine);
    }

    private static void AppendClickTimingLine(StringBuilder sb, string label, TimingStageSnapshot s)
    {
        if (s.AvgMs <= 0 && s.MaxMs <= 0)
            return;
        sb.AppendLine($"{label}: {s.LastMs:F1} ms (avg {s.AvgMs:F1}, max {s.MaxMs:F1})");
    }

    // Per-section sub-stage ALLOCATION breakdown for the heavier areas (rendered under GC Allocations).
    private static void AppendBreakdownLines(StringBuilder sb, PerformanceMetricsSnapshot perf, ProcessingSection section)
    {
        if (perf.Breakdowns == null || !perf.Breakdowns.TryGetValue(section, out BreakdownStats stats) || stats.SampleCount == 0)
            return;
        double periodMs = perf.GetAllocationSection(section).AvgPeriodMs;
        sb.AppendLine($"    {section} breakdown:");
        foreach (BreakdownStageSnapshot stage in stats.Stages)
        {
            if (stage.Allocation.AvgBytesPerRun <= 0 && stage.Allocation.MaxBytesPerRun <= 0)
                continue;
            AppendGcStageLine(sb, $"      {stage.Name}", stage.Allocation, periodMs);
        }
    }

    // Per-section sub-stage TIME breakdown for the heavier areas (rendered under Processing Total).
    private static void AppendBreakdownTimingLines(StringBuilder sb, PerformanceMetricsSnapshot perf, ProcessingSection section)
    {
        if (perf.Breakdowns == null || !perf.Breakdowns.TryGetValue(section, out BreakdownStats stats) || stats.SampleCount == 0)
            return;
        foreach (BreakdownStageSnapshot stage in stats.Stages)
        {
            if (stage.Time.AvgMs <= 0 && stage.Time.MaxMs <= 0)
                continue;
            AppendClickTimingLine(sb, $"      {stage.Name}", stage.Time);
        }
    }

    private static void AppendClickBreakdownTimingLines(StringBuilder sb, PerformanceMetricsSnapshot perf)
    {
        ClickAllocationStats click = perf.ClickAllocation;
        if (click.SampleCount == 0)
            return;
        AppendClickTimingLine(sb, "      Context", click.ContextTime);
        AppendClickTimingLine(sb, "      Acquire", click.AcquireTime);
        AppendClickTimingLine(sb, "      Rank", click.RankTime);
        AppendClickTimingLine(sb, "      Execute", click.ExecuteTime);
        AppendClickTimingLine(sb, "      Post", click.PostTime);
    }

    private static void AppendTimingLine(System.Text.StringBuilder sb, string label, TimingMetricsSnapshot stats)
    {
        sb.AppendLine($"{label}: last={stats.LastMs:F2} avg={stats.AverageMs:F2} max={stats.MaxMs:F2}");
    }

    private static void AppendTimingTotalLine(System.Text.StringBuilder sb, string label, TimingMetricsSnapshot stats)
    {
        if (stats.SampleCount <= 0)
            return;
        sb.AppendLine($"{label}: last={stats.LastMs:F2} avg={stats.AverageMs:F2} max={stats.MaxMs:F2} ms");
    }

    // Processing rows: per-frame cost (scaled by FPS) followed by the raw per-run cost.
    private static void AppendGcStageLine(System.Text.StringBuilder sb, string label, AllocationStageSnapshot stats, double periodMs)
    {
        double allocPerSecond = periodMs > 0 ? stats.AvgBytesPerRun * 1000.0 / periodMs : 0;
        sb.AppendLine($"{label}: {FormatAllocRate(allocPerSecond)} (avg {FormatBytes(stats.AvgBytesPerRun)}/run, last {FormatBytes(stats.LastBytesPerRun)}/run, max {FormatAllocRate(stats.MaxAllocPerSecond)}, max run {FormatBytes(stats.MaxBytesPerRun)})");
    }

    private static void AppendGcTotalLine(System.Text.StringBuilder sb, string label, PerformanceMetricsSnapshot perf)
    {
        (_, double totalPerSecond, _) = perf.GcTableTotalBytesPerFrame;
        if (totalPerSecond <= 0)
            return;
        double fps = perf.Fps.Current;
        sb.AppendLine($"{label}: {FormatBytes(totalPerSecond / fps)}/f | {FormatAllocRate(totalPerSecond)} | max {FormatAllocRate(perf.GcTableTotalMaxBytesPerSecond)}");
    }

    private static void AppendGcLine(System.Text.StringBuilder sb, string label, GcAllocationSnapshot stats, double fps)
    {
        if (stats.SampleCount <= 0)
            return;
        string perFrame = fps > 0 ? FormatBytes(stats.AllocPerSecond / fps) : "-";
        sb.AppendLine($"{label}: {perFrame}/f | {FormatAllocRate(stats.AllocPerSecond)} | avg {FormatBytes(stats.AvgBytesPerRun)}/run | max {FormatAllocRate(stats.MaxAllocPerSecond)} | max run {FormatBytes(stats.MaxBytesPerRun)}");
    }

    private static void AppendCoroLine(System.Text.StringBuilder sb, string label, TimingMetricsSnapshot stats, double fps)
    {
        double scale = stats.PerFrameScale(fps);
        if (scale > 0)
            sb.AppendLine($"{label}: {stats.LastMs * scale:F2}/{stats.AverageMs * scale:F2}/{stats.MaxMs * scale:F2} ms/frame | {stats.LastMs:F0}/{stats.AverageMs:F1}/{stats.MaxMs:F0} ms/run ({stats.DutyCyclePercent:F1}%)");
        else
            sb.AppendLine($"{label}: {stats.LastMs:F0}/{stats.AverageMs:F1}/{stats.MaxMs:F0} ms/run");
    }

    private void AppendErrors(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Recent Errors ---");
        IReadOnlyList<string>? errors = _lastSnapshot?.Errors.RecentErrors;
        if (errors == null || errors.Count == 0)
        {
            sb.AppendLine("  No recent errors");
        }
        else
        {
            sb.AppendLine($"  Error Count: {errors.Count}");
            int start = Math.Max(0, errors.Count - 3);
            for (int i = start; i < errors.Count; i++)
                sb.AppendLine($"  {errors[i]}");
        }
        sb.AppendLine();
    }

    private void AppendClick(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Click ---");
        if (_lastSnapshot != null)
        {
            ClickTelemetrySnapshot click = _lastSnapshot.Click;
            sb.AppendLine($"  Service Available: {click.ServiceAvailable}");
            sb.AppendLine("  Settings:");
            foreach (string line in click.Settings.SummaryLines)
                sb.AppendLine($"    {line}");

            ClickDebugSnapshot c = click.Click;
            if (c.HasData)
            {
                sb.AppendLine($"  Stage: {c.Stage} Seq: {c.Sequence}");
                sb.AppendLine($"  Mechanic: {c.MechanicId}");
                sb.AppendLine($"  Distance: {c.Distance:F1}");
                sb.AppendLine($"  Entity: {c.EntityPath}");
                sb.AppendLine($"  World Raw: ({c.WorldScreenRaw.X:F1},{c.WorldScreenRaw.Y:F1})");
                sb.AppendLine($"  World Abs: ({c.WorldScreenAbsolute.X:F1},{c.WorldScreenAbsolute.Y:F1})");
                sb.AppendLine($"  Click Pos: ({c.ResolvedClickPoint.X:F1},{c.ResolvedClickPoint.Y:F1})");
                sb.AppendLine($"  Center InWnd/Clickable: {c.CenterInWindow}/{c.CenterClickable}");
                sb.AppendLine($"  Resolved InWnd/Clickable: {c.ResolvedInWindow}/{c.ResolvedClickable}");
                sb.AppendLine($"  Resolved: {c.Resolved} Note: {c.Notes}");
            }
            AppendTrailSb(sb, "  Runtime Log", click.RuntimeLogTrail, 30);
            AppendTrailSb(sb, "  Recent Stages", click.ClickTrail, 100);
        }
        sb.AppendLine();
    }

    private void AppendLabels(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Labels ---");
        if (_lastSnapshot != null)
        {
            LabelTelemetrySnapshot label = _lastSnapshot.Label;
            sb.AppendLine($"  Service Available: {label.ServiceAvailable}");
            sb.AppendLine($"  Total Visible: {label.TotalVisibleLabels}");
            sb.AppendLine($"  Valid Labels: {label.ValidVisibleLabels}");
            LabelDebugSnapshot ld = label.Label;
            if (ld.HasData)
            {
                sb.AppendLine($"  Stage: {ld.Stage} Seq: {ld.Sequence}");
                sb.AppendLine($"  Range: {ld.StartIndex}-{ld.EndExclusive} Total: {ld.TotalLabels}");
                sb.AppendLine($"  Considered: {ld.ConsideredCandidates}");
                sb.AppendLine($"  Selected: {ld.SelectedMechanicId}");
                if (!string.IsNullOrEmpty(ld.Notes))
                    sb.AppendLine($"  Note: {ld.Notes}");
            }
            AppendTrailSb(sb, "  Recent Stages", label.LabelTrail, 100);
        }
        sb.AppendLine();
    }

    private void AppendPathfinding(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Pathfinding ---");
        if (_lastSnapshot != null)
        {
            PathfindingTelemetrySnapshot pf = _lastSnapshot.Pathfinding;
            sb.AppendLine($"  Service Available: {pf.ServiceAvailable}");
            if (pf.ServiceAvailable)
            {
                PathfindingDebugSnapshot data = pf.Pathfinding;
                sb.AppendLine($"  Terrain Loaded: {data.TerrainLoaded}");
                sb.AppendLine($"  Grid: {data.AreaWidth} x {data.AreaHeight}");
                sb.AppendLine($"  Expanded Nodes: {data.LastExpandedNodes}");
                sb.AppendLine($"  Path Length: {data.LastPathLength}");
                sb.AppendLine($"  Compute: {data.LastComputeMs} ms");
                sb.AppendLine($"  Goal Mode: {(data.LastGoalResolutionUsedFallback ? "Fallback" : "Direct")}");
                sb.AppendLine($"  Start=({data.LastStart.X},{data.LastStart.Y}) Req=({data.LastRequestedGoal.X},{data.LastRequestedGoal.Y}) Res=({data.LastResolvedGoal.X},{data.LastResolvedGoal.Y})");
                if (!string.IsNullOrWhiteSpace(data.LastGoalResolutionNote))
                    sb.AppendLine($"  Goal Note: {data.LastGoalResolutionNote}");
                if (!string.IsNullOrWhiteSpace(data.LastFailureReason))
                    sb.AppendLine($"  Failure: {data.LastFailureReason}");
                sb.AppendLine($"  Target Path: {data.LastTargetPath ?? "<none>"}");
            }

            OffscreenMovementDebugSnapshot offscreen = pf.OffscreenMovement;
            if (offscreen.HasData)
            {
                sb.AppendLine("  Offscreen Movement:");
                sb.AppendLine($"    Stage: {offscreen.Stage} built={offscreen.BuiltPath} fromPath={offscreen.ResolvedFromPath} clickPoint={offscreen.ResolvedClickPoint}");
                if (!string.IsNullOrWhiteSpace(offscreen.MovementSkillDebug))
                    sb.AppendLine($"    Skill: {offscreen.MovementSkillDebug}");
                sb.AppendLine($"    Target: {offscreen.TargetPath}");
                Vector2 td = offscreen.TargetScreen - offscreen.WindowCenter;
                Vector2 cd = offscreen.ClickScreen - offscreen.WindowCenter;
                sb.AppendLine($"    Grid P=({offscreen.PlayerGrid.X:F0},{offscreen.PlayerGrid.Y:F0}) T=({offscreen.TargetGrid.X:F0},{offscreen.TargetGrid.Y:F0})");
                sb.AppendLine($"    Target Delta=({td.X:F1},{td.Y:F1}) dir={ToCompass(td)}");
                sb.AppendLine($"    Click Delta=({cd.X:F1},{cd.Y:F1}) dir={ToCompass(cd)}");
                sb.AppendLine($"    Center=({offscreen.WindowCenter.X:F1},{offscreen.WindowCenter.Y:F1}) Target=({offscreen.TargetScreen.X:F1},{offscreen.TargetScreen.Y:F1}) Click=({offscreen.ClickScreen.X:F1},{offscreen.ClickScreen.Y:F1})");
            }
            AppendTrailSb(sb, "  Recent Stages", pf.OffscreenMovementTrail, 40);
            AppendTrailSb(sb, "  Recent Events", pf.RecentEvents, 40);
        }
        sb.AppendLine();
    }

    private void AppendUltimatum(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Ultimatum ---");
        if (_lastSnapshot != null)
        {
            ClickTelemetrySnapshot click = _lastSnapshot.Click;
            sb.AppendLine($"  Enabled Initial/Other: {click.Settings.InitialUltimatumClickEnabled}/{click.Settings.OtherUltimatumClickEnabled}");
            UltimatumDebugSnapshot u = click.Ultimatum;
            if (u.HasData)
            {
                sb.AppendLine($"  Stage: {u.Stage} Seq: {u.Sequence} Source: {u.Source}");
                sb.AppendLine($"  Panel Visible: {u.IsPanelVisible} GG Active: {u.IsGruelingGauntletActive}");
                sb.AppendLine($"  Saturated: {u.HasSaturatedChoice} Modifier: {u.SaturatedModifier}");
                sb.AppendLine($"  TakeReward: {u.ShouldTakeReward} Action: {u.Action}");
                sb.AppendLine($"  Candidates: {u.CandidateCount} Saturated: {u.SaturatedCandidateCount}");
                sb.AppendLine($"  Best: {u.BestModifier} (priority={u.BestPriority})");
                sb.AppendLine($"  Clicked Choice/Confirm/Reward: {u.ClickedChoice}/{u.ClickedConfirm}/{u.ClickedTakeRewards}");
                if (!string.IsNullOrEmpty(u.Notes))
                    sb.AppendLine($"  Note: {u.Notes}");
            }
            AppendTrailSb(sb, "  Recent Stages", click.UltimatumTrail, 40);
        }
        sb.AppendLine();
    }

    private void AppendAltar(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Altar ---");
        if (_lastSnapshot != null)
        {
            AltarTelemetrySnapshot altar = _lastSnapshot.Altar;
            sb.AppendLine($"  Components: {altar.ComponentCount}");
            for (int i = 0; i < altar.Components.Count; i++)
            {
                AltarComponentTelemetrySnapshot comp = altar.Components[i];
                sb.AppendLine($"  Altar #{i + 1}:");
                sb.AppendLine($"    Top ({comp.Top.SectionName}):");
                if (comp.Top.Upsides.Count > 0) sb.AppendLine($"      Upsides: {JoinModTexts(comp.Top.Upsides)}");
                if (comp.Top.Downsides.Count > 0) sb.AppendLine($"      Downsides: {JoinModTexts(comp.Top.Downsides)}");
                sb.AppendLine($"    Bottom ({comp.Bottom.SectionName}):");
                if (comp.Bottom.Upsides.Count > 0) sb.AppendLine($"      Upsides: {JoinModTexts(comp.Bottom.Upsides)}");
                if (comp.Bottom.Downsides.Count > 0) sb.AppendLine($"      Downsides: {JoinModTexts(comp.Bottom.Downsides)}");
            }
            if (altar.ServiceAvailable)
            {
                AltarServiceDebugTelemetrySnapshot d = altar.ServiceDebug;
                sb.AppendLine($"  Last Scan Exarch/Eater: {d.LastScanExarchLabels}/{d.LastScanEaterLabels}");
                sb.AppendLine($"  Elements: {d.ElementsFound}");
                sb.AppendLine($"  Components: {d.ComponentsProcessed}+{d.ComponentsAdded}+{d.ComponentsDuplicated}");
                sb.AppendLine($"  Mods Matched/Unmatched: {d.ModsMatched}/{d.ModsUnmatched}");
                sb.AppendLine($"  Last Altar Type: {d.LastProcessedAltarType}");
                if (!string.IsNullOrEmpty(d.LastError))
                    sb.AppendLine($"  Last Error: {d.LastError}");
            }
        }
        sb.AppendLine();
    }

    private void AppendHoveredItem(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Hovered Item ---");
        if (_lastSnapshot != null)
        {
            HoveredItemMetadataTelemetrySnapshot h = _lastSnapshot.HoveredItem;
            sb.AppendLine($"  Labels Available: {h.LabelsAvailable}");
            sb.AppendLine($"  Cursor Inside Window: {h.CursorInsideWindow}");
            sb.AppendLine($"  Has Hovered Item: {h.HasHoveredItem}");
            if (h.HasHoveredItem)
            {
                sb.AppendLine($"  Item: {h.GroundItemName}");
                sb.AppendLine($"  Entity: {h.EntityPath}");
                sb.AppendLine($"  Metadata: {h.MetadataPath}");
            }
        }
        sb.AppendLine();
    }

    private void AppendInventory(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Inventory ---");
        if (_lastSnapshot != null)
        {
            InventoryTelemetrySnapshot inv = _lastSnapshot.Inventory;
            InventoryDebugSnapshot id = inv.Inventory;
            if (id.HasData)
            {
                sb.AppendLine($"  Stage: {id.Stage} Seq: {id.Sequence}");
                sb.AppendLine($"  Full: {id.InventoryFull} Slots: {id.OccupiedCells}/{id.CapacityCells}");
                sb.AppendLine($"  Ground: {id.GroundItemName}");
                sb.AppendLine($"  Decision Allow Pickup: {id.DecisionAllowPickup}");
                if (!string.IsNullOrEmpty(id.Notes))
                    sb.AppendLine($"  Note: {id.Notes}");
            }
            AppendTrailSb(sb, "  Recent Stages", inv.InventoryTrail, 50);
        }
        sb.AppendLine();
    }

    private void AppendBlight(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Blight ---");
        if (_blight == null) { sb.AppendLine("(unavailable)"); sb.AppendLine(); return; }

        BlightPlan? plan = _blight.CurrentPlan;
        if (plan != null)
        {
            sb.AppendLine($"Plan v{plan.Version} ({plan.Steps.Count} steps, {(plan.IsComplete ? "complete" : "in progress")})");
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                BlightPlanStep s = plan.Steps[i];
                string marker = i == plan.CurrentStepIndex ? ">" : " ";
                string action = s.ActionLabel;
                sb.AppendLine($"  {marker}[{i + 1}] {action} {_blight.GetStepTargetName(s)} lvl{s.TargetLevel} ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
            }
        }

        Entity? pumpEntity = _blight.PumpEntity;
        if (pumpEntity != null)
        {
            sb.AppendLine($"Pump: grid=({pumpEntity.GridPosNum.X:F0},{pumpEntity.GridPosNum.Y:F0}) world=({pumpEntity.PosNum.X:F0},{pumpEntity.PosNum.Y:F0})");
        }
        sb.AppendLine(_blight.DumpPumpStateMachine());
        sb.Append(_blight.DumpPathwayDebug());
        sb.Append(_blight.DumpPathwayIconDebug());
        sb.Append(_blight.DumpBranchRootDebug());
        sb.Append(_blight.DumpBlightServerLanes());

        IReadOnlyList<BlightCachedTower> knownTowers = _blight.KnownTowers;
        bool hasFoundation = false;
        for (int i = 0; i < knownTowers.Count; i++)
            if (knownTowers[i].UpgradeLevel == 0) { hasFoundation = true; break; }
        if (hasFoundation)
        {
            sb.AppendLine("Foundations:");
            for (int i = 0; i < knownTowers.Count; i++)
            {
                BlightCachedTower t = knownTowers[i];
                if (t.UpgradeLevel > 0)
                    continue;
                string planned = t.PlannedTowerType != t.TowerType ? $" -> {t.PlannedTowerType}" : "";
                sb.AppendLine($"  ({t.WorldPosition.X:F0},{t.WorldPosition.Y:F0}){planned}");
            }
        }

        try
        {
            LaneCoverageResult[]? coverage = _blight.TryGetCachedCoverage();
            if (coverage is { Length: > 0 })
            {
                int realSegments = 0;
                for (int c = 0; c < coverage.Length; c++)
                    if (BlightLaneTopology.IsRealLaneSegment(coverage[c]))
                        realSegments++;
                sb.AppendLine($"Coverage: {realSegments} segments");

                (List<NumVector2>? positions, List<(PumpBranch Branch, List<int> Segments)>? branchData, List<int>? unassignedSegments, List<List<int>>? children, List<List<BlightLaneNode>>? forests) = _blight.GetBranchDebug();
                if (branchData != null && branchData.Count > 0 && forests != null)
                {
                    bool havePositions = positions != null && positions.Count == coverage.Length;
                    IReadOnlySet<BlightTowerType> coverageTypes = BlightCoverageFlags.ForStrategy(_blight.CurrentStrategy);
                    string Pt(NumVector2 p) => havePositions ? $"({p.X:F0},{p.Y:F0})" : "";

                    void AppendLane(BlightLaneNode lane, int depth)
                    {
                        string indent = new(' ', depth * 2);
                        if (lane.Segments.Count > 0)
                        {
                            LaneCoverageResult first = coverage[lane.Segments[0]];
                            LaneCoverageResult last = coverage[lane.Segments[lane.Segments.Count - 1]];
                            LaneCoverageResult aggregate = BlightLaneTopology.AggregateLane(lane, coverage);
                            sb.AppendLine($"{indent}{lane.Name} {Pt(first.Midpoint)}->{Pt(last.Midpoint)} {BlightCoverageFlags.Format(aggregate, coverageTypes)}");
                            for (int s = 0; s < lane.Segments.Count; s++)
                            {
                                LaneCoverageResult seg = coverage[lane.Segments[s]];
                                sb.AppendLine($"{indent}  {lane.Name}.{s + 1} {Pt(seg.Midpoint)} {BlightCoverageFlags.Format(seg, coverageTypes)}");
                            }
                        }
                        for (int c = 0; c < lane.Children.Count; c++)
                            AppendLane(lane.Children[c], depth + 1);
                    }

                    for (int b = 0; b < branchData.Count; b++)
                    {
                        (PumpBranch branch, List<int> segments) = branchData[b];
                        char branchLetter = (char)('A' + (b % 26));
                        sb.AppendLine(branch.CoverageSegment >= 0
                            ? $"Branch {branchLetter} ({segments.Count} segs)"
                            : $"Branch {branchLetter} (cached) ({branch.Anchor.X:F0},{branch.Anchor.Y:F0})");
                        if (branch.CoverageSegment >= 0)
                        {
                            List<BlightLaneNode> forest = forests[b];
                            for (int l = 0; l < forest.Count; l++)
                                AppendLane(forest[l], 1);

                            HashSet<int> rendered = BlightLaneTopology.CollectLaneSegments(forest);
                            bool firstUnmapped = true;
                            for (int s = 0; s < segments.Count; s++)
                            {
                                if (rendered.Contains(segments[s]))
                                    continue;
                                if (BlightLaneTopology.IsStackedOnRenderedLane(segments[s], coverage, rendered))
                                    continue; // stacked duplicate merged into a rendered lane
                                if (firstUnmapped)
                                {
                                    sb.AppendLine("  UNMAPPED segments (topology anomaly):");
                                    firstUnmapped = false;
                                }
                                LaneCoverageResult seg = coverage[segments[s]];
                                sb.AppendLine($"    seg {segments[s]} {Pt(seg.Midpoint)} par={seg.ParentIndex} {BlightCoverageFlags.Format(seg, coverageTypes)}");
                            }
                        }
                    }

                    if (unassignedSegments is { Count: > 0 })
                    {
                        sb.AppendLine($"  Unassigned ({unassignedSegments.Count} segs):");
                        for (int u = 0; u < unassignedSegments.Count; u++)
                        {
                            int segment = unassignedSegments[u];
                            LaneCoverageResult seg = coverage[segment];
                            NumVector2 pos = havePositions ? positions![segment] : seg.Midpoint;
                            sb.AppendLine($"    U{u + 1} {Pt(pos)} {BlightCoverageFlags.Format(seg, coverageTypes)}");
                        }
                    }
                }
            }
        }
        catch { }

        sb.Append(_blight.DumpBlightTowerDat());
        sb.AppendLine();

        IReadOnlyList<string> stages = _blight.DebugStages;
        if (stages.Count > 0)
        {
            sb.AppendLine("Recent Stages:");
            int stageStart = Math.Max(0, stages.Count - 60);
            for (int i = stageStart; i < stages.Count; i++)
                sb.AppendLine($"  {stages[i]}");
        }

        IReadOnlyList<string> executorEvents = _blight.ExecutorEvents;
        if (executorEvents.Count > 0)
        {
            sb.AppendLine("Executor Events (phase/menu trail):");
            int eventStart = Math.Max(0, executorEvents.Count - 100);
            for (int i = eventStart; i < executorEvents.Count; i++)
                sb.AppendLine($"  {executorEvents[i]}");
        }

        sb.AppendLine("Blight Chest Debug (latest):");
        ElementTreeDebugUi.AppendToDump(sb, _blight.BlightChestDebug);

        sb.AppendLine();
    }

    private void AppendHarvest(System.Text.StringBuilder sb)
    {
        sb.AppendLine("--- Harvest ---");
        if (_harvest == null) { sb.AppendLine("(unavailable)"); sb.AppendLine(); return; }

        sb.AppendLine($"  Settings: click={_settings.ClickHarvest.Value} showEst={_settings.ShowHarvestLifeforceEstimation.Value} higher={_settings.ClickHigherHarvestEstimate.Value} debug={_settings.DebugShowHarvest.Value}");

        IReadOnlyList<HarvestPlotEstimate> estimates = _harvest.CurrentEstimates;
        sb.AppendLine($"  Estimates: {estimates.Count}");
        for (int i = 0; i < estimates.Count; i++)
        {
            HarvestPlotEstimate e = estimates[i];
            string path = DynamicAccess.TryGetDynamicValue(e.Label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                && DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : "?";
            bool boundsEmpty = e.LabelBounds == RectangleF.Empty || e.LabelBounds.IsEmpty;
            sb.AppendLine($"    [{i}] {path} seeds={e.SeedRows.Count} est={e.EstimatedLifeforce:F1} bounds={(boundsEmpty ? "EMPTY" : $"({e.LabelBounds.X:F0},{e.LabelBounds.Y:F0},{e.LabelBounds.Width:F0}x{e.LabelBounds.Height:F0})")}");
        }

        HarvestDecision decision = _harvest.CurrentDecision;
        string chosen = decision.ChosenLabel != null
            ? (DynamicAccess.TryGetDynamicValue(decision.ChosenLabel, DynamicAccessProfiles.ItemOnGround, out object? rawChosen)
                && DynamicAccess.TryReadString(rawChosen, DynamicAccessProfiles.Path, out string chosenPath)
                ? chosenPath
                : "set")
            : "null";
        sb.AppendLine($"  Decision: {decision.Outcome} chosen={chosen} top={decision.TopEstimate:F1} bottom={decision.BottomEstimate:F1} blocked={decision.IsHarvestClickBlocked}");
        sb.AppendLine();
    }

    private static NumVec4 ColorToVec4(Color c)
        => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    private static NumVec4 Vec4(Color c) => ColorToVec4(c);

    private static void SectionAccent()
    {
        ImGui.Spacing();
        ImGui.Separator();
    }

    private static string BoolStr(bool v) => v ? "Yes" : "No";

    private static NumVec4 BoolColor(bool v) => v ? CGreen : CError;

    private bool TryLoadSplitterWidth(out float width)
    {
        width = 0f;
        string raw = _settings.DebugWindowSplitterWidth.Value;
        return !string.IsNullOrWhiteSpace(raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out width)
            && width > 0f;
    }

    private static void AppendTrailSb(System.Text.StringBuilder sb, string header, IReadOnlyList<string> trail, int maxRows)
    {
        if (trail.Count == 0) return;
        sb.AppendLine($"{header}:");
        int start = Math.Max(0, trail.Count - maxRows);
        for (int i = start; i < trail.Count; i++)
            sb.AppendLine($"  {trail[i]}");
    }

    private string? _lastTowerDatDump;
    private long _lastTowerDatDumpMs;

    // Coverage-tree render cache rebuilt only when the underlying coverage reference changes.
    private LaneCoverageResult[]? _covTreeCoverage;
    private readonly List<NumVector2> _covTreePositions = [];
    private readonly List<(PumpBranch Branch, List<int> Segments)> _covTreeBranchData = [];
    private readonly List<List<int>> _covTreeChildren = [];
    private readonly List<List<BlightLaneNode>> _covTreeForests = [];
    private readonly List<int> _covTreeUnassigned = [];
    private bool _covTreeHasCache;

    private static string TrimPath(string? path)
        => string.IsNullOrWhiteSpace(path) ? "<none>" : path.Length <= 80 ? path : path[..77] + "...";

    private static string ToCompass(SharpDX.Vector2 delta)
    {
        float angle = (float)(Math.Atan2(delta.Y, delta.X) * 180.0 / Math.PI);
        angle = (angle + 360f) % 360f;
        string dir = angle switch
        {
            < 22.5f or >= 337.5f => "E",
            < 67.5f => "NE",
            < 112.5f => "N",
            < 157.5f => "NW",
            < 202.5f => "W",
            < 247.5f => "SW",
            < 292.5f => "S",
            _ => "SE"
        };
        return $"{dir} ({angle:F0})";
    }
}
