namespace ClickIt.Tests.Features.Shrines
{
    [TestClass]
    public class ShrineServiceTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            ShrineService.ResetThreadLocalStorage();
        }

        [TestMethod]
        public void AreShrinesPresent_UsesFreshCache_WhenNotExpired()
        {
            var service = CreateService();

            service.SeedCacheWithSingleNullEntry(0);
            service.EnsureCacheTimerStarted();
            service.SeedCacheWithSingleNullEntry(service.GetCacheElapsedMilliseconds());

            service.AreShrinesPresent().Should().BeTrue();
        }

        [TestMethod]
        public void AreShrinesPresent_RefreshesCache_WhenExpired()
        {
            var service = CreateService();

            service.SeedCacheWithSingleNullEntry(0);
            service.EnsureCacheTimerStarted();
            service.SeedCacheWithSingleNullEntry(-1000L);

            service.AreShrinesPresent().Should().BeFalse();
        }

        [TestMethod]
        public void InvalidateCache_ClearsCachedShrines()
        {
            var service = CreateService();
            service.SeedCacheWithSingleNullEntry(123L);

            service.InvalidateCache();

            service.HasCachedShrines().Should().BeFalse();
            service.GetLastShrineCacheTime().Should().Be(0);
        }

        [TestMethod]
        public void AreShrinesPresentInClickableArea_ReturnsFalse_WhenCacheContainsOnlyNullEntries()
        {
            var service = CreateService();

            service.SeedCacheWithSingleNullEntry(0);
            service.EnsureCacheTimerStarted();
            service.SeedCacheWithSingleNullEntry(service.GetCacheElapsedMilliseconds());

            bool result = service.AreShrinesPresentInClickableArea(_ => true);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void GetNearestShrineInRange_ReturnsNull_WhenCacheContainsOnlyNullEntries()
        {
            var service = CreateService();

            service.SeedCacheWithSingleNullEntry(0);
            service.EnsureCacheTimerStarted();
            service.SeedCacheWithSingleNullEntry(service.GetCacheElapsedMilliseconds());

            var nearest = service.GetNearestShrineInRange(100);

            nearest.Should().BeNull();
        }

        private static ShrineService CreateService()
        {
            var gc = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            var camera = (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera));
            return new ShrineService(gc, camera);
        }
    }
}
