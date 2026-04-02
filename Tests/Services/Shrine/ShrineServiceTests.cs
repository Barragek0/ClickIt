using ClickIt.Services;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ClickIt.Tests.Shrine
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
        public void ThreadLocalShrineList_IsIsolatedPerThread()
        {
            ShrineService.ResetThreadLocalStorage();

            int mainThreadId = ShrineService.GetThreadLocalShrineListInstanceId();
            int workerThreadId = 0;

            var thread = new Thread(() =>
            {
                workerThreadId = ShrineService.GetThreadLocalShrineListInstanceId();
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
            service.SeedCacheWithSingleNullEntry(123L);

            service.InvalidateCache();

            service.HasCachedShrines().Should().BeFalse();
            service.GetLastShrineCacheTime().Should().Be(0);
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
