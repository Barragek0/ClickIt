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
    ];

    public string Name => "Chilling + Seismic + Meteor";
    public string Description =>
        "Builds Chilling and Seismic towers (level 3) in the best places for multi-lane coverage, then fills remaining foundations with Meteor towers for damage.\n\n" +
        "Recommended ring anoints:\n" +
        "- Silver + Opalescent -> Chilling Tower (Chilling Beams)\n" +
        "- Indigo + Violet -> Meteor Tower (Burning Ground)";
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
            return new Color(255, 200, 0, 100);  // amber = only seismic
        return new Color(200, 60, 60, 100);      // red = uncovered
    }
}
