namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightServiceTests
{
    private static BlightPlanStep Step(BlightPlanAction action, BlightTowerType type, int targetLevel)
        => new(action, new NumVector2(5, 5), type, targetLevel);

    [TestMethod]
    public void GetStepTargetName_SpecTierUpgrade_ShowsChosenSpecialization()
    {
        // ChillingSeismicMeteorStrategy sets Fireball -> Meteor, so the plan UI
        // shows "UPGRADE Meteor" instead of "UPGRADE Fireball lvl4".
        var service = new BlightService(new ClickItSettings());
        service.GetStepTargetName(Step(BlightPlanAction.Upgrade, BlightTowerType.Fireball, 4))
            .Should().Be("Meteor");
    }

    [TestMethod]
    public void GetStepTargetName_PlainUpgrade_ShowsBaseType()
    {
        var service = new BlightService(new ClickItSettings());
        service.GetStepTargetName(Step(BlightPlanAction.Upgrade, BlightTowerType.Fireball, 3))
            .Should().Be("Fireball");
    }

    [TestMethod]
    public void GetStepTargetName_BuildStep_ShowsBaseType()
    {
        var service = new BlightService(new ClickItSettings());
        service.GetStepTargetName(Step(BlightPlanAction.Build, BlightTowerType.Fireball, 1))
            .Should().Be("Fireball");
    }

    [TestMethod]
    public void GetStepTargetName_NoSpecializationChosen_ShowsBaseType()
    {
        // Chilling/Seismic rules carry no specialization, so even a lvl4-style
        // step (if ever planned) shows the base tower type, never a guessed spec.
        var service = new BlightService(new ClickItSettings());
        service.GetStepTargetName(Step(BlightPlanAction.Upgrade, BlightTowerType.Seismic, 4))
            .Should().Be("Seismic");
    }
}
