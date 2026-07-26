namespace ClickIt.Features.Harvest;

internal static class HarvestLifeforceEstimator
{
    // Expected lifeforce per seed for each tier (dropChance * averageAmount)
    private static readonly double[] ExpectedLifeforcePerSeedByTier =
    [
        0.02 * 7.25,   // Tier 1: ~0.145
        0.10 * 18.5,   // Tier 2: ~1.85
        1.00 * 47.0,   // Tier 3: 47.0
        1.00 * 235.0   // Tier 4: 235.0
    ];

    internal static double EstimateTotalLifeforce(ReadOnlySpan<HarvestSeedRow> seedRows)
    {
        double total = 0;
        for (int i = 0; i < seedRows.Length; i++)
        {
            total += EstimateRowLifeforce(seedRows[i]);
        }

        return total;
    }

    internal static double EstimateRowLifeforce(HarvestSeedRow row)
    {
        int tierIndex = (int)row.Tier;
        if (tierIndex < 0 || tierIndex >= ExpectedLifeforcePerSeedByTier.Length)
            return 0;

        return row.Quantity * ExpectedLifeforcePerSeedByTier[tierIndex];
    }
}

internal readonly record struct HarvestSeedRow(
    int Quantity,
    string MonsterType,
    HarvestSeedTier Tier,
    string RawText = "",
    byte ColorR = 0,
    byte ColorG = 0,
    byte ColorB = 0);

internal enum HarvestSeedTier
{
    Tier1 = 0,
    Tier2 = 1,
    Tier3 = 2,
    Tier4 = 3
}
