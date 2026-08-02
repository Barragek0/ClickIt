namespace ClickIt.Features.Blight.Strategy;

internal sealed class MeteorOnlyStrategy : IBlightTowerStrategy
{
    private static readonly TowerBuildRule[] s_rules =
    [
        TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(4)
            .SetSpecialization(TowerSpecialization.Meteor)
            .PreferCloseFoundationToPump(),
    ];

    public string Name => "Meteor Only";
    public string Description =>
        "Builds Meteor towers on every available foundation.\n\n" +
        "Recommended ring anoints:\n" +
        "- Indigo + Violet -> Meteor Tower (Burning Ground)";
    public Color DefaultLaneColor => new(194, 200, 0, 57);
    public IReadOnlyList<TowerBuildRule> Rules => s_rules;

    public TowerBuildRule? GetRule(BlightTowerType type)
        => type == BlightTowerType.Fireball ? s_rules[0] : null;

    public Color GetLaneColor(LaneCoverageResult segment)
        => segment.HasFireball
            ? new Color(0, 200, 0, 100)   // green = covered
            : new Color(200, 60, 60, 100); // red = uncovered
}
