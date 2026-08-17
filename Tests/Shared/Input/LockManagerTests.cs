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
    }
}