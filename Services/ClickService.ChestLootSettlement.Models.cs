namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal readonly struct ChestLootSettlementTiming
        {
            public ChestLootSettlementTiming(int initialDelayMs, int pollIntervalMs, int quietWindowMs)
            {
                InitialDelayMs = initialDelayMs;
                PollIntervalMs = pollIntervalMs;
                QuietWindowMs = quietWindowMs;
            }

            public int InitialDelayMs { get; }
            public int PollIntervalMs { get; }
            public int QuietWindowMs { get; }
        }

        internal readonly struct ChestLootSettlementTimingOptions
        {
            public ChestLootSettlementTimingOptions(ChestLootSettlementTiming basic, ChestLootSettlementTiming league)
            {
                Basic = basic;
                League = league;
            }

            public ChestLootSettlementTiming Basic { get; }
            public ChestLootSettlementTiming League { get; }
        }
    }
}