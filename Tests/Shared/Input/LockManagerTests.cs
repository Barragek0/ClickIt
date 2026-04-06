namespace ClickIt.Tests.Shared.Input
{
    [TestClass]
    [DoNotParallelize]
    public class LockManagerTests
    {
        [TestCleanup]
        public void TestCleanup()
        {
            LockManager.Instance = null;
            GlobalLockManager.Instance = null;
        }

        [TestMethod]
        public void Acquire_ReturnsNoopReleaser_WhenLockObjectIsNull()
        {
            Action act = () =>
            {
                using IDisposable releaser = LockManager.Acquire(null);
            };

            act.Should().NotThrow();
        }

        [TestMethod]
        public void Acquire_AcquiresAndReleasesMonitor_ForNonNullObject()
        {
            object sync = new();

            using (LockManager.Acquire(sync))
            {
                Monitor.IsEntered(sync).Should().BeTrue();
            }

            Monitor.IsEntered(sync).Should().BeFalse();
        }

        [TestMethod]
        public void Acquire_DisposeTwice_DoesNotThrow()
        {
            object sync = new();
            IDisposable releaser = LockManager.Acquire(sync);

            releaser.Dispose();
            Action act = releaser.Dispose;

            act.Should().NotThrow();
        }

        [TestMethod]
        public void AcquireStatic_ReturnsNoopReleaser_WhenInstanceMissing()
        {
            object sync = new();

            using (LockManager.AcquireStatic(sync))
            {
                Monitor.IsEntered(sync).Should().BeFalse();
            }
        }

        [TestMethod]
        public void AcquireStatic_AcquiresAndReleasesMonitor_WhenInstancePresent()
        {
            object sync = new();
            LockManager.Instance = new LockManager(new ClickItSettings());

            using (LockManager.AcquireStatic(sync))
            {
                Monitor.IsEntered(sync).Should().BeTrue();
            }

            Monitor.IsEntered(sync).Should().BeFalse();
        }

        [TestMethod]
        public void GlobalLockManager_Instance_DelegatesToLockManagerInstance()
        {
            var manager = new LockManager(new ClickItSettings());

            GlobalLockManager.Instance = manager;

            LockManager.Instance.Should().BeSameAs(manager);
            GlobalLockManager.Instance.Should().BeSameAs(manager);
        }
    }
}