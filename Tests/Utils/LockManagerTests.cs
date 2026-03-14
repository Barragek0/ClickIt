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
            _ = new global::ClickIt.Utils.LockManager(settings);

            using var d = global::ClickIt.Utils.LockManager.Acquire(null);
            d.Should().NotBeNull();

            using var d2 = global::ClickIt.Utils.LockManager.Acquire(null);
            d2.Should().BeSameAs(d);
        }

        [TestMethod]
        public void Acquire_WithObject_EntersMonitorAndExitsOnDispose()
        {
            var settings = new ClickItSettings();
            _ = new global::ClickIt.Utils.LockManager(settings);

            var lockObj = new object();

            var disp = global::ClickIt.Utils.LockManager.Acquire(lockObj);
            try
            {
                bool entered = Monitor.IsEntered(lockObj);
                entered.Should().BeTrue("Acquire must enter the monitor for non-null objects");

                disp.Dispose();

                Monitor.IsEntered(lockObj).Should().BeFalse();

                Action a = () => disp.Dispose();
                a.Should().NotThrow();
            }
            finally
            {
                try { while (Monitor.IsEntered(lockObj)) Monitor.Exit(lockObj); } catch { }
            }
        }

        [TestMethod]
        public void AcquireStatic_WithoutInstance_ReturnsNoopOrHandlesNullObject()
        {
            global::ClickIt.Utils.LockManager.Instance = null;

            using var n = global::ClickIt.Utils.LockManager.AcquireStatic(null);
            n.Should().NotBeNull();

            var obj = new object();
            using var n2 = global::ClickIt.Utils.LockManager.AcquireStatic(obj);
            n2.Should().NotBeNull();
            n2.Should().BeSameAs(n);
        }

        [TestMethod]
        public void AcquireStatic_WithInstance_AcquiresMonitor()
        {
            var settings = new ClickItSettings();
            var lm = new global::ClickIt.Utils.LockManager(settings);
            global::ClickIt.Utils.LockManager.Instance = lm;

            var o = new object();
            using var disp = global::ClickIt.Utils.LockManager.AcquireStatic(o);

            Monitor.IsEntered(o).Should().BeTrue();

            disp.Dispose();
            Monitor.IsEntered(o).Should().BeFalse();
        }

        [TestMethod]
        public void GlobalLockManager_Proxy_PassesThroughToLockManagerInstance()
        {
            var settings = new ClickItSettings();
            var lm = new global::ClickIt.Utils.LockManager(settings);

            global::ClickIt.Utils.GlobalLockManager.Instance = lm;
            global::ClickIt.Utils.GlobalLockManager.Instance.Should().BeSameAs(lm);

            global::ClickIt.Utils.GlobalLockManager.Instance = null;
            global::ClickIt.Utils.GlobalLockManager.Instance.Should().BeNull();
        }
    }
}
