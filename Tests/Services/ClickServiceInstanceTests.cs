using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class ClickServiceInstanceTests
    {
        [TestMethod]
        public void GetElementAccessLock_Returns_PrivateField_WhenSetOnUninitializedInstance()
        {
            var csType = Type.GetType("ClickIt.Services.ClickService, ClickIt");
            csType.Should().NotBeNull();

            // Create an instance without running the constructor to avoid heavy dependencies
            var instance = FormatterServices.GetUninitializedObject(csType);
            instance.Should().NotBeNull();

            // Set the private _elementAccessLock field manually
            var lockField = csType.GetField("_elementAccessLock", BindingFlags.NonPublic | BindingFlags.Instance);
            lockField.Should().NotBeNull("_elementAccessLock private field should exist");

            var sentinel = new object();
            lockField.SetValue(instance, sentinel);

            // Invoke the public GetElementAccessLock() method and verify it returns the same object
            var getLock = csType.GetMethod("GetElementAccessLock", BindingFlags.Public | BindingFlags.Instance);
            getLock.Should().NotBeNull();

            var returned = getLock.Invoke(instance, null);
            returned.Should().BeSameAs(sentinel);
        }

        [TestMethod]
        public void TwoInstances_Have_Different_ElementAccessLock_Objects_WhenSet()
        {
            var csType = Type.GetType("ClickIt.Services.ClickService, ClickIt");
            csType.Should().NotBeNull();

            var a = FormatterServices.GetUninitializedObject(csType);
            var b = FormatterServices.GetUninitializedObject(csType);

            var lockField = csType.GetField("_elementAccessLock", BindingFlags.NonPublic | BindingFlags.Instance);
            lockField.Should().NotBeNull();

            var sa = new object();
            var sb = new object();
            lockField.SetValue(a, sa);
            lockField.SetValue(b, sb);

            var getLock = csType.GetMethod("GetElementAccessLock", BindingFlags.Public | BindingFlags.Instance);
            getLock.Should().NotBeNull();

            var ra = getLock.Invoke(a, null);
            var rb = getLock.Invoke(b, null);

            ra.Should().BeSameAs(sa);
            rb.Should().BeSameAs(sb);
            ra.Should().NotBeSameAs(rb);
        }
    }
}
