using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LockManagerTests
    {
        [TestMethod]
        public void AcquireStatic_ReturnsDisposable_Noop_WhenNull()
        {
            using var r = LockManager.AcquireStatic(null);
            r.Should().NotBeNull();
        }

        [TestMethod]
        public void Acquire_ReturnsReleaser_ForObject()
        {
            var lm = new LockManager(new ClickItSettings());
            using var r = LockManager.Acquire(new object());
            r.Should().NotBeNull();
        }

        [TestMethod]
        public void Acquire_WithNull_ReturnsNoopAndDisposeSafe()
        {
            using (var d = LockManager.Acquire(null))
            {
                d.Should().NotBeNull();
            }
        }

        [TestMethod]
        public void AcquireStatic_WithNullInstance_ReturnsNoop()
        {
            LockManager.Instance = null;
            var o = new object();
            using (var d = LockManager.AcquireStatic(o))
            {
                d.Should().NotBeNull();
            }
        }

        [TestMethod]
        public void AcquireStatic_WithInstance_AcquiresLock()
        {
            LockManager.Instance = new LockManager(new ClickItSettings());
            var o = new object();
            using (var d = LockManager.AcquireStatic(o))
            {
                System.Threading.Monitor.IsEntered(o).Should().BeTrue();
            }
            System.Threading.Monitor.IsEntered(o).Should().BeFalse();
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
