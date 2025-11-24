using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Threading;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class LockingTests
    {
        [TestMethod]
        public void Acquire_InstanceAcquiresLock()
        {
            var settings = new ClickIt.ClickItSettings();
            var lm = new ClickIt.Utils.LockManager(settings);

            var lockObj = new object();
            using (var releaser = lm.Acquire(lockObj))
            {
                // While acquired by this instance, another thread should NOT be able to acquire it
                bool acquiredByOther = false;
                using (var done = new System.Threading.ManualResetEvent(false))
                {
                    var t = new System.Threading.Thread(() =>
                    {
                        bool otherTaken = System.Threading.Monitor.TryEnter(lockObj, 200);
                        if (otherTaken)
                        {
                            System.Threading.Monitor.Exit(lockObj);
                            acquiredByOther = true;
                        }
                        done.Set();
                    });
                    t.IsBackground = true;
                    t.Start();
                    done.WaitOne(1000);
                }
                acquiredByOther.Should().BeFalse();
            }
        }

        [TestMethod]
        public void Acquire_WithLocking_AcquiresLock()
        {
            var settings = new ClickIt.ClickItSettings();
            var lm = new ClickIt.Utils.LockManager(settings);

            var lockObj = new object();
            using (var releaser = lm.Acquire(lockObj))
            {
                // While acquired, another thread should NOT be able to acquire it
                bool acquiredByOther = false;
                using (var done = new System.Threading.ManualResetEvent(false))
                {
                    var t = new System.Threading.Thread(() =>
                    {
                        bool otherTaken = System.Threading.Monitor.TryEnter(lockObj, 200);
                        if (otherTaken)
                        {
                            System.Threading.Monitor.Exit(lockObj);
                            acquiredByOther = true;
                        }
                        done.Set();
                    });
                    t.IsBackground = true;
                    t.Start();
                    // Wait for the worker to finish
                    done.WaitOne(1000);
                }
                acquiredByOther.Should().BeFalse();
            }

            // After release, the main thread can enter
            bool nowTaken = Monitor.TryEnter(lockObj, 0);
            try
            {
                nowTaken.Should().BeTrue();
            }
            finally
            {
                if (nowTaken) Monitor.Exit(lockObj);
            }
        }

        [TestMethod]
        public void Setting_GlobalInstance_MapsToLockManagerInstance()
        {
            var settings = new ClickIt.ClickItSettings();
            var lm = new ClickIt.Utils.LockManager(settings);
            ClickIt.Utils.GlobalLockManager.Instance = lm;
            ClickIt.Utils.LockManager.Instance.Should().BeSameAs(lm);
            // restore
            ClickIt.Utils.GlobalLockManager.Instance = null;
            ClickIt.Utils.LockManager.Instance.Should().BeNull();
        }

        [TestMethod]
        public void Acquire_NullLockObj_ReturnsNoop()
        {
            var settings = new ClickIt.ClickItSettings();
            var lm = new ClickIt.Utils.LockManager(settings);
            using (var d = lm.Acquire(null))
            {
                d.Should().NotBeNull();
            }
        }
    }
}
