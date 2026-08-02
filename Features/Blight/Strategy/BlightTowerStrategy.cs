namespace ClickIt.Features.Blight.Strategy;

internal enum BlightTowerStrategy
{
    ChillingSeismicMeteor = 0,

    MeteorOnly = 1
}

internal static class BlightTowerStrategyMetadata
{
    internal static string GetDescription(BlightTowerStrategy strategy) => strategy switch
    {
        BlightTowerStrategy.ChillingSeismicMeteor =>
            "Attempts to build Chilling and Seismic towers at all " +
            "lane cross-sections and/or near the pump to cover multiple lanes, " +
            "then fills remaining foundations with Meteor towers for damage.\n\n" +
            "Recommended ring anoints:\n" +
            "- Silver + Opalescent -> Chilling Tower (Chilling Beams)\n" +
            "- Indigo + Violet -> Meteor Tower (Burning Ground)",

        BlightTowerStrategy.MeteorOnly =>
            "Builds Meteor towers on every available foundation.\n\n" +
            "Recommended ring anoints:\n" +
            "- Indigo + Violet -> Meteor Tower (Burning Ground)",

        _ => string.Empty
    };

    internal static string GetName(BlightTowerStrategy strategy) => strategy switch
    {
        BlightTowerStrategy.ChillingSeismicMeteor => "Chilling + Seismic + Meteor",
        BlightTowerStrategy.MeteorOnly => "Meteor Only",
        _ => "Unknown"
    };

    internal static readonly string[] StrategyNames =
    [
        GetName(BlightTowerStrategy.ChillingSeismicMeteor),
        GetName(BlightTowerStrategy.MeteorOnly)
    ];
}
