using ClickIt.Services;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ClickIt.Tests.Services.Shrine
{
    [TestClass]
    public class ShrineServiceTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            ShrineService.ClearThreadLocalStorageForTests();
        }

        [TestMethod]
        public void AreShrinesPresent_UsesFreshCache_WhenNotExpired()
        {
            var service = CreateService();

            service.SeedCacheWithSingleNullEntryForTests(0);
            service.StartCacheTimerForTests();
            service.SeedCacheWithSingleNullEntryForTests(service.GetCacheElapsedMillisecondsForTests());

            service.AreShrinesPresent().Should().BeTrue();
        }

        [TestMethod]
        public void AreShrinesPresent_RefreshesCache_WhenExpired()
        {
            var service = CreateService();

            service.SeedCacheWithSingleNullEntryForTests(0);
            service.StartCacheTimerForTests();
            service.SeedCacheWithSingleNullEntryForTests(-1000L);

            service.AreShrinesPresent().Should().BeFalse();
        }

        [TestMethod]
        public void ThreadLocalShrineList_IsIsolatedPerThread()
        {
            ShrineService.ClearThreadLocalStorageForTests();

            int mainThreadId = ShrineService.GetThreadLocalShrineListInstanceIdForTests();
            int workerThreadId = 0;

            var thread = new Thread(() =>
            {
                workerThreadId = ShrineService.GetThreadLocalShrineListInstanceIdForTests();
            });

            thread.Start();
            thread.Join();

            workerThreadId.Should().NotBe(0);
            workerThreadId.Should().NotBe(mainThreadId);
        }

        [TestMethod]
        public void InvalidateCache_ClearsCachedShrines()
        {
            var service = CreateService();
            service.SeedCacheWithSingleNullEntryForTests(123L);

            service.InvalidateCache();

            service.HasCachedShrinesForTests().Should().BeFalse();
            service.GetLastShrineCacheTimeForTests().Should().Be(0);
        }

        [TestMethod]
        public void IsClickableShrineCandidate_ReturnsFalse_ForNull()
        {
            ShrineService.IsClickableShrineCandidate(null).Should().BeFalse();
        }

        [TestMethod]
        public void IsShrine_ReturnsFalse_ForNull()
        {
            ShrineService.IsShrine(null!).Should().BeFalse();
        }

        [TestMethod]
        public void Constructor_Throws_WhenGameControllerIsNull()
        {
            var camera = (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera));

            Action act = () => new ShrineService(null!, camera);

            act.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void Constructor_Throws_WhenCameraIsNull()
        {
            var gc = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));

            Action act = () => new ShrineService(gc, null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void AreShrinesPresentInClickableArea_ReturnsFalse_WhenCacheContainsOnlyNullEntries()
        {
            var service = CreateService();

            service.SeedCacheWithSingleNullEntryForTests(0);
            service.StartCacheTimerForTests();
            service.SeedCacheWithSingleNullEntryForTests(service.GetCacheElapsedMillisecondsForTests());

            bool result = service.AreShrinesPresentInClickableArea(_ => true);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void GetNearestShrineInRange_ReturnsNull_WhenCacheContainsOnlyNullEntries()
        {
            var service = CreateService();

            service.SeedCacheWithSingleNullEntryForTests(0);
            service.StartCacheTimerForTests();
            service.SeedCacheWithSingleNullEntryForTests(service.GetCacheElapsedMillisecondsForTests());

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
