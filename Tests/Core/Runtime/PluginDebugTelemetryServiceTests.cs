namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginDebugTelemetryServiceTests
    {
        [TestMethod]
        public void GetSnapshot_ReturnsEmpty_WhenNoPortsAreAvailable()
        {
            var service = new PluginDebugTelemetryService(
                () => null,
                () => null,
                () => null);

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Should().Be(DebugTelemetrySnapshot.Empty);
        }

        [TestMethod]
        public void GetSnapshot_UsesFrozenSnapshot_WithoutReevaluatingProviders()
        {
            bool shouldThrow = false;
            var service = new PluginDebugTelemetryService(
                () => shouldThrow ? throw new InvalidOperationException("click provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("label provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("path provider should not run while frozen") : null);

            service.FreezeSnapshot("hold", holdDurationMs: 100);
            shouldThrow = true;

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Should().Be(DebugTelemetrySnapshot.Empty);
            service.TryGetFreezeState(out long remainingMs, out string reason).Should().BeTrue();
            remainingMs.Should().BePositive();
            reason.Should().Be("hold");
        }

        [TestMethod]
        public void Clear_RemovesFrozenSnapshot_AndAllowsFreshProviderEvaluation()
        {
            bool shouldThrow = false;
            var service = new PluginDebugTelemetryService(
                () => shouldThrow ? throw new InvalidOperationException("click provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("label provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("path provider should run after clear") : null);

            service.FreezeSnapshot("hold", holdDurationMs: 100);
            shouldThrow = true;

            service.Clear();

            FluentActions.Invoking(service.GetSnapshot)
                .Should().Throw<InvalidOperationException>();

            service.TryGetFreezeState(out long remainingMs, out string reason).Should().BeFalse();
            remainingMs.Should().Be(0);
            reason.Should().BeEmpty();
        }
    }
}