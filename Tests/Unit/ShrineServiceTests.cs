using ClickIt.Services;
using ClickIt.Tests.TestUtils;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ClickIt.Tests.Unit
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

            PrivateFieldAccessor.Set(service, "_cachedShrines", new List<ExileCore.PoEMemory.MemoryObjects.Entity> { null! });
            StartCacheTimer(service);
            PrivateFieldAccessor.Set(service, "_lastShrineCacheTime", GetCacheElapsed(service));

            service.AreShrinesPresent().Should().BeTrue();
        }

        [TestMethod]
        public void AreShrinesPresent_RefreshesCache_WhenExpired()
        {
            var service = CreateService();

            PrivateFieldAccessor.Set(service, "_cachedShrines", new List<ExileCore.PoEMemory.MemoryObjects.Entity> { null! });
            StartCacheTimer(service);
            PrivateFieldAccessor.Set(service, "_lastShrineCacheTime", -1000L);

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
            PrivateFieldAccessor.Set(service, "_cachedShrines", new List<ExileCore.PoEMemory.MemoryObjects.Entity> { null! });
            PrivateFieldAccessor.Set(service, "_lastShrineCacheTime", 123L);

            service.InvalidateCache();

            PrivateFieldAccessor.Get<List<ExileCore.PoEMemory.MemoryObjects.Entity>?>(service, "_cachedShrines").Should().BeNull();
            PrivateFieldAccessor.Get<long>(service, "_lastShrineCacheTime").Should().Be(0);
        }

        private static ShrineService CreateService()
        {
            var gc = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            var camera = (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera));
            return new ShrineService(gc, camera);
        }

        private static void StartCacheTimer(ShrineService service)
        {
            var timer = PrivateFieldAccessor.Get<Stopwatch>(service, "_shrineCacheTimer");
            if (!timer.IsRunning)
            {
                timer.Start();
            }
        }

        private static long GetCacheElapsed(ShrineService service)
        {
            return PrivateFieldAccessor.Get<Stopwatch>(service, "_shrineCacheTimer").ElapsedMilliseconds;
        }
    }
}
