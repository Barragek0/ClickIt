namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightPlanStepTests
{
    private static BlightPlanStep Step(BlightPlanAction action, int targetLevel)
        => new(action, NumVector2.Zero, BlightTowerType.Chilling, targetLevel);

    [TestMethod]
    public void ActionLabel_BuildStep_IsBuild()
    {
        Step(BlightPlanAction.Build, 1).ActionLabel.Should().Be("BUILD");
    }

    [TestMethod]
    public void ActionLabel_UpgradeBelowSpecTier_IsUpgrade()
    {
        Step(BlightPlanAction.Upgrade, 2).ActionLabel.Should().Be("UPGRADE");
        Step(BlightPlanAction.Upgrade, 3).ActionLabel.Should().Be("UPGRADE");
    }

    [TestMethod]
    public void ActionLabel_UpgradeToSpecTier_IsSpecial()
    {
        Step(BlightPlanAction.Upgrade, BlightTowerData.MaxUpgradeLevel).ActionLabel.Should().Be("SPECIAL");
    }

    [TestMethod]
    public void IsSpecializationStep_True_OnlyForUpgradeToLevelFour()
    {
        Step(BlightPlanAction.Upgrade, 4).IsSpecializationStep.Should().BeTrue();
        Step(BlightPlanAction.Upgrade, 3).IsSpecializationStep.Should().BeFalse();
        Step(BlightPlanAction.Build, 4).IsSpecializationStep.Should().BeFalse();
    }
}
