using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceBasicTests
    {
        [TestMethod]
        public void GetElementAccessLock_ReturnsSameObject()
        {
            // Using uninitialized instance to avoid full construction; we only need access to the method
            var svc = (ClickService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClickService));
            var lock1 = svc.GetElementAccessLock();
            var lock2 = svc.GetElementAccessLock();

            lock1.Should().NotBeNull();
            lock1.Should().BeSameAs(lock2);
        }
    }
}
