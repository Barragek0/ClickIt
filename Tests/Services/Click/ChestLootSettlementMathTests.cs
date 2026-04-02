using System.Collections.Generic;
using ClickIt.Services;
using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Click
{
    [TestClass]
    public class ChestLootSettlementMathTests
    {
        [TestMethod]
        public void FindPendingChestLabel_ReturnsNull_WhenLabelsMissing()
        {
            ChestLootSettlementMath.FindPendingChestLabel(null, itemAddress: 1, labelAddress: 2)
                .Should()
                .BeNull();

            ChestLootSettlementMath.FindPendingChestLabel(new List<ExileCore.PoEMemory.Elements.LabelOnGround>(), itemAddress: 1, labelAddress: 2)
                .Should()
                .BeNull();
        }

        [TestMethod]
        public void SeedKnownGroundItemAddresses_ReplacesSet_WithNonZeroSnapshotEntries()
        {
            var known = new HashSet<long> { 11, 22 };
            var snapshot = new HashSet<long> { 0, 22, 33, 44 };

            ChestLootSettlementMath.SeedKnownGroundItemAddresses(known, snapshot);

            known.Should().BeEquivalentTo(new[] { 22L, 33L, 44L });
        }

        [TestMethod]
        public void MergeNewGroundItemAddresses_AddsOnlyNewNonZeroAddresses()
        {
            var known = new HashSet<long> { 10, 20 };

            bool changed = ChestLootSettlementMath.MergeNewGroundItemAddresses(known, new HashSet<long> { 0, 20, 30 });

            changed.Should().BeTrue();
            known.Should().BeEquivalentTo(new[] { 10L, 20L, 30L });

            bool changedAgain = ChestLootSettlementMath.MergeNewGroundItemAddresses(known, new HashSet<long> { 0, 20, 30 });
            changedAgain.Should().BeFalse();
        }

        [TestMethod]
        public void IsMechanicEligibleForNearbyChestLootSettlementBypass_RequiresNonWhitespaceId()
        {
            ChestLootSettlementMath.IsMechanicEligibleForNearbyChestLootSettlementBypass(null).Should().BeFalse();
            ChestLootSettlementMath.IsMechanicEligibleForNearbyChestLootSettlementBypass("  ").Should().BeFalse();
            ChestLootSettlementMath.IsMechanicEligibleForNearbyChestLootSettlementBypass("Mechanic").Should().BeTrue();
        }

        [TestMethod]
        public void ShouldWaitForChestLootSettlement_RespectsMechanicSpecificToggles()
        {
            ChestLootSettlementMath.ShouldWaitForChestLootSettlement("basic-chests", waitAfterOpeningBasicChests: true, waitAfterOpeningLeagueChests: false)
                .Should()
                .BeTrue();

            ChestLootSettlementMath.ShouldWaitForChestLootSettlement("league-chests", waitAfterOpeningBasicChests: true, waitAfterOpeningLeagueChests: false)
                .Should()
                .BeFalse();

            ChestLootSettlementMath.ShouldWaitForChestLootSettlement("other", waitAfterOpeningBasicChests: true, waitAfterOpeningLeagueChests: true)
                .Should()
                .BeFalse();
        }

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
        public void ChestOpenRetryAndSettleStartGates_AreMutuallyExclusiveByVisibility()
        {
            ChestLootSettlementMath.ShouldContinueChestOpenRetries(pendingChestOpenConfirmationActive: true, chestLabelVisible: true).Should().BeTrue();
            ChestLootSettlementMath.ShouldContinueChestOpenRetries(pendingChestOpenConfirmationActive: true, chestLabelVisible: false).Should().BeFalse();

            ChestLootSettlementMath.ShouldStartChestLootSettlementAfterClick(pendingChestOpenConfirmationActive: true, chestLabelVisible: false).Should().BeTrue();
            ChestLootSettlementMath.ShouldStartChestLootSettlementAfterClick(pendingChestOpenConfirmationActive: true, chestLabelVisible: true).Should().BeFalse();
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

        [TestMethod]
        public void IsWithinNearbyChestLootSettlementBypassDistance_UsesInclusiveDistanceWindow()
        {
            ChestLootSettlementMath.IsWithinNearbyChestLootSettlementBypassDistance(new Vector2(0, 0), new Vector2(3, 4), maxDistance: 5)
                .Should()
                .BeTrue();

            ChestLootSettlementMath.IsWithinNearbyChestLootSettlementBypassDistance(new Vector2(0, 0), new Vector2(3, 4), maxDistance: 4)
                .Should()
                .BeFalse();

            ChestLootSettlementMath.IsWithinNearbyChestLootSettlementBypassDistance(new Vector2(0, 0), new Vector2(0, 0), maxDistance: -1)
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public void TryGetEntityGridPosition_ReturnsFalseForNullEntity()
        {
            bool ok = ChestLootSettlementMath.TryGetEntityGridPosition(null, out Vector2 gridPos);

            ok.Should().BeFalse();
            gridPos.Should().Be(default(Vector2));
        }
    }
}