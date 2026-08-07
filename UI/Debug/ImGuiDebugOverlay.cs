using ImGuiNET;
using System.Globalization;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;
using ClickIt.Features.Observability;
using ClickIt.Features.Blight;
using ClickIt.Features.Blight.Data;
using ClickIt.Features.Blight.Planning;
using ClickIt.Features.Pathfinding.Diagnostics;
using ClickIt.Features.Labels.Inventory;
using ClickIt.Shared.Diagnostics;

namespace ClickIt.UI.Debug;

internal sealed class ImGuiDebugOverlay(
    ClickItSettings settings,
    PerformanceMonitor? performanceMonitor = null,
    BlightService? blight = null,
    IDebugTelemetrySource? telemetrySource = null)
{
    private const string WindowTitle = "ClickIt Debug Info";
    private const float WindowMinWidth = 1240f;
    private const float WindowMinHeight = 400f;
    private static readonly NumVec2 DefaultPosN = new(100f, 80f);

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

    private readonly ClickItSettings _settings = settings;
    private readonly PerformanceMonitor? _performanceMonitor = performanceMonitor;
    private readonly BlightService? _blight = blight;
    private readonly IDebugTelemetrySource? _telemetrySource = telemetrySource;
    private DebugTelemetrySnapshot? _lastSnapshot;
    private PerformanceMetricsSnapshot _lastPerformance;
    private float _leftColWidth;
    private bool _windowPosApplied;
    private float _savedWinX;
    private float _savedWinY;
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

        if (!_windowPosApplied)
        {
            ImGui.SetNextWindowPos(TryLoadWindowPosition(out float winX, out float winY)
                ? new NumVec2(winX, winY)
                : DefaultPosN);
            _windowPosApplied = true;
        }

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

        PersistWindowPosition();

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
        Span<char> buffer = stackalloc char[text.Length];
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

        // Two independent columns: the summary lines are pinned to a fixed X so a changing value
        // width never shifts the opposite side, while the fixed-fit tables sit directly adjacent
        // (SameLine) with just the default item spacing between them.
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

        RenderRenderTable(perf);
        ImGui.SameLine(0, 0);
        ImGui.SetCursorPosX(summaryColX);
        RenderProcessingTable(perf);

        ImGui.Spacing();
        double coroutineFps = perf.Fps.Current;
        BeginFixedTable("CoroutinesPerFrame", "CR ms/f", 175f, "Last", 38f, "Avg", 38f, "Max", 38f);
        RenderTimingTotalRow("Total", perf.CoroutinesTotalPerFrameSnapshot);
        RenderScaledTimingRow("Altar", perf.AltarCoroutine, coroutineFps);
        RenderScaledTimingRow("Blight", perf.BlightCoroutine, coroutineFps);
        RenderScaledTimingRow("Click", perf.ClickCoroutine, coroutineFps);
        RenderScaledTimingRow("Flare", perf.FlareCoroutine, coroutineFps);
        RenderScaledTimingRow("Label Overlay", perf.LabelOverlayCoroutine, coroutineFps);
        RenderScaledTimingRow("Ultimatum", perf.UltimatumCoroutine, coroutineFps);
        ImGui.EndTable();
        ImGui.SameLine(0, 0);
        ImGui.SetCursorPosX(summaryColX);
        RenderGcTable(perf);

        ImGui.Spacing();
        bool hasFreqTarget = perf.ClickTargetIntervalMs > 0 || perf.AverageClickIntervalMs > 0;
        if (hasFreqTarget)
            RenderClickFrequencyTarget(perf, hasCoroutines: true);
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
        BeginFixedTable("RenderBreakdown", "Render", 95f, "Last", 38f, "Avg", 38f, "Max", 38f);

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
        double fps = perf.Fps.Current;
        BeginFixedTable("ProcessingBreakdown", "Proc ms/f", 110f, "Last", 38f, "Avg", 38f, "Max", 38f);

        RenderTimingTotalRow("Total", perf.ProcessingTotalPerFrameSnapshot);
        RenderScaledTimingRow("Altar", perf.GetProcessingSection(ProcessingSection.Altar), fps);
        RenderScaledTimingRow("Area.Blocked", perf.GetProcessingSection(ProcessingSection.AreaBlockedUi), fps);
        RenderScaledTimingRow("Blight", perf.GetProcessingSection(ProcessingSection.Blight), fps);
        RenderScaledTimingRow("Click", perf.GetProcessingSection(ProcessingSection.Click), fps);
        RenderScaledTimingRow("Flare", perf.GetProcessingSection(ProcessingSection.Flare), fps);
        RenderScaledTimingRow("Harvest", perf.GetProcessingSection(ProcessingSection.Harvest), fps);
        RenderScaledTimingRow("Label Scan", perf.GetProcessingSection(ProcessingSection.Label), fps);
        RenderScaledTimingRow("Manual Hover", perf.GetProcessingSection(ProcessingSection.ManualUiHover), fps);
        RenderScaledTimingRow("Pathfinding", perf.GetProcessingSection(ProcessingSection.Pathfinding), fps);
        RenderScaledTimingRow("Strongbox", perf.GetProcessingSection(ProcessingSection.Strongbox), fps);
        RenderScaledTimingRow("Ultimatum", perf.GetProcessingSection(ProcessingSection.Ultimatum), fps);
        ImGui.EndTable();
    }

    private static void RenderGcTable(PerformanceMetricsSnapshot perf)
    {
        double fps = perf.Fps.Current;
        BeginFixedTable("GcBreakdown", "GC", 110f, "KB/f", 60f, "KB/s", 90f, "Max", 60f);

        RenderGcTotalRow(perf);
        RenderGcRow("Altar", perf.GetAllocationSection(ProcessingSection.Altar), fps);
        RenderGcRow("Area.Blocked", perf.GetAllocationSection(ProcessingSection.AreaBlockedUi), fps);
        RenderGcRow("Blight", perf.GetAllocationSection(ProcessingSection.Blight), fps);
        RenderGcRow("Click", perf.GetAllocationSection(ProcessingSection.Click), fps);
        RenderClickBreakdown(perf);
        RenderGcRow("Flare", perf.GetAllocationSection(ProcessingSection.Flare), fps);
        RenderGcRow("Harvest", perf.GetAllocationSection(ProcessingSection.Harvest), fps);
        RenderGcRow("Label Scan", perf.GetAllocationSection(ProcessingSection.Label), fps);
        RenderLabelScanBreakdown(perf);
        RenderGcRow("Manual Hover", perf.GetAllocationSection(ProcessingSection.ManualUiHover), fps);
        RenderGcRow("Pathfinding", perf.GetAllocationSection(ProcessingSection.Pathfinding), fps);
        RenderGcRow("Strongbox", perf.GetAllocationSection(ProcessingSection.Strongbox), fps);
        RenderGcRow("Ultimatum", perf.GetAllocationSection(ProcessingSection.Ultimatum), fps);
        ImGui.EndTable();
    }

    private static void RenderLabelScanBreakdown(PerformanceMetricsSnapshot perf)
    {
        LabelScanAllocationStats s = perf.LabelScanAllocation;
        if (s.SampleCount == 0)
            return;
        double fps = perf.Fps.Current;
        double periodMs = perf.GetAllocationSection(ProcessingSection.Label).AvgPeriodMs;
        RenderGcStageRow("Validity", s.Validity, fps, periodMs);
        RenderGcStageRow("Sort", s.Sort, fps, periodMs);
    }

    private static void RenderClickBreakdown(PerformanceMetricsSnapshot perf)
    {
        ClickAllocationStats s = perf.ClickAllocation;
        if (s.SampleCount == 0)
            return;
        double fps = perf.Fps.Current;
        double periodMs = perf.GetAllocationSection(ProcessingSection.Click).AvgPeriodMs;
        RenderGcStageRow("Context", s.Context, fps, periodMs);
        RenderGcStageRow("Acquire", s.Acquire, fps, periodMs);
        RenderGcStageRow("Rank", s.Rank, fps, periodMs);
        RenderGcStageRow("Execute", s.Execute, fps, periodMs);
        RenderGcStageRow("Post", s.Post, fps, periodMs);
        RenderGcStageRow("Other", s.Other, fps, periodMs);
    }

    private static void RenderGcStageRow(string label, AllocationStageSnapshot s, double fps, double periodMs)
    {
        if (s.AvgBytesPerRun <= 0 && s.MaxBytesPerRun <= 0)
            return;
        double allocPerSecond = periodMs > 0 ? s.AvgBytesPerRun * 1000.0 / periodMs : 0;
        double mbPerSecond = 1024.0 * 1024.0;
        NumVec4 c = allocPerSecond >= 15 * mbPerSecond ? CError : allocPerSecond >= 6 * mbPerSecond ? CWarn : CGreen;
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text($"  {label}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, fps > 0 ? FormatBytes(allocPerSecond / fps) : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, FormatAllocRate(allocPerSecond));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, FormatBytes(s.MaxBytesPerRun));
    }

    private static void RenderGcRow(string label, GcAllocationSnapshot stats, double fps)
    {
        double mbPerSecond = 1024.0 * 1024.0;
        NumVec4 c = stats.AllocPerSecond >= 15 * mbPerSecond ? CError : stats.AllocPerSecond >= 6 * mbPerSecond ? CWarn : CGreen;
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, stats.SampleCount > 0 && fps > 0 ? FormatBytes(stats.AllocPerSecond / fps) : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, stats.SampleCount > 0 ? FormatAllocRate(stats.AllocPerSecond) : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, stats.SampleCount > 0 ? FormatBytes(stats.MaxBytesPerRun) : "-");
    }

    // Table-wide timing totals as the first data row: last/avg/max summed across every row beneath
    // (max is the combination of all maxes, not the single worst).
    private static void RenderTimingTotalRow(string label, TimingMetricsSnapshot total)
    {
        bool hasData = total.SampleCount > 0;
        NumVec4 c = total.AverageMs <= 6.94 ? CGreen : total.AverageMs <= 16.67 ? CWarn : CError;
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.TextColored(CHeader, label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, hasData ? $"{total.LastMs:F2}" : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, hasData ? $"{total.AverageMs:F2}" : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, hasData ? $"{total.MaxMs:F2}" : "-");
    }

    // Table-wide GC totals as the first data row: KB/f, KB/s and Max summed across every row beneath.
    private static void RenderGcTotalRow(PerformanceMetricsSnapshot perf)
    {
        (_, double avgKf, double maxKf) = perf.GcTableTotalBytesPerFrame;
        double mb = 1024.0 * 1024.0;
        double allocPerSecond = avgKf * perf.Fps.Current;
        NumVec4 c = allocPerSecond >= 15 * mb ? CError : allocPerSecond >= 6 * mb ? CWarn : CGreen;
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.TextColored(CHeader, "Total");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, FormatBytes(avgKf));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, FormatAllocRate(allocPerSecond));
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, FormatBytes(maxKf));
    }

    private static string FormatMemoryMb(double mb)
        => mb >= 1024.0 ? $"{mb / 1024.0:F1} GB" : $"{mb:F0} MB";

    private static NumVec4 SizeColor(double mb)
        => mb > 2048 ? CError : mb > 1228.8 ? CWarn : CGreen;

    private static NumVec4 FragmentationColor(double mb)
        => mb > 400 ? CError : mb > 100 ? CWarn : CGreen;

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

    private static void RenderScaledTimingRow(string label, TimingMetricsSnapshot stats, double fps)
    {
        double scale = stats.PerFrameScale(fps);
        double avg = stats.AverageMs * scale;
        NumVec4 c = avg <= 6.94 ? CGreen : avg <= 16.67 ? CWarn : CError;
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, scale > 0 ? $"{stats.LastMs * scale:F2}" : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, scale > 0 ? $"{avg:F2}" : "-");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, scale > 0 ? $"{stats.MaxMs * scale:F2}" : "-");
    }

    private static void RenderTimingRow(string label, TimingMetricsSnapshot stats)
    {
        NumVec4 c = stats.AverageMs <= 6.94 ? CGreen : stats.AverageMs <= 16.67 ? CWarn : CError;
        ImGui.TableNextRow();
        _ = ImGui.TableNextColumn(); ImGui.Text(label);
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, $"{stats.LastMs:F2}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, $"{stats.AverageMs:F2}");
        _ = ImGui.TableNextColumn(); ImGui.TextColored(c, $"{stats.MaxMs:F2}");
    }

    private readonly record struct FrequencyTargetModel(
        double TargetMs,
        double ProcessingMs,
        double DelayMs,
        double ModeledTotalMs,
        double ObservedTotalMs,
        double SchedulerDeltaMs,
        double Deviation,
        string TargetStatus);

    private static FrequencyTargetModel BuildFrequencyTargetModel(PerformanceMetricsSnapshot perf, ClickFrequencyTargetTelemetrySnapshot? freqTarget)
    {
        double targetMs = freqTarget is { SettingsAvailable: true } ? freqTarget.TargetIntervalMs : perf.ClickTargetIntervalMs;
        double processingMs = perf.ClickCoroutine.AverageMs;
        if (processingMs <= 0) processingMs = perf.AverageSuccessfulClickTimingMs;
        double observedMs = perf.AverageClickIntervalMs;

        double delayMs = Math.Max(0, targetMs - processingMs);
        double modeledTotalMs = delayMs + processingMs;
        double observedTotalMs = observedMs > 0 ? observedMs : modeledTotalMs;
        double schedulerDeltaMs = observedTotalMs - modeledTotalMs;
        double deviation = observedTotalMs > 0 ? (observedTotalMs - targetMs) / targetMs : 0;

        string targetStatus = deviation <= 0.10
            ? (observedMs > 0 ? "meeting target" : "estimating")
            : "not meeting target";

        return new FrequencyTargetModel(targetMs, processingMs, delayMs, modeledTotalMs, observedTotalMs, schedulerDeltaMs, deviation, targetStatus);
    }

    private void RenderClickFrequencyTarget(PerformanceMetricsSnapshot perf, bool hasCoroutines = true)
    {
        ClickFrequencyTargetTelemetrySnapshot freqTarget = _lastSnapshot.Click.FrequencyTarget;
        FrequencyTargetModel m = BuildFrequencyTargetModel(perf, freqTarget);

        if (!hasCoroutines) { ImGui.BeginGroup(); }
        if (ImGui.BeginTable("FreqTarget", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX))
        {
            ImGui.TableSetupColumn("Click Frequency Target", ImGuiTableColumnFlags.WidthFixed, 180f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 165f);
            ImGui.TableHeadersRow();

            FreqTargetRow("Target", $"{m.TargetMs:F0} ms{(freqTarget.ShowLazyModeTarget ? " (Lazy)" : "")}", CWarn);
            FreqTargetRow("Processing", $"{m.ProcessingMs:F0} ms", m.ProcessingMs > m.TargetMs ? CError : m.ProcessingMs >= m.TargetMs * 0.75 ? CWarn : CGreen);
            FreqTargetRow("Total (model)", $"{m.ModeledTotalMs:F0} ms", m.Deviation <= 0.05 ? CGreen : m.Deviation <= 0.10 ? CWarn : CError);
            FreqTargetRow("Scheduler", $"{m.SchedulerDeltaMs:+0;-0;0} ms", Math.Abs(m.SchedulerDeltaMs) <= 5 ? CGreen : Math.Abs(m.SchedulerDeltaMs) <= 20 ? CWarn : COrangeRed);
            FreqTargetRow("Observed", $"{m.ObservedTotalMs:F0} ms ({m.TargetStatus})", m.Deviation <= 0.05 ? CGreen : m.Deviation <= 0.10 ? CWarn : CError);
            ImGui.EndTable();
        }
        if (!hasCoroutines) { ImGui.EndGroup(); }
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
                ImGui.TextColored(CDim, $" {summary[bracketIdx..]}");
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
                    ImGui.Text(s.Action == BlightPlanAction.Build ? "BUILD" : "UPGRADE");
                    _ = ImGui.TableNextColumn();
                    string targetName = _blight.GetStepTargetName(s);
                    NumVec4 typeColor = s.TowerType switch
                    {
                        BlightTowerType.Chilling => Vec4(new Color(50, 130, 255)),
                        BlightTowerType.Seismic => CWarn,
                        BlightTowerType.Fireball => Vec4(new Color(200, 60, 60)),
                        _ => CWhite
                    };
                    ImGui.TextColored(typeColor, targetName);
                    _ = ImGui.TableNextColumn();
                    ImGui.Text($"lvl{s.TargetLevel}");
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

            if (plan.CurrentStep != null)
                ImGui.TextColored(CWarn, $"Current step: {plan.CurrentStepIndex + 1}/{plan.Steps.Count}");
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

                    string spec = "";
                    if (level >= BlightTowerData.MaxUpgradeLevel && level >= 2)
                    {
                        BlightTowerInfo? info = BlightTowerData.FindByDatId(tid);
                        if (info is { Specialization: not TowerSpecialization.None })
                            spec = info.Value.Name;
                    }

                    string typeLabel = spec.Length > 0 ? $"{mapped.Value} ({spec})" : mapped.Value.ToString();

                    ImGui.TableNextRow();
                    _ = ImGui.TableNextColumn();
                    ImGui.Text(t.ToString());
                    _ = ImGui.TableNextColumn();
                    ImGui.Text($"({e.GridPosNum.X:F0},{e.GridPosNum.Y:F0})");
                    _ = ImGui.TableNextColumn();
                    NumVec4 tc = mapped.Value switch
                    {
                        BlightTowerType.Chilling => Vec4(SharpDX.Color.DodgerBlue),
                        BlightTowerType.Seismic => CWarn,
                        _ => CWhite
                    };
                    ImGui.TextColored(tc, typeLabel);
                    _ = ImGui.TableNextColumn();
                    ImGui.TextColored(level >= 3 ? CGreen : CWhite, $"lvl{level}");
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
                    ImGui.TextColored(planned ? CInfo : CDim, planned ? t.PlannedTowerType.ToString() : "—");
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

                    (List<NumVector2> positions, List<(PumpBranch Branch, List<int> Segments)> branchData) = _blight.GetBranchDebug();
                    _covTreePositions.AddRange(positions);
                    _covTreeBranchData.AddRange(branchData);
                    if (branchData.Count > 0)
                    {
                        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
                        _covTreeChildren.AddRange(children);
                        for (int b = 0; b < branchData.Count; b++)
                        {
                            (PumpBranch branch, List<int> segments) = branchData[b];
                            if (branch.CoverageSegment < 0)
                            {
                                _covTreeForests.Add([]);
                                continue;
                            }
                            _covTreeForests.Add(BlightLaneTopology.BuildBranchLaneForest(
                                coverage, children, segments, branch.CoverageSegment, ((char)('A' + (b % 26))).ToString()));
                        }
                    }
                    _covTreeHasCache = true;
                }
                RenderCoverageTree(coverage, _covTreePositions, _covTreeBranchData, _covTreeForests);
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
        List<List<BlightLaneNode>> forests)
    {
        if (branchData.Count == 0)
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

        void RenderLane(BlightLaneNode lane)
        {
            if (lane.Segments.Count > 0)
            {
                LaneCoverageResult first = coverage[lane.Segments[0]];
                LaneCoverageResult last = coverage[lane.Segments[lane.Segments.Count - 1]];
                LaneCoverageResult aggregate = BlightLaneTopology.AggregateLane(lane, coverage);
                ImGui.PushStyleColor(ImGuiCol.Text, SegTextColor(aggregate));
                string label = $"{lane.Name} {Pt(first.Midpoint)}->{Pt(last.Midpoint)} {Flags(aggregate)}";
                if (lane.Children.Count > 0)
                {
                    // A lane with divergences renders as a dropdown with its child lanes nested
                    // inside it (not as flat "Divergence" siblings below a leaf).
                    if (ImGui.TreeNodeEx($"{label}##covrow{lane.Name}", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        for (int c = 0; c < lane.Children.Count; c++)
                            RenderLane(lane.Children[c]);
                        ImGui.TreePop();
                    }
                }
                else
                {
                    ImGui.TreeNodeEx($"{label}##covrow{lane.Name}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                }
                ImGui.PopStyleColor();
            }
            else
            {
                for (int c = 0; c < lane.Children.Count; c++)
                    RenderLane(lane.Children[c]);
            }
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

            if (ImGui.TreeNodeEx($"Branch {branchLetter} ({segments.Count} segs)##covbranch{branchLetter}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                List<BlightLaneNode> forest = forests[b];
                for (int l = 0; l < forest.Count; l++)
                    RenderLane(forest[l]);
                ImGui.TreePop();
            }
        }
    }

    private void CopyAllToClipboard()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ClickIt Debug Information");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (_settings.DebugShowStatus.Value) AppendStatus(sb);
        if (_settings.DebugShowPerformance.Value) AppendPerformance(sb);
        if (_settings.DebugShowRecentErrors.Value) AppendErrors(sb);
        if (_settings.DebugShowClicking.Value) AppendClick(sb);
        if (_settings.DebugShowLabels.Value) AppendLabels(sb);
        if (_settings.DebugShowPathfinding.Value) AppendPathfinding(sb);
        if (_settings.DebugShowUltimatum.Value) AppendUltimatum(sb);
        if (_settings.DebugShowAltarDetection.Value || _settings.DebugShowAltarService.Value)
            AppendAltar(sb);
        if (_settings.DebugShowHoveredItemMetadata.Value) AppendHoveredItem(sb);
        if (_settings.DebugShowInventoryPickup.Value) AppendInventory(sb);
        if (_settings.DebugShowBlight.Value) AppendBlight(sb);

        try { ImGui.SetClipboardText(sb.ToString()); }
        catch { }
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
        if (perf.Fps.Max > 0)
            sb.AppendLine($"  FPS: {perf.Fps.Current:F1} (avg: {perf.Fps.Average:F1}, max: {perf.Fps.Max:F1})");
        sb.AppendLine($"  Memory: {FormatMemoryMb(perf.Memory.ProcessWorkingSetMb)} (managed {FormatMemoryMb(perf.Memory.ManagedHeapMb)}, gen2 {FormatMemoryMb(perf.Memory.Gen2Mb)}, loh {FormatMemoryMb(perf.Memory.LohMb)}, frag {FormatMemoryMb(perf.Memory.FragmentedMb)}, load {perf.Memory.MemoryLoadPercent:F0}%)");
        if (perf.Render.SampleCount > 0)
        {
            sb.AppendLine($"  Render: {perf.Render.LastMs:F0} ms (avg: {perf.Render.AverageMs:F2}, max: {perf.Render.MaxMs:F0})");
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
            TimingMetricsSnapshot processing = perf.ProcessingTotal;
            sb.AppendLine($"  Processing: {processing.LastMs:F0} ms (avg: {processing.AverageMs:F2}, max: {processing.MaxMs:F0})");
            AppendTimingLine(sb, "    Altar", perf.GetProcessingSection(ProcessingSection.Altar));
            AppendTimingLine(sb, "    Area.Blocked", perf.GetProcessingSection(ProcessingSection.AreaBlockedUi));
            AppendTimingLine(sb, "    Blight", perf.GetProcessingSection(ProcessingSection.Blight));
            AppendTimingLine(sb, "    Click", perf.GetProcessingSection(ProcessingSection.Click));
            AppendTimingLine(sb, "    Flare", perf.GetProcessingSection(ProcessingSection.Flare));
            AppendTimingLine(sb, "    Harvest", perf.GetProcessingSection(ProcessingSection.Harvest));
            AppendTimingLine(sb, "    Label Scan", perf.GetProcessingSection(ProcessingSection.Label));
            AppendTimingLine(sb, "    Manual Hover", perf.GetProcessingSection(ProcessingSection.ManualUiHover));
            AppendTimingLine(sb, "    Pathfinding", perf.GetProcessingSection(ProcessingSection.Pathfinding));
            AppendTimingLine(sb, "    Strongbox", perf.GetProcessingSection(ProcessingSection.Strongbox));
            AppendTimingLine(sb, "    Ultimatum", perf.GetProcessingSection(ProcessingSection.Ultimatum));
        }
        if (perf.Allocations != null && perf.Allocations.Count > 0)
        {
            sb.AppendLine("  GC Allocations:");
            foreach (ProcessingSection section in Enum.GetValues<ProcessingSection>())
            {
                if (section == ProcessingSection.Unknown)
                    continue;
                GcAllocationSnapshot stats = perf.GetAllocationSection(section);
                if (stats.SampleCount > 0)
                    sb.AppendLine($"    {section}: {FormatAllocRate(stats.AllocPerSecond)} (avg {FormatBytes(stats.AvgBytesPerRun)}/run, max {FormatBytes(stats.MaxBytesPerRun)})");
            }
            LabelScanAllocationStats labelScan = perf.LabelScanAllocation;
            if (labelScan.SampleCount > 0)
            {
                double labelPeriodMs = perf.GetAllocationSection(ProcessingSection.Label).AvgPeriodMs;
                sb.AppendLine("    Label Scan breakdown:");
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

        if (perf.ClickTargetIntervalMs > 0 || perf.AverageClickIntervalMs > 0)
        {
            sb.AppendLine("  Click Frequency Target:");
            ClickFrequencyTargetTelemetrySnapshot? freqTarget = _lastSnapshot?.Click?.FrequencyTarget;
            FrequencyTargetModel m = BuildFrequencyTargetModel(perf, freqTarget);
            sb.AppendLine($"    Target: {m.TargetMs:F0} ms{(freqTarget?.ShowLazyModeTarget == true ? " (Lazy)" : "")}");
            sb.AppendLine($"    Delay: {m.DelayMs:F0} ms");
            sb.AppendLine($"    Processing: {m.ProcessingMs:F0} ms");
            sb.AppendLine($"    Total (model): {m.ModeledTotalMs:F0} ms");
            sb.AppendLine($"    Scheduler: {m.SchedulerDeltaMs:+0;-0;0} ms");
            sb.AppendLine($"    Observed: {m.ObservedTotalMs:F0} ms");
        }
        sb.AppendLine();
    }

    private static void AppendTimingLine(System.Text.StringBuilder sb, string label, TimingMetricsSnapshot stats)
    {
        sb.AppendLine($"{label}: last={stats.LastMs:F2} avg={stats.AverageMs:F2} max={stats.MaxMs:F2}");
    }

    private static void AppendGcStageLine(System.Text.StringBuilder sb, string label, AllocationStageSnapshot stats, double periodMs)
    {
        double allocPerSecond = periodMs > 0 ? stats.AvgBytesPerRun * 1000.0 / periodMs : 0;
        sb.AppendLine($"{label}: {FormatAllocRate(allocPerSecond)} (avg {FormatBytes(stats.AvgBytesPerRun)}/run, last {FormatBytes(stats.LastBytesPerRun)}/run, max {FormatBytes(stats.MaxBytesPerRun)})");
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
            AppendTrailSb(sb, "  Runtime Log", click.RuntimeLogTrail, 10);
            AppendTrailSb(sb, "  Recent Stages", click.ClickTrail, 15);
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
            AppendTrailSb(sb, "  Recent Stages", label.LabelTrail, 15);
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
            AppendTrailSb(sb, "  Recent Stages", pf.OffscreenMovementTrail, 15);
            AppendTrailSb(sb, "  Recent Events", pf.RecentEvents, 20);
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
            AppendTrailSb(sb, "  Recent Stages", click.UltimatumTrail, 10);
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
            AppendTrailSb(sb, "  Recent Stages", inv.InventoryTrail, 10);
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
                string action = s.Action == BlightPlanAction.Build ? "BUILD" : "UPGRADE";
                sb.AppendLine($"  {marker}[{i + 1}] {action} {_blight.GetStepTargetName(s)} lvl{s.TargetLevel} ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
            }
        }

        Entity? pumpEntity = _blight.PumpEntity;
        if (pumpEntity != null)
        {
            sb.AppendLine($"Pump: grid=({pumpEntity.GridPosNum.X:F0},{pumpEntity.GridPosNum.Y:F0}) world=({pumpEntity.PosNum.X:F0},{pumpEntity.PosNum.Y:F0})");
        }
        sb.Append(_blight.DumpPathwayDebug());
        sb.Append(_blight.DumpBranchRootDebug());

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

                (List<NumVector2>? positions, List<(PumpBranch Branch, List<int> Segments)>? branchData) = _blight.GetBranchDebug();
                if (branchData != null && branchData.Count > 0)
                {
                    List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
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
                            List<BlightLaneNode> forest = BlightLaneTopology.BuildBranchLaneForest(
                                coverage, children, segments, branch.CoverageSegment, branchLetter.ToString());
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
            int stageStart = Math.Max(0, stages.Count - 20);
            for (int i = stageStart; i < stages.Count; i++)
                sb.AppendLine($"  {stages[i]}");
        }

        sb.AppendLine("Blight Chest Debug (latest):");
        ElementTreeDebugUi.AppendToDump(sb, _blight.BlightChestDebug);

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

    private bool TryLoadWindowPosition(out float x, out float y)
    {
        x = 0f;
        y = 0f;
        string raw = _settings.DebugWindowPosition.Value;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        int comma = raw.IndexOf(',');
        if (comma <= 0 || comma == raw.Length - 1)
            return false;
        return float.TryParse(raw.AsSpan(0, comma), NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            && float.TryParse(raw.AsSpan(comma + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out y);
    }

    private bool TryLoadSplitterWidth(out float width)
    {
        width = 0f;
        string raw = _settings.DebugWindowSplitterWidth.Value;
        return !string.IsNullOrWhiteSpace(raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out width)
            && width > 0f;
    }

    private void PersistWindowPosition()
    {
        NumVec2 pos = ImGui.GetWindowPos();
        if (SystemMath.Abs(pos.X - _savedWinX) < 1f && SystemMath.Abs(pos.Y - _savedWinY) < 1f)
            return;
        _savedWinX = pos.X;
        _savedWinY = pos.Y;
        _settings.DebugWindowPosition.Value = $"{pos.X:F0},{pos.Y:F0}";
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

    // Coverage-tree render cache: the blight cache only refreshes on its own 200ms cadence, and
    // the topology derivation (branch search + lane forest) is expensive on large maps, so it is
    // rebuilt only when the underlying coverage reference changes.
    private LaneCoverageResult[]? _covTreeCoverage;
    private readonly List<NumVector2> _covTreePositions = [];
    private readonly List<(PumpBranch Branch, List<int> Segments)> _covTreeBranchData = [];
    private readonly List<List<int>> _covTreeChildren = [];
    private readonly List<List<BlightLaneNode>> _covTreeForests = [];
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
