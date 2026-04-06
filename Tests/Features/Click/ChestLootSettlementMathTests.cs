namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ChestLootSettlementMathTests
    {
        [TestMethod]
        public void ResolvePostChestLootSettlementTimingSettings_NormalizesConfiguredValues_AndUsesDefaultsForUnknownMechanic()
        {
            var options = new ChestLootSettlementTimingOptions(
                new ChestLootSettlementTiming(initialDelayMs: -5, pollIntervalMs: 0, quietWindowMs: -1),
                new ChestLootSettlementTiming(initialDelayMs: 1200, pollIntervalMs: 250, quietWindowMs: 900));

            ChestLootSettlementTiming basic = ChestLootSettlementMath.ResolvePostChestLootSettlementTimingSettings("basic-chests", options);
            basic.InitialDelayMs.Should().Be(0);
            basic.PollIntervalMs.Should().Be(1);
            basic.QuietWindowMs.Should().Be(0);

            ChestLootSettlementTiming league = ChestLootSettlementMath.ResolvePostChestLootSettlementTimingSettings("league-chests", options);
            league.InitialDelayMs.Should().Be(1200);
            league.PollIntervalMs.Should().Be(250);
            league.QuietWindowMs.Should().Be(900);

            ChestLootSettlementTiming unknown = ChestLootSettlementMath.ResolvePostChestLootSettlementTimingSettings("other", options);
            unknown.InitialDelayMs.Should().Be(ChestLootSettlementMath.DefaultInitialDelayMs);
            unknown.PollIntervalMs.Should().Be(ChestLootSettlementMath.DefaultPollIntervalMs);
            unknown.QuietWindowMs.Should().Be(ChestLootSettlementMath.DefaultQuietWindowMs);
        }

        [TestMethod]
        public void IsChestLootSettlementQuietPeriodElapsed_HandlesDisabledWindowAndElapsedThreshold()
        {
            ChestLootSettlementMath.IsChestLootSettlementQuietPeriodElapsed(now: 1000, lastNewGroundItemTimestampMs: 0, quietWindowMs: 0, out long disabledRemaining)
                .Should()
                .BeTrue();
            disabledRemaining.Should().Be(0);

            ChestLootSettlementMath.IsChestLootSettlementQuietPeriodElapsed(now: 1000, lastNewGroundItemTimestampMs: 0, quietWindowMs: 500, out long missingTimestampRemaining)
                .Should()
                .BeFalse();
            missingTimestampRemaining.Should().Be(500);

            ChestLootSettlementMath.IsChestLootSettlementQuietPeriodElapsed(now: 1500, lastNewGroundItemTimestampMs: 1100, quietWindowMs: 300, out long elapsedRemaining)
                .Should()
                .BeTrue();
            elapsedRemaining.Should().Be(0);

            ChestLootSettlementMath.IsChestLootSettlementQuietPeriodElapsed(now: 1250, lastNewGroundItemTimestampMs: 1100, quietWindowMs: 300, out long activeRemaining)
                .Should()
                .BeFalse();
            activeRemaining.Should().Be(150);
        }

    }
}