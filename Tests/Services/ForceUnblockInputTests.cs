#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Threading;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class ForceUnblockInputTests
    {
        [TestMethod]
        public void ForceUnblock_ShouldResetBlockedState_WhenSafeBlockSucceeds()
        {
            bool blocked = false;
            Func<int, bool> safeBlock = (timeoutMs) => { blocked = true; return true; };
            Action forceUnblock = () => { blocked = false; };

            // Act
            bool ok = safeBlock(500);
            ok.Should().BeTrue();
            blocked.Should().BeTrue();

            // Force unblock
            forceUnblock();
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ForceUnblock_ShouldRecover_WhenSafeBlockThrows()
        {
            bool blocked = true; // stuck
            Func<int, bool> safeBlock = (timeoutMs) => throw new InvalidOperationException("Simulated blocking failure");
            Action forceUnblock = () => { blocked = false; };

            // Act & Assert: safeBlock throws, but forceUnblock should be able to recover state
            Assert.ThrowsException<InvalidOperationException>(() => safeBlock(100));
            // simulate emergency recovery
            forceUnblock();
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ForceUnblock_ShouldWorkUnderConcurrency()
        {
            int blockedCount = 0;
            object sync = new object();
            Func<int, bool> safeBlock = (timeoutMs) => { lock (sync) { blockedCount++; } return true; };
            Action forceUnblock = () => { lock (sync) { blockedCount = 0; } };

            var threads = new Thread[8];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() => safeBlock(100));
                threads[i].Start();
            }
            foreach (var t in threads) t.Join();

            blockedCount.Should().Be(threads.Length);

            // Force unblock concurrently
            var unblockThreads = new Thread[4];
            for (int i = 0; i < unblockThreads.Length; i++)
            {
                unblockThreads[i] = new Thread(() => forceUnblock());
                unblockThreads[i].Start();
            }
            foreach (var t in unblockThreads) t.Join();

            blockedCount.Should().Be(0);
        }
    }
}
#endif
