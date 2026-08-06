namespace ClickIt.Features.Blight.Strategy;

internal static class BlightStrategyResolver
{
    private static readonly ChillingSeismicMeteorStrategy DefaultStrategy = new();
    private static readonly MeteorOnlyStrategy MeteorOnly = new();

    internal static IBlightTowerStrategy Resolve(ClickItSettings settings)
    {
        BlightTowerStrategy strategy = (BlightTowerStrategy)settings.BlightTowerStrategy.Value;
        return strategy switch
        {
            BlightTowerStrategy.ChillingSeismicMeteor => DefaultStrategy,
            BlightTowerStrategy.MeteorOnly => MeteorOnly,
            _ => DefaultStrategy
        };
    }

    internal static string GetDescription(BlightTowerStrategy strategy) => strategy switch
    {
        BlightTowerStrategy.ChillingSeismicMeteor => DefaultStrategy.Description,
        BlightTowerStrategy.MeteorOnly => MeteorOnly.Description,
        _ => string.Empty
    };

    internal static readonly string[] StrategyNames =
    [
        DefaultStrategy.Name,
        MeteorOnly.Name
    ];
}
