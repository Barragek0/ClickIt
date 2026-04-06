namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ChestLootSettlementMathTests
    {
        [TestMethod]
        public void ResolvePostChestLootSettlementTimingSettings_NormalizesConfiguredValues_AndUsesDefaultsForUnknownMechanic()
        {
            var options = new ChestLootSettlementTimingOptions(
                new ChestLootSettlementTiming(initialDelayMs: -5, pollIntervalMs: 0, quietWindowMs: -1));

            ChestLootSettlementTiming basic = ChestLootSettlementMath.ResolvePostChestLootSettlementTimingSettings("basic-chests", options);
            basic.InitialDelayMs.Should().Be(0);
            basic.PollIntervalMs.Should().Be(1);
            basic.QuietWindowMs.Should().Be(0);

            ChestLootSettlementTiming league = ChestLootSettlementMath.ResolvePostChestLootSettlementTimingSettings("league-chests", options);
            league.InitialDelayMs.Should().Be(0);
            league.PollIntervalMs.Should().Be(1);
            league.QuietWindowMs.Should().Be(0);

            ChestLootSettlementTiming heist = ChestLootSettlementMath.ResolvePostChestLootSettlementTimingSettings(ChestLootSettlementMath.HeistChestSettleMechanicId, options);
            heist.InitialDelayMs.Should().Be(0);
            heist.PollIntervalMs.Should().Be(1);
            heist.QuietWindowMs.Should().Be(0);

            ChestLootSettlementTiming unknown = ChestLootSettlementMath.ResolvePostChestLootSettlementTimingSettings("other", options);
            unknown.InitialDelayMs.Should().Be(ChestLootSettlementMath.DefaultInitialDelayMs);
            unknown.PollIntervalMs.Should().Be(ChestLootSettlementMath.DefaultPollIntervalMs);
            unknown.QuietWindowMs.Should().Be(ChestLootSettlementMath.DefaultQuietWindowMs);
        }

        [TestMethod]
        public void ShouldWaitForChestLootSettlement_UsesIndependentWaitToggleByCategory()
        {
            ChestLootSettlementMath.ShouldWaitForChestLootSettlement("basic-chests", waitAfterOpeningBasicChests: true, waitAfterOpeningLeagueChests: false, waitAfterOpeningHeistChests: false)
                .Should()
                .BeTrue();

            ChestLootSettlementMath.ShouldWaitForChestLootSettlement("league-chests", waitAfterOpeningBasicChests: true, waitAfterOpeningLeagueChests: false, waitAfterOpeningHeistChests: true)
                .Should()
                .BeFalse();

            ChestLootSettlementMath.ShouldWaitForChestLootSettlement(ChestLootSettlementMath.HeistChestSettleMechanicId, waitAfterOpeningBasicChests: false, waitAfterOpeningLeagueChests: true, waitAfterOpeningHeistChests: true)
                .Should()
                .BeTrue();
        }

        [TestMethod]
        public void ResolveChestLootSettlementMechanicIdForOpenedLabel_MapsLeagueHeistChestToHeistCategory()
        {
            string? heistByPath = ChestLootSettlementMath.ResolveChestLootSettlementMechanicIdForOpenedLabel(
                MechanicIds.LeagueChests,
                entityPath: "Metadata/Chests/LeagueHeist/MilitaryChests/HeistChestPathMilitary",
                entityRenderName: "Military Supplies");

            string? heistByName = ChestLootSettlementMath.ResolveChestLootSettlementMechanicIdForOpenedLabel(
                MechanicIds.LeagueChests,
                entityPath: null,
                entityRenderName: "Secure Repository");

            string? nonHeistLeague = ChestLootSettlementMath.ResolveChestLootSettlementMechanicIdForOpenedLabel(
                MechanicIds.LeagueChests,
                entityPath: "Metadata/Chests/Blight/BlightChestObject",
                entityRenderName: "Blight Cyst");

            heistByPath.Should().Be(ChestLootSettlementMath.HeistChestSettleMechanicId);
            heistByName.Should().Be(ChestLootSettlementMath.HeistChestSettleMechanicId);
            nonHeistLeague.Should().Be(MechanicIds.LeagueChests);
        }

        [TestMethod]
        public void ResolveChestLootSettlementMechanicIdForOpenedLabel_DoesNotTreatHeistHazardsAsChestMechanic()
        {
            string? unresolvedHazard = ChestLootSettlementMath.ResolveChestLootSettlementMechanicIdForOpenedLabel(
                MechanicIds.HeistHazards,
                entityPath: "Metadata/Heist/Objects/Level/Hazards/Strength_SmashMarker",
                entityRenderName: "Strength Smash Marker");

            unresolvedHazard.Should().Be(MechanicIds.HeistHazards);
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