using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System.Threading;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LockManagerAdditionalTests
    {
        [TestMethod]
        public void Acquire_WithNull_ReturnsNoopAndDisposeSafe()
        {
            var lm = new LockManager(new ClickItSettings());
            using (var d = lm.Acquire(null))
            {
                d.Should().NotBeNull();
            }
        }

        [TestMethod]
        public void Acquire_AcquiresAndReleasesLock()
        {
            var lm = new LockManager(new ClickItSettings());
            var o = new object();

            using (var d = lm.Acquire(o))
            {
                // while inside the acquired scope the current thread should be marked as owning the monitor
                Monitor.IsEntered(o).Should().BeTrue();
            }

            // After disposal we should no longer own the lock
            Monitor.IsEntered(o).Should().BeFalse();
        }

        [TestMethod]
        public void AcquireStatic_WithNullInstance_ReturnsNoop()
        {
            LockManager.Instance = null;
            var o = new object();
            using (var d = LockManager.AcquireStatic(o))
            {
                d.Should().NotBeNull(); // noop
            }
        }

        [TestMethod]
        public void AcquireStatic_WithInstance_AcquiresLock()
        {
            LockManager.Instance = new LockManager(new ClickItSettings());
            var o = new object();
            using (var d = LockManager.AcquireStatic(o))
            {
                Monitor.IsEntered(o).Should().BeTrue();
            }

            // after disposal we should no longer own the lock
            Monitor.IsEntered(o).Should().BeFalse();
        }

        [TestMethod]
        public void GlobalLockManager_MapsToStaticInstance()
        {
            var lm = new LockManager(new ClickItSettings());
            GlobalLockManager.Instance = lm;
            LockManager.Instance.Should().Be(lm);
            GlobalLockManager.Instance.Should().Be(lm);
        }
    }
}
