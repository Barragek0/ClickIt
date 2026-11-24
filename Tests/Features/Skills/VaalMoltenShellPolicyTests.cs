namespace ClickIt.Tests.Features.Skills;

[TestClass]
public sealed class VaalMoltenShellPolicyTests
{
    [TestMethod]
    public void Settings_DefaultToLifeAndMagicRareUniqueMonsterConditions()
    {
        var settings = new ClickItSettings();
        settings.VaalMoltenShellLifeEnabled.Value.Should().BeTrue();
        settings.VaalMoltenShellLifePercent.Value.Should().Be(90);
        settings.VaalMoltenShellAlways.Value.Should().BeFalse();
        settings.VaalMoltenShellEnergyShieldEnabled.Value.Should().BeFalse();
        settings.VaalMoltenShellNormalEnabled.Value.Should().BeFalse();
        settings.VaalMoltenShellMagicEnabled.Value.Should().BeTrue();
        settings.VaalMoltenShellRareEnabled.Value.Should().BeTrue();
        settings.VaalMoltenShellUniqueEnabled.Value.Should().BeTrue();
        settings.VaalMoltenShellNormalCount.Value.Should().Be(1);
        settings.VaalMoltenShellMagicCount.Value.Should().Be(1);
        settings.VaalMoltenShellRareCount.Value.Should().Be(1);
        settings.VaalMoltenShellUniqueCount.Value.Should().Be(1);
        settings.VaalMoltenShellNormalDistance.Value.Should().Be(50);
        settings.VaalMoltenShellMagicDistance.Value.Should().Be(50);
        settings.VaalMoltenShellRareDistance.Value.Should().Be(50);
        settings.VaalMoltenShellUniqueDistance.Value.Should().Be(50);
    }

    [DataTestMethod]
    [DataRow(89, 100, 90, true)]
    [DataRow(90, 100, 90, false)]
    [DataRow(0, 0, 90, true)]
    [DataRow(-1, 100, 90, false)]
    public void BelowPercent_UsesStrictThreshold(int current, int max, int threshold, bool expected)
        => VaalMoltenShellPolicy.BelowPercent(current, max, threshold).Should().Be(expected);

    [TestMethod]
    [TestCategory("Regression")]
    public void AnyMonsterConditionMet_AcceptsAnyEnabledRarityAtItsThreshold()
    {
        var rules = LazyModeBlockerService.BuildNearbyMonsterRestrictionSettings(0, 30, 2, 40, 1, 50, 1, 50);
        VaalMoltenShellPolicy.AnyMonsterConditionMet(rules, new NearbyMonsterCounts(10, 1, 0, 0)).Should().BeFalse();
        VaalMoltenShellPolicy.AnyMonsterConditionMet(rules, new NearbyMonsterCounts(0, 2, 0, 0)).Should().BeTrue();
        VaalMoltenShellPolicy.AnyMonsterConditionMet(rules, new NearbyMonsterCounts(0, 0, 1, 0)).Should().BeTrue();
        VaalMoltenShellPolicy.AnyMonsterConditionMet(rules, new NearbyMonsterCounts(0, 0, 0, 1)).Should().BeTrue();
        VaalMoltenShellPolicy.AnyMonsterConditionMet(rules, new NearbyMonsterCounts(0, 0, 0, 0)).Should().BeFalse();
    }
}
