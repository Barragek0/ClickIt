namespace ClickIt.Features.Blight.Strategy;

internal sealed class ChillingSeismicMeteorStrategy : IBlightTowerStrategy
{
    private static readonly TowerBuildRule[] s_rules =
    [
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Chilling)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(3)
            .TreatAsCoverageTower()
            .PreferCloseFoundationToPump()
            .UpgradeOnlyWhenNeededForCoverage()
            .UpgradeBeforeMovingOntoLowerPriority(),
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Seismic)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(3)
            .TreatAsCoverageTower()
            .PreferCloseFoundationToPump()
            .UpgradeOnlyWhenNeededForCoverage()
            .UpgradeBeforeMovingOntoLowerPriority(),
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .SetSpecialization(TowerSpecialization.Meteor)
            .PreferCloseFoundationToPump()
            .AlwaysUpgradeBeforeBuildingNew(),
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Empowering)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(3)
            .AlwaysUpgradeBeforeBuildingNew()
            .BuildUntilTowersAreEmpowered(BlightTowerType.Seismic, BlightTowerType.Chilling),
    ];

    public string Name => "Empowered Lockdown + Meteor";
    public bool UsesPhasedLockdown => true;
    public string Description =>
        "Rules in order of priority:\n" +
        "-> Builds Tier 1 Chilling and Seismic Towers where effective.\n" +
        "-> Rushes support and upgrades for the control tower on immune lanes.\n" +
        "-> Upgrades Chilling, then Seismic to Tier 3 on ordinary lanes.\n" +
        "-> Builds and upgrades Empowering Towers to support both control types.\n" +
        "-> Covers every lane segment with Meteors, closest foundations to the pump first.\n\n" +
        "Recommended ring anoints:\n" +
        "- Silver + Opalescent -> Chilling Tower (Chilling Beams)\n" +
        "- Indigo + Violet -> Meteor Tower (Burning Ground)\n";
    public IReadOnlyList<TowerBuildRule> Rules => s_rules;

    public TowerBuildRule? GetRule(BlightTowerType type)
    {
        for (int i = 0; i < s_rules.Length; i++)
            if (s_rules[i].TowerType == type)
                return s_rules[i];
        return null;
    }
}
