using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class OffscreenTraversalConfirmationTests
    {
        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_Delays_FirstSighting()
        {
            var result = ClickService.EvaluateOffscreenTraversalTargetConfirmation(
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
            var result = ClickService.EvaluateOffscreenTraversalTargetConfirmation(
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
        public void EvaluateOffscreenTraversalTargetConfirmation_ResetsDelay_WhenTargetChanges()
        {
            var result = ClickService.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 77,
                targetPath: "Metadata/Chests/Chest12",
                pendingAddress: 42,
                pendingPath: "Metadata/Chests/Chest9",
                pendingFirstSeenTimestampMs: 1000,
                now: 1060,
                confirmationWindowMs: 120);

            result.ShouldDelay.Should().BeTrue();
            result.NextAddress.Should().Be(77);
            result.NextPath.Should().Be("Metadata/Chests/Chest12");
            result.NextFirstSeenTimestampMs.Should().Be(1060);
            result.RemainingDelayMs.Should().Be(120);
        }
    }
}