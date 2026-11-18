namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightMenuInteractionsTests
{
    [DataTestMethod]
    [DataRow("0", true, 0)]
    [DataRow(" 100 ", true, 100)]
    [DataRow("", false, 0)]
    [DataRow("?", false, 0)]
    [DataRow("-1", false, -1)]
    public void BuildCostRequiresReadableNonnegativeText(string text, bool readable, int cost)
    {
        BlightMenuInteractions.TryParseBuildCost(text, out int actual).Should().Be(readable);
        if (readable)
            actual.Should().Be(cost);
    }

    [TestMethod]
    public void MenuChildIndexForStep_IsBuildIconForBuild_AndUpgradeIconForUpgrade()
    {
        // The walk-ready region must use the step's OWN icon: an unbuilt foundation has no upgrade button (Child[3]), so checking it for a BUILD step never resolves and stalls the build.
        BlightMenuInteractions.MenuChildIndexForStep(BlightPlanAction.Build).Should().Be(2);
        BlightMenuInteractions.MenuChildIndexForStep(BlightPlanAction.Upgrade).Should().Be(3);
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(2, 2)]
    [DataRow(3, 3)]
    [DataRow(4, 4)]
    [DataRow(5, 5)]
    public void BuildTargetsTheSlotForThePlannedTower(int towerType, int slotIndex)
    {
        var step = new BlightPlanStep(BlightPlanAction.Build, default, (BlightTowerType)towerType, 1);
        BlightMenuInteractions.GetStepSlotIndex(step, -1).Should().Be(slotIndex);
    }

    [TestMethod]
    public void UpgradeTargetsPlainSlotOrTheChosenSpecialization()
    {
        var step = new BlightPlanStep(BlightPlanAction.Upgrade, default, BlightTowerType.Fireball, 2);
        BlightMenuInteractions.GetStepSlotIndex(step, (int)TowerSpecialization.Meteor).Should().Be(0);
        BlightMenuInteractions.GetStepSlotIndex(step with { TargetLevel = 3 }, (int)TowerSpecialization.Meteor).Should().Be(0);
        BlightMenuInteractions.GetStepSlotIndex(step with { TargetLevel = 4 }, (int)TowerSpecialization.Meteor).Should().Be(1);
        BlightMenuInteractions.GetStepSlotIndex(step with { TargetLevel = 4 }, (int)TowerSpecialization.Flamethrower).Should().Be(0);
    }
}
