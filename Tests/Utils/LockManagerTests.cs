using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using System.Threading;
using ClickIt.Utils;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class LockManagerTests
    {
        [TestMethod]
        public void Acquire_WhenLockingDisabled_DoesNotLockObject()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.UseLocking.Value = false;
            var mgr = new LockManager(settings);

            var lockObj = new object();
            using (var d = mgr.Acquire(lockObj))
            {
                // Since locking is disabled, another thread should be able to enter the monitor immediately
                bool otherThreadEntered = false;
                var t = Task.Run(() =>
                {
                    try
                    {
                        if (Monitor.TryEnter(lockObj, 200))
                        {
                            otherThreadEntered = true;
                            Monitor.Exit(lockObj);
                        }
                    }
                    catch { }
                });
                t.Wait(1000).Should().BeTrue();
                otherThreadEntered.Should().BeTrue();
            }
        }

        [TestMethod]
        public void Acquire_WhenLockingEnabled_LocksObjectAcrossThreads()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.UseLocking.Value = true;
            var mgr = new LockManager(settings);

            var lockObj = new object();

            using (var d = mgr.Acquire(lockObj))
            {
                // Another thread should NOT be able to acquire the lock while held
                bool otherThreadEntered = false;
                var t = Task.Run(() =>
                {
                    try
                    {
                        if (Monitor.TryEnter(lockObj, 200))
                        {
                            otherThreadEntered = true;
                            Monitor.Exit(lockObj);
                        }
                    }
                    catch { }
                });
                t.Wait(1000).Should().BeTrue();
                otherThreadEntered.Should().BeFalse();
            }

            // After disposing, other thread should be able to enter
            bool enteredAfterDispose = false;
            if (Monitor.TryEnter(lockObj, 200))
            {
                enteredAfterDispose = true;
                Monitor.Exit(lockObj);
            }
            enteredAfterDispose.Should().BeTrue();
        }

        [TestMethod]
        public void Acquire_NullObject_ReturnsNoopAndDoesNotThrow()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.UseLocking.Value = true; // even when enabled, null should return noop
            var mgr = new LockManager(settings);

            Action act = () =>
            {
                using (var d = mgr.Acquire(null))
                {
                    // nothing
                }
            };

            act.Should().NotThrow();
        }

        [TestMethod]
        public void GlobalLockManager_Wrapper_GetSet_Works()
        {
            var settings = new ClickIt.ClickItSettings();
            var mgr = new LockManager(settings);
            GlobalLockManager.Instance = mgr;
            GlobalLockManager.Instance.Should().Be(mgr);
            GlobalLockManager.Instance = null;
            GlobalLockManager.Instance.Should().BeNull();
        }
    }
}
