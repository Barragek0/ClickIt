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
    }
}
