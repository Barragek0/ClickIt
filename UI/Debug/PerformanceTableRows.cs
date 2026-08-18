namespace ClickIt.UI.Debug;

// Per-section breakdown shown after a process/GC row: Generic reads the section's Breakdowns store (time in the process table, allocation in the GC table), Click and LabelScan read their dedicated stores, None renders alone.
internal enum PerfBreakdownKind
{
    None,
    Generic,
    Click,
    LabelScan,
}

// Single source of truth for the performance tables; every surface (ImGui debug box, in-game overlay, clipboard dump) iterates the same row list so the section set, order, and labels cannot drift apart.
internal static class PerformanceTableRows
{
    // Render ms/f table: whole-frame row followed by every render section, in display order.
    public static readonly (string Label, Func<PerformanceMetricsSnapshot, TimingMetricsSnapshot> Get)[] Render =
    [
        ("Frame", p => p.Render),
        ("Altar", p => p.GetRenderSection(RenderSection.AltarOverlay)),
        ("Blight", p => p.GetRenderSection(RenderSection.BlightOverlay)),
        ("Click.Hotkey", p => p.GetRenderSection(RenderSection.ClickHotkeyToggle)),
        ("Debug", p => p.GetRenderSection(RenderSection.DebugOverlay)),
        ("Flush.Frame", p => p.GetRenderSection(RenderSection.FrameFlush)),
        ("Flush.Text", p => p.GetRenderSection(RenderSection.TextFlush)),
        ("Harvest", p => p.GetRenderSection(RenderSection.HarvestOverlay)),
        ("Inv.Full", p => p.GetRenderSection(RenderSection.InventoryFullWarning)),
        ("Lazy", p => p.GetRenderSection(RenderSection.LazyMode)),
        ("Pathfinding", p => p.GetRenderSection(RenderSection.PathfindingOverlay)),
        ("Perf.Overlay", p => p.GetRenderSection(RenderSection.PerformanceOverlay)),
        ("Strongbox", p => p.GetRenderSection(RenderSection.StrongboxOverlay)),
        ("ClickIt.ClickIt.UI.Rect", p => p.GetRenderSection(RenderSection.UiRegionRectangle)),
        ("Ultimatum", p => p.GetRenderSection(RenderSection.UltimatumOverlay)),
    ];

    // Interval ms table: every measured cadence marker in display order, with its expected ms.
    public static readonly (string Label, IntervalKind Kind, Func<PerformanceMetricsSnapshot, double> ExpectedMs)[] Interval =
    [
        ("Click", IntervalKind.Click, p => p.ClickTargetIntervalMs),
        ("Walk", IntervalKind.Walk, p => p.ClickTargetIntervalMs),
        ("Blight", IntervalKind.Blight, _ => 200),
        ("Label", IntervalKind.Label, _ => 50),
        ("Area.Blocked", IntervalKind.Area, _ => 250),
        ("Ultimatum", IntervalKind.Ultimatum, _ => 50),
        ("Flare", IntervalKind.Flare, _ => 100),
    ];

    // Coroutine ms/f table: every coroutine channel; the two Click sub-rows scale with the Click channel's own period and are indented per surface ("  " in overlays, "    " in the dump).
    public static readonly (string Label, Func<PerformanceMetricsSnapshot, TimingMetricsSnapshot> Get, bool IsSub)[] Coroutine =
    [
        ("Altar", p => p.AltarCoroutine, false),
        ("Blight", p => p.BlightCoroutine, false),
        ("Click", p => p.ClickCoroutine, false),
        ("Processing", p => p.GetProcessingSection(ProcessingSection.Click), true),
        ("Sleep", p => p.ClickSleepTiming, true),
        ("Flare", p => p.FlareCoroutine, false),
        ("Label Overlay", p => p.LabelOverlayCoroutine, false),
        ("Ultimatum", p => p.UltimatumCoroutine, false),
    ];

    // DLR ms/f table: per-feature dynamic-read attribution, in display order.
    public static readonly (string Label, ProcessingSection Section)[] Dlr =
    [
        ("Altar", ProcessingSection.Altar),
        ("Area.Blocked", ProcessingSection.AreaBlockedUi),
        ("Blight", ProcessingSection.Blight),
        ("Click", ProcessingSection.Click),
        ("Dump", ProcessingSection.GameStateDump),
        ("Flare", ProcessingSection.Flare),
        ("Harvest", ProcessingSection.Harvest),
        ("Label Scan", ProcessingSection.Label),
        ("Manual Hover", ProcessingSection.ManualUiHover),
        ("Pathfinding", ProcessingSection.Pathfinding),
        ("Strongbox", ProcessingSection.Strongbox),
        ("Ultimatum", ProcessingSection.Ultimatum),
    ];

    // Process ms/click table: section rows in display order with their time-breakdown kind (the Click row is the full click-block: click time + stage breakdown + frequency-target rows).
    public static readonly (string Label, ProcessingSection Section, PerfBreakdownKind Breakdown)[] Processing =
    [
        ("Altar", ProcessingSection.Altar, PerfBreakdownKind.Generic),
        ("Area.Blocked", ProcessingSection.AreaBlockedUi, PerfBreakdownKind.None),
        ("Blight", ProcessingSection.Blight, PerfBreakdownKind.Generic),
        ("Click", ProcessingSection.Click, PerfBreakdownKind.Click),
        ("Dump", ProcessingSection.GameStateDump, PerfBreakdownKind.None),
        ("Flare", ProcessingSection.Flare, PerfBreakdownKind.None),
        ("Harvest", ProcessingSection.Harvest, PerfBreakdownKind.None),
        ("Label Scan", ProcessingSection.Label, PerfBreakdownKind.None),
        ("Manual Hover", ProcessingSection.ManualUiHover, PerfBreakdownKind.None),
        ("Pathfinding", ProcessingSection.Pathfinding, PerfBreakdownKind.Generic),
        ("Strongbox", ProcessingSection.Strongbox, PerfBreakdownKind.Generic),
        ("Ultimatum", ProcessingSection.Ultimatum, PerfBreakdownKind.None),
    ];

    // GC byte/s table: section rows in display order with their allocation-breakdown kind; the clipboard dump keeps its own complete enum-order GC block (a deliberately different presentation, not the curated table).
    public static readonly (string Label, ProcessingSection Section, PerfBreakdownKind Breakdown)[] Gc =
    [
        ("Altar", ProcessingSection.Altar, PerfBreakdownKind.Generic),
        ("Area.Blocked", ProcessingSection.AreaBlockedUi, PerfBreakdownKind.None),
        ("Blight", ProcessingSection.Blight, PerfBreakdownKind.Generic),
        ("Click", ProcessingSection.Click, PerfBreakdownKind.Click),
        ("Dump", ProcessingSection.GameStateDump, PerfBreakdownKind.None),
        ("Flare", ProcessingSection.Flare, PerfBreakdownKind.None),
        ("Harvest", ProcessingSection.Harvest, PerfBreakdownKind.None),
        ("Label Scan", ProcessingSection.Label, PerfBreakdownKind.LabelScan),
        ("Manual Hover", ProcessingSection.ManualUiHover, PerfBreakdownKind.None),
        ("Pathfinding", ProcessingSection.Pathfinding, PerfBreakdownKind.Generic),
        ("Strongbox", ProcessingSection.Strongbox, PerfBreakdownKind.Generic),
        ("Ultimatum", ProcessingSection.Ultimatum, PerfBreakdownKind.None),
    ];
}
