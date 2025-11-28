using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
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
            // Since runtime construction is heavy, create an uninitialized instance and initialize the internal lock
            var svc = (ClickService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClickService));
            // Inject a lock object so GetElementAccessLock returns a real object
            var fld = typeof(ClickService).GetField("_elementAccessLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fld == null)
            {
                fld = typeof(ClickService).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(f => f.Name.IndexOf("elementAccess", System.StringComparison.OrdinalIgnoreCase) >= 0);
            }
            fld!.SetValue(svc, new object());
            var lock1 = svc.GetElementAccessLock();
            var lock2 = svc.GetElementAccessLock();

            // In some test contexts the lock may not be initialized on uninitialized objects,
            // but the important behaviour is that repeated calls return the same instance.
            lock1.Should().BeSameAs(lock2);
            lock1.Should().BeSameAs(lock2);
        }
    }
}
