namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenTraversalConfirmationTests
    {
        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_Delays_FirstSighting()
        {
            var result = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 42,
                targetPath: "Metadata/Chests/Chest9",
                pendingAddress: 0,
                pendingPath: string.Empty,
                pendingFirstSeenTimestampMs: 0,
                now: 1000,
                confirmationWindowMs: 120);

            result.ShouldDelay.Should().BeTrue();
            result.NextAddress.Should().Be(42);
            result.NextPath.Should().Be("Metadata/Chests/Chest9");
            result.NextFirstSeenTimestampMs.Should().Be(1000);
            result.RemainingDelayMs.Should().Be(120);
        }

        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_Allows_AfterWindowElapsed()
        {
            var result = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 42,
                targetPath: "Metadata/Chests/Chest9",
                pendingAddress: 42,
                pendingPath: "Metadata/Chests/Chest9",
                pendingFirstSeenTimestampMs: 1000,
                now: 1125,
                confirmationWindowMs: 120);

            result.ShouldDelay.Should().BeFalse();
            result.RemainingDelayMs.Should().Be(0);
        }

        [TestMethod]
        public void ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure_RequiresMatchingSettlersMechanics()
        {
            OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure("settlers-verisium", "settlers-verisium")
                .Should()
                .BeTrue();

            OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure("settlers-verisium", "settlers-copper")
                .Should()
                .BeFalse();

            OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure("items", "settlers-verisium")
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public void GetEldritchAltarMechanicIdForPath_RespectsEnabledInfluence()
        {
            OffscreenPathingMath.GetEldritchAltarMechanicIdForPath(
                    clickExarchAltars: true,
                    clickEaterAltars: false,
                    path: "Metadata/Terrain/CleansingFireAltar")
                .Should()
                .Be(MechanicIds.AltarsSearingExarch);

            OffscreenPathingMath.GetEldritchAltarMechanicIdForPath(
                    clickExarchAltars: false,
                    clickEaterAltars: true,
                    path: "Metadata/Terrain/TangleAltar")
                .Should()
                .Be(MechanicIds.AltarsEaterOfWorlds);

            OffscreenPathingMath.GetEldritchAltarMechanicIdForPath(
                    clickExarchAltars: false,
                    clickEaterAltars: false,
                    path: "Metadata/Terrain/CleansingFireAltar")
                .Should()
                .BeNull();
        }

        [TestMethod]
        public void ShouldBlockOffscreenTraversalAfterPathBuildFailure_BlocksOnlyNoRouteFailure()
        {
            OffscreenPathingMath.ShouldBlockOffscreenTraversalAfterPathBuildFailure(PathfindingService.AStarNoRouteFailureReason)
                .Should()
                .BeTrue();

            OffscreenPathingMath.ShouldBlockOffscreenTraversalAfterPathBuildFailure("Terrain/pathfinding data unavailable.")
                .Should()
                .BeFalse();

            OffscreenPathingMath.ShouldBlockOffscreenTraversalAfterPathBuildFailure(string.Empty)
                .Should()
                .BeFalse();
        }

    }
}