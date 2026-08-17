namespace ClickIt.Tests.UI;

[TestClass]
public class PerformanceTableRowsTests
{
    [TestMethod]
    public void Render_Catalog_ContainsExpectedSectionsInOrder()
    {
        string[] expected =
        [
            "Frame",
            "Altar",
            "Blight",
            "ClickIt.Features.ClickIt.Features.Click.Hotkey",
            "Debug",
            "Flush.Frame",
            "Flush.Text",
            "Harvest",
            "Inv.Full",
            "Lazy",
            "Pathfinding",
            "Perf.Overlay",
            "Strongbox",
            "ClickIt.ClickIt.UI.Rect",
            "Ultimatum",
        ];

        string[] actual = PerformanceTableRows.Render.Select(r => r.Label).ToArray();
        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Render_Accessors_ResolveTheRightRenderSection()
    {
        var monitor = new PerformanceMonitor(new ClickItSettings());
        monitor.RecordFpsSample(120);
        monitor.RecordRenderSectionTiming(RenderSection.AltarOverlay, 1.25);
        monitor.RecordRenderSectionTiming(RenderSection.BlightOverlay, 2.5);
        monitor.RecordRenderSectionTiming(RenderSection.UltimatumOverlay, 3.75);
        PerformanceMetricsSnapshot perf = monitor.GetDebugSnapshot();

        // The catalog's accessors must read from the same snapshot fields the surfaces render.
        PerformanceTableRows.Render
            .First(r => r.Label == "Altar").Get(perf).LastMs.Should().Be(1.25);
        PerformanceTableRows.Render
            .First(r => r.Label == "Blight").Get(perf).LastMs.Should().Be(2.5);
        PerformanceTableRows.Render
            .First(r => r.Label == "Ultimatum").Get(perf).LastMs.Should().Be(3.75);
        // The Frame row reads the whole-frame render timing (not a render section).
        PerformanceTableRows.Render
            .First(r => r.Label == "Frame").Get(perf).Should().Be(perf.Render);
    }

    [TestMethod]
    public void Interval_Catalog_ContainsExpectedKindsInOrder()
    {
        (string Label, IntervalKind Kind)[] expected =
        [
            ("Click", IntervalKind.Click),
            ("Walk", IntervalKind.Walk),
            ("Blight", IntervalKind.Blight),
            ("Label", IntervalKind.Label),
            ("ClickIt.Features.ClickIt.Features.Area.Blocked", IntervalKind.Area),
            ("Ultimatum", IntervalKind.Ultimatum),
            ("Flare", IntervalKind.Flare),
        ];

        var actual = PerformanceTableRows.Interval
            .Select(r => (r.Label, r.Kind))
            .ToArray();
        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Coroutine_Catalog_ContainsExpectedChannelsWithSubFlags()
    {
        (string Label, bool IsSub)[] expected =
        [
            ("Altar", false),
            ("Blight", false),
            ("Click", false),
            ("Processing", true),
            ("Sleep", true),
            ("Flare", false),
            ("Label Overlay", false),
            ("Ultimatum", false),
        ];

        var actual = PerformanceTableRows.Coroutine
            .Select(r => (r.Label, r.IsSub))
            .ToArray();
        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Coroutine_Accessors_ResolveTheRightChannels()
    {
        var monitor = new PerformanceMonitor(new ClickItSettings());
        monitor.RecordFpsSample(120);
        monitor.StartCoroutineTiming(TimingChannel.Altar);
        monitor.StopCoroutineTiming(TimingChannel.Altar);
        monitor.StartCoroutineTiming(TimingChannel.Blight);
        monitor.StopCoroutineTiming(TimingChannel.Blight);
        PerformanceMetricsSnapshot perf = monitor.GetDebugSnapshot();

        PerformanceTableRows.Coroutine
            .First(r => r.Label == "Altar").Get(perf).Should().Be(perf.AltarCoroutine);
        PerformanceTableRows.Coroutine
            .First(r => r.Label == "Blight").Get(perf).Should().Be(perf.BlightCoroutine);
        PerformanceTableRows.Coroutine
            .First(r => r.Label == "Processing").Get(perf).Should().Be(perf.GetProcessingSection(ProcessingSection.Click));
    }

    [TestMethod]
    public void Dlr_Catalog_ContainsExpectedSectionsInOrder()
    {
        (string Label, ProcessingSection Section)[] expected =
        [
            ("Altar", ProcessingSection.Altar),
            ("ClickIt.Features.ClickIt.Features.Area.Blocked", ProcessingSection.AreaBlockedUi),
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

        var actual = PerformanceTableRows.Dlr
            .Select(r => (r.Label, r.Section))
            .ToArray();
        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Processing_Catalog_ContainsExpectedSectionsWithBreakdownKinds()
    {
        (string Label, ProcessingSection Section, PerfBreakdownKind Breakdown)[] expected =
        [
            ("Altar", ProcessingSection.Altar, PerfBreakdownKind.Generic),
            ("ClickIt.Features.ClickIt.Features.Area.Blocked", ProcessingSection.AreaBlockedUi, PerfBreakdownKind.None),
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

        var actual = PerformanceTableRows.Processing
            .Select(r => (r.Label, r.Section, r.Breakdown))
            .ToArray();
        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Gc_Catalog_ContainsExpectedSectionsWithBreakdownKinds()
    {
        (string Label, ProcessingSection Section, PerfBreakdownKind Breakdown)[] expected =
        [
            ("Altar", ProcessingSection.Altar, PerfBreakdownKind.Generic),
            ("ClickIt.Features.ClickIt.Features.Area.Blocked", ProcessingSection.AreaBlockedUi, PerfBreakdownKind.None),
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

        var actual = PerformanceTableRows.Gc
            .Select(r => (r.Label, r.Section, r.Breakdown))
            .ToArray();
        actual.Should().Equal(expected);
    }
}
