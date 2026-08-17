namespace ClickIt.Tests.UI;

[TestClass]
public class PerformanceInGameOverlayTests
{
    private static OverlayRenderContext CreateContext(DeferredDrawQueue textQueue)
        => new(
            new ClickItSettings(),
            GameController: null,
            Graphics: null,
            WindowArea: default,
            Labels: null,
            textQueue);

    [TestMethod]
    public void Draw_EnqueuesTableRows_WhenFpsDataPresent()
    {
        var monitor = new PerformanceMonitor(new ClickItSettings());
        monitor.RecordFpsSample(120);
        monitor.RecordRenderSectionTiming(RenderSection.AltarOverlay, 1.0);
        monitor.RecordProcessingTiming(ProcessingSection.Label, 2.0);
        monitor.RecordAllocation(ProcessingSection.Label, 2048);

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        textQueue.GetPendingCount().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Draw_EnqueuesLastAvgMaxColumnHeaders_ForEveryTimingTable()
    {
        var settings = new ClickItSettings();
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);
        monitor.RecordRenderSectionTiming(RenderSection.AltarOverlay, 1.0);
        monitor.RecordProcessingTiming(ProcessingSection.Label, 2.0);
        monitor.RecordAllocation(ProcessingSection.Label, 2048);

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        // The render, CR ms/f, interval, processing, DLR and GC tables keep "Last/Avg/Max" columns, matching the timing tables (the GC table shows byte/s in those same columns).
        lines.Count(l => l == "Last").Should().Be(6);
        lines.Count(l => l == "Avg").Should().Be(6);
        lines.Count(l => l == "Max").Should().Be(6);
    }

    [TestMethod]
    public void Draw_EnqueuesTotalRow_ForEveryTimingTable()
    {
        var settings = new ClickItSettings();
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);
        monitor.RecordRenderSectionTiming(RenderSection.AltarOverlay, 1.0);

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        // Render, coroutine, DLR, process and GC tables each render a "Total" row in the debug-box format, plus the click-frequency block's own indented Total (the interval table has no total).
        lines.Count(l => l == "Total").Should().Be(6);
    }

    [TestMethod]
    public void Draw_EnqueuesIntervalTable_WhenFpsDataPresent()
    {
        var settings = new ClickItSettings();
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);
        monitor.MarkInterval(IntervalKind.Click);
        monitor.MarkInterval(IntervalKind.Click);

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        lines.Should().Contain("Interval ms");
    }

    [TestMethod]
    public void Draw_EnqueuesLabelScanStageBreakdown_WhenLabelScanAllocationRecorded()
    {
        var settings = new ClickItSettings();
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);
        monitor.RecordAllocation(ProcessingSection.Label, 2048);
        monitor.RecordLabelScanAllocation(new LabelScanAllocationBreakdown(
            ListReadBytes: 1024 * 1024, ListAllocBytes: 4096, ValidityBytes: 32768, SortBytes: 2048, TotalBytes: 1024 * 1024 + 4096 + 32768 + 2048));

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        lines.Should().Contain("ListRead");
        lines.Should().Contain("ListAlloc");
        lines.Should().Contain("Validity");
        lines.Should().Contain("Sort");
    }

    [TestMethod]
    public void Draw_EnqueuesClickStageBreakdown_WhenClickAllocationRecorded()
    {
        var settings = new ClickItSettings();
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);
        monitor.RecordAllocation(ProcessingSection.Click, 4096);
        monitor.RecordClickAllocation(new ClickAllocationBreakdown(
            ContextBytes: 2048, AcquireBytes: 2 * 1024 * 1024, RankBytes: 32768, ExecuteBytes: 65536, PostBytes: 1024, OtherBytes: 4096, TotalBytes: 2048 + 2 * 1024 * 1024 + 32768 + 65536 + 1024 + 4096));

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        lines.Should().Contain("Acquire");
        lines.Should().Contain("Rank");
        lines.Should().Contain("Execute");
        lines.Should().Contain("Context");
        lines.Should().Contain("Post");
        lines.Should().Contain("Other");
    }

    [TestMethod]
    public void Draw_EnqueuesGcTable_AndSkipsZeroByteBreakdownStages()
    {
        var settings = new ClickItSettings();
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);

        // Blight breakdown stages: Scan allocates, Events never allocates and must not appear in the GC byte table (only in the process time table).
        Span<long> bytes = stackalloc long[2];
        Span<double> ms = stackalloc double[2];
        bytes[0] = 4096; bytes[1] = 0;
        ms[0] = 1; ms[1] = 4;
        monitor.RecordBreakdown(ProcessingSection.Blight, bytes, ms);

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        lines.Should().Contain("GC byte/s");
        lines.Should().Contain("Scan");
        lines.Should().Contain("Events");
        // The process table keeps the Events TIME row; the GC table must skip the 0-byte stage, so "Events" appears exactly once instead of once per table.
        lines.Count(l => l == "Events").Should().Be(1);
    }

    [TestMethod]
    public void Draw_EnqueuesNothing_WhenNoFpsData()
    {
        var overlay = new PerformanceInGameOverlay(() => default);
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        textQueue.GetPendingCount().Should().Be(0);
    }

    [TestMethod]
    public void Draw_TotalsRow_CombinesAllSectionMaxes()
    {
        var settings = new ClickItSettings();
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);
        monitor.RecordRenderSectionTiming(RenderSection.AltarOverlay, 2.0);
        monitor.RecordRenderSectionTiming(RenderSection.AltarOverlay, 4.0);  // Altar max = 4.0
        monitor.RecordRenderSectionTiming(RenderSection.BlightOverlay, 3.0); // Blight max = 3.0
        monitor.RecordRenderSectionTiming(RenderSection.DebugOverlay, 5.0);  // Debug max = 5.0

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        lines.Should().Contain("12.0");
    }

    [TestMethod]
    public void Draw_EnqueuesOnlyTopMemoryValues_WhenFpsDataPresent()
    {
        var settings = new ClickItSettings();
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);
        monitor.RecordSuccessfulClickTiming(18);

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        lines.Should().Contain("Process");
        lines.Should().Contain("Managed");
        lines.Should().Contain("Frag");
        lines.Should().NotContain("Gen0");
        lines.Should().NotContain("Gen1");
        lines.Should().NotContain("Gen2");
        lines.Should().NotContain("LOH");
        lines.Should().NotContain("Load");
        lines.Should().NotContain("CR ms/r");
    }

    [TestMethod]
    public void Draw_EnqueuesClickFrequencyTarget_WhenTargetConfigured()
    {
        var settings = new ClickItSettings();
        settings.ClickFrequencyTarget.Value = 200;
        var monitor = new PerformanceMonitor(settings);
        monitor.RecordFpsSample(120);
        monitor.RecordSuccessfulClickTiming(18);

        var overlay = new PerformanceInGameOverlay(() => monitor.GetDebugSnapshot());
        var textQueue = new DeferredDrawQueue();

        overlay.Draw(CreateContext(textQueue));

        string[] lines = textQueue.GetPendingTextSnapshot();
        lines.Should().Contain("Click Frequency");
        lines.Should().Contain("Processing");
        lines.Should().Contain("Total");
    }

    [TestMethod]
    public void IsEnabled_HiddenOutsideMap_WhenInMapOnlyToggleOnAndProviderFalse()
    {
        var settings = new ClickItSettings();
        settings.RenderPerformanceInGame.Value = true;
        settings.OnlyShowPerformanceInGameWhileInMap.Value = true;
        var overlay = new PerformanceInGameOverlay(() => default, () => false);

        overlay.IsEnabled(settings).Should().BeFalse();
    }

    [TestMethod]
    public void IsEnabled_ShownInMap_WhenInMapOnlyToggleOnAndProviderTrue()
    {
        var settings = new ClickItSettings();
        settings.RenderPerformanceInGame.Value = true;
        settings.OnlyShowPerformanceInGameWhileInMap.Value = true;
        var overlay = new PerformanceInGameOverlay(() => default, () => true);

        overlay.IsEnabled(settings).Should().BeTrue();
    }

    [TestMethod]
    public void IsEnabled_ShownOutsideMap_WhenInMapOnlyToggleOff()
    {
        var settings = new ClickItSettings();
        settings.RenderPerformanceInGame.Value = true;
        settings.OnlyShowPerformanceInGameWhileInMap.Value = false;
        var overlay = new PerformanceInGameOverlay(() => default, () => false);

        overlay.IsEnabled(settings).Should().BeTrue();
    }

    [TestMethod]
    public void IsEnabled_HiddenOutsideMap_WhenProviderMissingAndToggleOn()
    {
        var settings = new ClickItSettings();
        settings.RenderPerformanceInGame.Value = true;
        settings.OnlyShowPerformanceInGameWhileInMap.Value = true;
        var overlay = new PerformanceInGameOverlay(() => default);

        overlay.IsEnabled(settings).Should().BeFalse();
    }
}
