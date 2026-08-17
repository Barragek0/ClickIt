namespace ClickIt.Tests.Features.Shrines
{
    [TestClass]
    public class ShrineServiceTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            ShrineService.ClearThreadLocalStorageForCurrentThread();
        }

        [TestMethod]
        public void InvalidateCache_ClearsCachedShrines()
        {
            var service = CreateService();
            SeedNullShrineCache(service, cacheTimestampMs: 123);

            service.InvalidateCache();

            service.GetNearestShrineInRange(100).Should().BeNull();
        }

        [TestMethod]
        public void GetNearestShrineInRange_ReturnsNull_WhenCacheContainsOnlyNullEntries()
        {
            var service = CreateService();

            SeedNullShrineCache(service, cacheTimestampMs: 0);

            var nearest = service.GetNearestShrineInRange(100);

            nearest.Should().BeNull();
        }

        [TestMethod]
        public void IsShrine_ReturnsFalse_WhenEntityPathCannotBeRead()
        {
            Entity shrine = ExileCoreOpaqueFactory.CreateOpaqueEntity();

            ShrineService.IsShrine(shrine).Should().BeFalse();
        }

        [TestMethod]
        public void IsClickableShrineCandidate_ReturnsFalse_WhenEntityStateCannotBeRead()
        {
            Entity shrine = ExileCoreOpaqueFactory.CreateOpaqueEntity();

            ShrineService.IsClickableShrineCandidate(shrine).Should().BeFalse();
        }

        private static ShrineService CreateService()
        {
            var gc = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            var camera = (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera));
            return new ShrineService(gc, camera);
        }

        private static void SeedNullShrineCache(ShrineService service, long cacheTimestampMs)
        {
            var cache = new TimedValueCache<int, List<Entity>>(200);
            cache.SetValue(0, cacheTimestampMs, [null!]);
            RuntimeMemberAccessor.SetRequiredMember(service, "_shrineCache", cache);
            RuntimeMemberAccessor.SetRequiredMember(service, "_shrineCacheTimer", new Stopwatch());
        }
    }
}
