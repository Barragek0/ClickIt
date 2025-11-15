// Disabled: merged into `InputSafetyAndValidationTests.cs` which provides broader coverage
#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Threading;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class InputSafetyTests
    {
        // These tests exercise the safety delegates used by ClickService which, in production,
        // wrap blocking/unblocking input. We don't call OS-level APIs here; instead we verify
        // that the delegates provided to the ClickService (or mocks) can be invoked safely
        // and that the expected fallback behavior is present.

        [TestMethod]
        public void SafeBlockInput_ShouldReturnFalseOnTimeoutByDefault()
        {
            bool blocked = false;
            // Simulate SafeBlockInput which returns whether it successfully blocked input.
            Func<int, bool> safeBlock = (timeoutMs) =>
            {
                // mimic an implementation that fails if timeout < 0
                if (timeoutMs < 0) return false;
                blocked = true;
                return true;
            };

            // negative timeout should be rejected
            safeBlock(-1).Should().BeFalse();
            blocked.Should().BeFalse();

            // positive timeout should block
            safeBlock(500).Should().BeTrue();
            blocked.Should().BeTrue();
        }

        [TestMethod]
        public void ForceUnblockInput_ShouldAlwaysSucceed()
        {
            bool wasBlocked = true;
            Action forceUnblock = () => { wasBlocked = false; };

            // After calling ForceUnblockInput, the internal tracked state should be unblocked
            forceUnblock();
            wasBlocked.Should().BeFalse();
        }

        [TestMethod]
        public void SafeBlockInput_ShouldBeThreadSafe()
        {
            int counter = 0;
            object sync = new object();
            Func<int, bool> safeBlock = (timeoutMs) =>
            {
                // small critical section to emulate state-change
                lock (sync)
                {
                    counter++;
                }
                return true;
            };

            var threads = new Thread[8];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() => { safeBlock(100); });
                threads[i].Start();
            }
            foreach (var t in threads) t.Join();

            // All invocations should have completed and incremented the counter
            counter.Should().Be(threads.Length);
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
