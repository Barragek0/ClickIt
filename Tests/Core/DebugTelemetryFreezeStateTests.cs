using ClickIt.Core.Runtime;
using ClickIt.Features.Observability;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Core
{
    [TestClass]
    public class DebugTelemetryFreezeStateTests
    {
        [TestMethod]
        public void Freeze_WithPositiveDuration_ActivatesStateAndReturnsFrozenSnapshot()
        {
            var state = new DebugTelemetryFreezeState();

            state.Freeze(DebugTelemetrySnapshot.Empty, "hold", holdDurationMs: 100, nowTimestampMs: 1000);

            state.TryGetFreezeState(nowTimestampMs: 1050, out long remainingMs, out string reason).Should().BeTrue();
            remainingMs.Should().Be(50);
            reason.Should().Be("hold");
            state.TryGetFrozenSnapshot(nowTimestampMs: 1050, out DebugTelemetrySnapshot snapshot).Should().BeTrue();
            snapshot.Should().Be(DebugTelemetrySnapshot.Empty);
        }

        [TestMethod]
        public void Freeze_WithZeroDuration_DoesNotActivateState()
        {
            var state = new DebugTelemetryFreezeState();

            state.Freeze(DebugTelemetrySnapshot.Empty, "ignored", holdDurationMs: 0, nowTimestampMs: 1000);

            state.TryGetFreezeState(nowTimestampMs: 1000, out long remainingMs, out string reason).Should().BeFalse();
            remainingMs.Should().Be(0);
            reason.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetFrozenSnapshot_ExpiresStateAndClearsReason()
        {
            var state = new DebugTelemetryFreezeState();
            state.Freeze(DebugTelemetrySnapshot.Empty, "short", holdDurationMs: 10, nowTimestampMs: 500);

            state.TryGetFrozenSnapshot(nowTimestampMs: 511, out _).Should().BeFalse();
            state.TryGetFreezeState(nowTimestampMs: 511, out long remainingMs, out string reason).Should().BeFalse();
            remainingMs.Should().Be(0);
            reason.Should().BeEmpty();
        }
    }
}