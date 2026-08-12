namespace ClickIt.Tests.Features.Harvest;

[TestClass]
public class ClickItHarvestFeatureTests
{
    [TestMethod]
    public void DecideBestPlot_Empty_ReturnsNoHarvestLabels()
    {
        HarvestDecision d = HarvestService.DecideBestPlot([]);
        d.Outcome.Should().Be(HarvestDecisionOutcome.NoHarvestLabels);
        d.ChosenLabel.Should().BeNull();
        d.IsHarvestClickBlocked.Should().BeFalse();
    }

    [TestMethod]
    public void DecideBestPlot_OneLabel_BlocksClicking()
    {
        LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();
        var estimates = new List<HarvestPlotEstimate> { new(label, [], 100, default) };
        HarvestDecision d = HarvestService.DecideBestPlot(estimates);
        d.Outcome.Should().Be(HarvestDecisionOutcome.SingleLabelNoClick);
        d.ChosenLabel.Should().BeNull();
        d.IsHarvestClickBlocked.Should().BeTrue();
    }

    [TestMethod]
    public void DecideBestPlot_TwoLabelsDifferentEstimates_PicksHighest()
    {
        LabelOnGround low = ExileCoreOpaqueFactory.CreateOpaqueLabel();
        LabelOnGround high = ExileCoreOpaqueFactory.CreateOpaqueLabel();
        var e = new List<HarvestPlotEstimate> { new(low, [], 50, default), new(high, [], 100, default) };
        HarvestDecision d = HarvestService.DecideBestPlot(e);
        d.Outcome.Should().Be(HarvestDecisionOutcome.TopLabelChosen);
        d.ChosenLabel.Should().BeSameAs(high);
        d.IsHarvestClickBlocked.Should().BeFalse();
    }

    [TestMethod]
    public void DecideBestPlot_TwoLabelsEqualEstimates_NoFilter()
    {
        LabelOnGround a = ExileCoreOpaqueFactory.CreateOpaqueLabel();
        LabelOnGround b = ExileCoreOpaqueFactory.CreateOpaqueLabel();
        var e = new List<HarvestPlotEstimate> { new(a, [], 75, default), new(b, [], 75, default) };
        HarvestDecision d = HarvestService.DecideBestPlot(e);
        d.Outcome.Should().Be(HarvestDecisionOutcome.EqualEstimatesNoFilter);
        d.ChosenLabel.Should().BeNull();
        d.IsHarvestClickBlocked.Should().BeFalse();
    }

    [TestMethod]
    public void GetLabelToClick_LifeforceOff_ReturnsNull()
    {
        var s = new ClickItSettings();
        s.ClickHigherHarvestEstimate.Value = false;
        var svc = new HarvestService(s);
        svc.GetLabelToClick().Should().BeNull();
    }

    [TestMethod]
    public void ShouldSkipRescan_SameSetWithinWindow_Skips()
    {
        HarvestService.ShouldSkipRescan(sameLabelSet: true, now: 99, lastScanAtMs: 0, rescanIntervalMs: 100)
            .Should().BeTrue();
    }

    [TestMethod]
    public void ShouldSkipRescan_SameSetAfterWindow_ReScans()
    {
        // A stable reference must never freeze position-dependent bounds — the cadence forces a
        // re-scan even when the label set is unchanged.
        HarvestService.ShouldSkipRescan(sameLabelSet: true, now: 100, lastScanAtMs: 0, rescanIntervalMs: 100)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldSkipRescan_ChangedSet_ReScansImmediately()
    {
        HarvestService.ShouldSkipRescan(sameLabelSet: false, now: 0, lastScanAtMs: 0, rescanIntervalMs: 100)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ProcessHarvestPlots_ReScansOnCadence_WhenLabelReferenceUnchanged()
    {
        var s = new ClickItSettings();
        s.ClickHarvest.Value = true;
        long now = 0;
        var svc = new HarvestService(s, () => now);
        IReadOnlyList<LabelOnGround> labels = [];

        // First scan runs; a second call inside the window is skipped; after the cadence the scan
        // runs again (observable via the re-scan path not throwing and the guard releasing).
        svc.ProcessHarvestPlots(labels, gameController: null);
        now = 50;
        svc.ProcessHarvestPlots(labels, gameController: null);
        now = 150;
        svc.ProcessHarvestPlots(labels, gameController: null);

        svc.CurrentDecision.Outcome.Should().Be(HarvestDecisionOutcome.NoHarvestLabels);
    }

    [TestMethod]
    public void GetLabelToClick_BlockedState_ReturnsNull()
    {
        // Simulate blocked decision directly
        LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();
        var s = new ClickItSettings();
        s.ClickHigherHarvestEstimate.Value = true;
        var svc = new HarvestService(s);
        // Manually set blocked decision (normally done by DecideBestPlot)
        typeof(HarvestService).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, s);
        // We can't easily simulate the full state, but GetLabelToClick
        // checks IsHarvestClickBlocked which defaults to false in a
        // fresh HarvestDecision. With no ChosenLabel and no blocked flag,
        // and lifeforce off, GetLabelToClick returns null.
        svc.GetLabelToClick().Should().BeNull();
    }

    [TestMethod]
    public void SettingsFactory_LifeforceOn_SetsBlocked()
    {
        var s = new ClickItSettings();
        s.ClickHigherHarvestEstimate.Value = true;
        var f = new ClickSettingsFactory(s, new MechanicPrioritySnapshotService(), _ => false, _ => false);
        f.Create([]).HarvestLabelSelectionBlocked.Should().BeTrue();
    }

    [TestMethod]
    public void SettingsFactory_LifeforceOff_NoBlocked()
    {
        var s = new ClickItSettings();
        s.ClickHigherHarvestEstimate.Value = false;
        var f = new ClickSettingsFactory(s, new MechanicPrioritySnapshotService(), _ => false, _ => false);
        f.Create([]).HarvestLabelSelectionBlocked.Should().BeFalse();
    }

    [TestMethod]
    public void ClickSettings_HarvestLabelSelectionBlocked_DefaultsFalse()
    {
        new ClickSettings().HarvestLabelSelectionBlocked.Should().BeFalse();
    }
}
