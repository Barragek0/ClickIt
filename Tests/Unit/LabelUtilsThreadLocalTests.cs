using Microsoft.VisualStudio.TestTools.UnitTesting;
#nullable enable
using FluentAssertions;
using System.Reflection;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsThreadLocalTests
    {
        [TestMethod]
        public void ClearThreadLocalStorage_EmptiesInternalThreadLocalList()
        {
            // Access the private static ThreadLocal<List<Element>> field
            var fi = typeof(global::ClickIt.Utils.LabelUtils).GetField("_threadLocalElementsList", BindingFlags.NonPublic | BindingFlags.Static);
            fi.Should().NotBeNull("internal thread-local field should exist");

            var threadLocalObj = fi!.GetValue(null);
            threadLocalObj.Should().NotBeNull();

            // Use reflection to interact with the ThreadLocal<T>.Value property so we don't need to refer
            // to the Element type at compile time. Create an instance of the T (List<Element>) and add
            // a couple of null entries to simulate populated state.
            var tlType = threadLocalObj!.GetType();
            var valueProp = tlType.GetProperty("Value");
            valueProp.Should().NotBeNull();

            var listInstance = System.Activator.CreateInstance(valueProp!.PropertyType);
            var addMethod = valueProp.PropertyType.GetMethod("Add");
            addMethod!.Invoke(listInstance, [null]);
            addMethod!.Invoke(listInstance, [null]);

            // Set the created list as the thread-local value
            valueProp.SetValue(threadLocalObj, listInstance);

            // Verify there are >0 items
            var countProp = valueProp.PropertyType.GetProperty("Count");
            var initialCount = (int)countProp!.GetValue(listInstance)!;
            initialCount.Should().BeGreaterThan(0);

            // Call the public clear method -> should empty the list
            global::ClickIt.Utils.LabelUtils.ClearThreadLocalStorage();

            // Read back the thread-local list and ensure it's empty
            var afterVal = valueProp.GetValue(threadLocalObj);
            var afterCount = (int)countProp!.GetValue(afterVal)!;
            afterCount.Should().Be(0);
        }
    }
}
