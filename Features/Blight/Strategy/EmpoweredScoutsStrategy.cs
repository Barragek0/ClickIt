namespace ClickIt.Features.Blight.Strategy;

internal sealed class EmpoweredScoutsStrategy : IBlightTowerStrategy
{
    private static readonly TowerBuildRule[] s_rules =
    [
        // Phase 1 — one Scout tower per lane (coverage), each pushed to Scout Minion level 4 before any lower-priority work starts.
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Summoning)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(4)
            .SetSpecialization(TowerSpecialization.ScoutMinion)
            .TreatAsCoverageTower()
            .PreferCloseFoundationToPump()
            .UpgradeBeforeMovingOntoLowerPriority(),
        // Phases 2 + 3 — same tier so the fill planner round-robins them: each new Scout tower is followed by an Empowering tower (upgraded to 3) that covers it.
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Empowering)
            .SetPriority(TowerBuildPriority.Normal)
            .SetMaxUpgradeLevel(3)
            .AlwaysUpgradeBeforeBuildingNew()
            .BuildUntilTowersAreEmpowered(BlightTowerType.Summoning),
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Summoning)
            .SetPriority(TowerBuildPriority.Normal)
            .SetMaxUpgradeLevel(4)
            .SetSpecialization(TowerSpecialization.ScoutMinion)
            .PreferCloseFoundationToPump()
            .AlwaysUpgradeBeforeBuildingNew(),
    ];

    public string Name => "Empowered Scouts";
    public string Description =>
        "Rules in order of priority:\n" +
        "-> Builds Scout Towers to cover every lane.\n" +
        "-> Builds Empowering Towers to buff the coverage Scouts.\n" +
        "-> Alternates Scout and Empowering Towers for damage.\n\n" +
        "Recommended ring anoints:\n" +
        "- Teal + Violet -> Scout Tower (+25% range)\n" +
        "- Amber + Amber -> Scout Tower (+25% damage)";
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
        => segment.HasSummoning
            ? new Color(0, 200, 0, 100)   // green = scout coverage
            : new Color(200, 60, 60, 100); // red = uncovered
}
