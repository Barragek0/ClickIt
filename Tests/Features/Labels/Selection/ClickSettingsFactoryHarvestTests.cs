namespace ClickIt.Tests.Features.Labels.Selection;

[TestClass]
public class ClickSettingsFactoryHarvestTests
{
    [TestMethod]
    public void Create_LifeforceEstimationEnabled_SetsBlockedFlag()
    {
        var settings = new ClickItSettings();
        settings.HarvestLifeforceEstimation.Value = true;
        var factory = new ClickSettingsFactory(
            settings,
            new MechanicPrioritySnapshotService(),
            _ => false,
            _ => false);

        ClickSettings result = factory.Create([]);

        result.HarvestLabelSelectionBlocked.Should().BeTrue();
    }

    [TestMethod]
    public void Create_LifeforceEstimationDisabled_NoBlocked()
    {
        var settings = new ClickItSettings();
        settings.HarvestLifeforceEstimation.Value = false;
        var factory = new ClickSettingsFactory(
            settings,
            new MechanicPrioritySnapshotService(),
            _ => false,
            _ => false);

        ClickSettings result = factory.Create([]);

        result.HarvestLabelSelectionBlocked.Should().BeFalse();
    }
}
