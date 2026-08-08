namespace ClickIt.Features.Blight.Strategy;

internal static class BlightStrategyResolver
{
    private static readonly ChillingSeismicMeteorStrategy DefaultStrategy = new();
    private static readonly LockdownArcStrategy LockdownArc = new();
    private static readonly EmpoweredScoutsStrategy EmpoweredScouts = new();

    internal static readonly BlightTowerStrategy[] Strategies =
    [
        BlightTowerStrategy.ChillingSeismicMeteor,
        BlightTowerStrategy.LockdownArc,
        BlightTowerStrategy.EmpoweredScouts,
    ];

    internal static readonly string[] StrategyNames =
    [
        DefaultStrategy.Name,
        LockdownArc.Name,
        EmpoweredScouts.Name,
    ];

    internal static IBlightTowerStrategy Resolve(ClickItSettings settings)
        => Resolve((BlightTowerStrategy)settings.BlightTowerStrategy.Value);

    internal static IBlightTowerStrategy Resolve(BlightTowerStrategy strategy) => strategy switch
    {
        BlightTowerStrategy.ChillingSeismicMeteor => DefaultStrategy,
        BlightTowerStrategy.LockdownArc => LockdownArc,
        BlightTowerStrategy.EmpoweredScouts => EmpoweredScouts,
        _ => DefaultStrategy
    };

    internal static string GetDescription(BlightTowerStrategy strategy) => strategy switch
    {
        BlightTowerStrategy.ChillingSeismicMeteor => DefaultStrategy.Description,
        BlightTowerStrategy.LockdownArc => LockdownArc.Description,
        BlightTowerStrategy.EmpoweredScouts => EmpoweredScouts.Description,
        _ => string.Empty
    };

    // Combo index for a persisted value; a stale/removed value falls back to the first strategy.
    internal static int GetComboIndex(int strategyValue)
    {
        for (int i = 0; i < Strategies.Length; i++)
            if ((int)Strategies[i] == strategyValue)
                return i;
        return 0;
    }
}
