namespace ClickIt.Features.Blight.Strategy;

internal sealed class LockdownArcStrategy : IBlightTowerStrategy
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
            .SetTower(BlightTowerType.ShockNova)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .SetSpecialization(TowerSpecialization.ArcTower)
            .PreferCloseFoundationToPump()
            .AlwaysUpgradeBeforeBuildingNew(),
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Empowering)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(3)
            .AlwaysUpgradeBeforeBuildingNew()
            .BuildUntilTowersAreEmpowered(BlightTowerType.Seismic, BlightTowerType.Chilling),
    ];

    public string Name => "Empowered Lockdown + Arc";
    public string Description =>
        "Rules in order of priority:\n" +
        "-> Builds Chilling and Seismic Towers to cover every lane.\n" +
        "-> Builds Empowering Towers to buff Chilling and Seismic.\n" +
        "-> Builds Arc Towers for damage.\n\n" +
        "Recommended ring anoints:\n" +
        "- Silver + Opalescent -> Chilling Tower (Chilling Beams)\n" +
        "- Silver + Clear -> Arc Tower (+1 repeat)";
    public Color DefaultLaneColor => new(194, 200, 0, 57);
    public IReadOnlyList<TowerBuildRule> Rules => s_rules;

    public TowerBuildRule? GetRule(BlightTowerType type)
    {
        for (int i = 0; i < s_rules.Length; i++)
            if (s_rules[i].TowerType == type)
                return s_rules[i];
        return null;
    }

    public Color GetLaneColor(LaneCoverageResult segment)
    {
        if (segment.HasChilling && segment.HasSeismic)
            return new Color(0, 200, 0, 100);   // green = both types covered
        if (segment.HasChilling)
            return new Color(50, 130, 255, 100); // blue = only chilling
        if (segment.HasSeismic)
            return new Color(255, 128, 0, 100);  // orange = only seismic
        return new Color(200, 60, 60, 100);      // red = uncovered
    }
}
