namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class ExileCorePerformanceApplierTests
    {
        [TestMethod]
        public void TryApplyRecommended_ReturnsFalse_WhenNoGameControllerProviderIsSet()
        {
            ExileCorePerformanceApplier.SetGameControllerProvider(null);

            ExileCorePerformanceApplier.TryApplyRecommended().Should().BeFalse();
        }

        [TestMethod]
        public void TryApplyRecommended_ReturnsFalse_WhenProviderReturnsNull()
        {
            ExileCorePerformanceApplier.SetGameControllerProvider(static () => null);

            ExileCorePerformanceApplier.TryApplyRecommended().Should().BeFalse();

            ExileCorePerformanceApplier.SetGameControllerProvider(null);
        }

        [TestMethod]
        public void GetRecommendedChanges_ReturnsNull_WhenNoGameControllerProviderIsSet()
        {
            ExileCorePerformanceApplier.SetGameControllerProvider(null);

            ExileCorePerformanceApplier.GetRecommendedChanges().Should().BeNull();
        }

        [TestMethod]
        public void GetRecommendedChanges_ReturnsNull_WhenProviderReturnsNull()
        {
            ExileCorePerformanceApplier.SetGameControllerProvider(static () => null);

            ExileCorePerformanceApplier.GetRecommendedChanges().Should().BeNull();

            ExileCorePerformanceApplier.SetGameControllerProvider(null);
        }

        [TestMethod]
        public void ResolveRecommendedThreadCount_ReturnsPositiveCount()
        {
            ExileCorePerformanceApplier.ResolveRecommendedThreadCount().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void SuppressSetupUntilReload_CanBeSetAndCleared()
        {
            ExileCorePerformanceApplier.SetSuppressSetupUntilReload(true);
            ExileCorePerformanceApplier.SuppressSetupUntilReload.Should().BeTrue();

            ExileCorePerformanceApplier.SetSuppressSetupUntilReload(false);
            ExileCorePerformanceApplier.SuppressSetupUntilReload.Should().BeFalse();
        }
    }
}
