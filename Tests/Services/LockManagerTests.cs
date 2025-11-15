#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Threading;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class LockManagerTests
    {
        [TestMethod]
        public void Acquire_NoLocking_ReturnsNoop()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.UseLocking.Value = false;
            var lm = new ClickIt.Utils.LockManager(settings);

            var lockObj = new object();
            using (var releaser = lm.Acquire(lockObj))
            {
                // When locking disabled, should not block the Monitor
                bool taken = Monitor.TryEnter(lockObj, 0);
                if (taken) Monitor.Exit(lockObj);
                taken.Should().BeTrue();
            }
        }

        [TestMethod]
        public void Acquire_WithLocking_AcquiresLock()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.UseLocking.Value = true;
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
    }
}
#endif
