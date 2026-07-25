namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenTraversalConfirmationGateTests
    {
        [TestMethod]
        public void ShouldDelay_AllowsSameTargetAfterConfirmationWindowElapses()
        {
            long now = 1000;
            var gate = new OffscreenTraversalConfirmationGate(() => now);
            Entity target = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));

            bool firstDelayed = gate.ShouldDelay(target, "Metadata/Chests/Chest9", out long firstRemainingDelayMs);

            now = 1125;
            bool secondDelayed = gate.ShouldDelay(target, "Metadata/Chests/Chest9", out long secondRemainingDelayMs);

            firstDelayed.Should().BeTrue();
            firstRemainingDelayMs.Should().Be(120);
            secondDelayed.Should().BeFalse();
            secondRemainingDelayMs.Should().Be(0);
        }

        [TestMethod]
        public void Reset_RestartsConfirmationWindowForSameTarget()
        {
            long now = 1000;
            var gate = new OffscreenTraversalConfirmationGate(() => now);
            Entity target = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));

            gate.ShouldDelay(target, "Metadata/Chests/Chest9", out _).Should().BeTrue();

            gate.Reset();
            now = 1005;

            bool delayedAfterReset = gate.ShouldDelay(target, "Metadata/Chests/Chest9", out long remainingDelayMs);

            delayedAfterReset.Should().BeTrue();
            remainingDelayMs.Should().Be(120);
        }

        [TestMethod]
        public void ShouldDelay_RestartsConfirmationWindow_WhenTargetPathChanges()
        {
            long now = 1000;
            var gate = new OffscreenTraversalConfirmationGate(() => now);
            Entity target = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));

            gate.ShouldDelay(target, "Metadata/Chests/Chest9", out _).Should().BeTrue();

            now = 1010;
            bool delayed = gate.ShouldDelay(target, "Metadata/Chests/Chest10", out long remainingDelayMs);

            delayed.Should().BeTrue();
            remainingDelayMs.Should().Be(120);
        }
    }
}