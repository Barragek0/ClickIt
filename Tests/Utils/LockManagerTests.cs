using System;
using System.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class LockManagerTests
    {
        [TestMethod]
        public void Acquire_Null_ReturnsNoopReleaser()
        {
            var settings = new ClickItSettings();
            var lm = new global::ClickIt.Utils.LockManager(settings);

            using var d = global::ClickIt.Utils.LockManager.Acquire(null);
            d.Should().NotBeNull();

            // Acquire(null) must return a singleton no-op instance (safe to dispose multiple times)
            using var d2 = global::ClickIt.Utils.LockManager.Acquire(null);
            d2.Should().BeSameAs(d);
        }

        [TestMethod]
        public void Acquire_WithObject_EntersMonitorAndExitsOnDispose()
        {
            var settings = new ClickItSettings();
            var lm = new global::ClickIt.Utils.LockManager(settings);

            var lockObj = new object();

            // Acquire should enter the monitor
            var disp = global::ClickIt.Utils.LockManager.Acquire(lockObj);
            try
            {
                // Monitor.IsEntered is available; ensure current thread owns the lock
                bool entered = System.Threading.Monitor.IsEntered(lockObj);
                entered.Should().BeTrue("Acquire must enter the monitor for non-null objects");

                // Dispose once releases the monitor
                disp.Dispose();

                // After disposing the first time, the lock should be released
                System.Threading.Monitor.IsEntered(lockObj).Should().BeFalse();

                // Calling Dispose a second time should be swallowed by the implementation (no exception)
                Action a = () => disp.Dispose();
                a.Should().NotThrow();
            }
            finally
            {
                // ensure no leftover lock in case of test failure
                try { while (Monitor.IsEntered(lockObj)) Monitor.Exit(lockObj); } catch { }
            }
        }

        [TestMethod]
        public void AcquireStatic_WithoutInstance_ReturnsNoopOrHandlesNullObject()
        {
            // Ensure global instance is null
            global::ClickIt.Utils.LockManager.Instance = null;

            // Null object should return noop
            using var n = global::ClickIt.Utils.LockManager.AcquireStatic(null);
            n.Should().NotBeNull();

            // Non-null object with no Instance returns noop (no Enter performed)
            var obj = new object();
            using var n2 = global::ClickIt.Utils.LockManager.AcquireStatic(obj);
            n2.Should().NotBeNull();
            // No exception and no hang
        }

        [TestMethod]
        public void AcquireStatic_WithInstance_AcquiresMonitor()
        {
            var settings = new ClickItSettings();
            var lm = new global::ClickIt.Utils.LockManager(settings);
            global::ClickIt.Utils.LockManager.Instance = lm;

            var o = new object();
            using var disp = global::ClickIt.Utils.LockManager.AcquireStatic(o);

            // Should have entered the monitor
            Monitor.IsEntered(o).Should().BeTrue();

            // Dispose releases
            disp.Dispose();
            Monitor.IsEntered(o).Should().BeFalse();
        }

        [TestMethod]
        public void GlobalLockManager_Proxy_PassesThroughToLockManagerInstance()
        {
            var settings = new ClickItSettings();
            var lm = new global::ClickIt.Utils.LockManager(settings);

            // proxy setter/getter
            global::ClickIt.Utils.GlobalLockManager.Instance = lm;
            global::ClickIt.Utils.GlobalLockManager.Instance.Should().BeSameAs(lm);

            // reset
            global::ClickIt.Utils.GlobalLockManager.Instance = null;
            global::ClickIt.Utils.GlobalLockManager.Instance.Should().BeNull();
        }
    }
}
