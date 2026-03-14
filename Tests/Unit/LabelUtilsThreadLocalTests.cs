using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsThreadLocalTests
    {
        [TestMethod]
        public void ClearThreadLocalStorage_EmptiesInternalThreadLocalList()
        {
            var fi = typeof(global::ClickIt.Utils.LabelUtils).GetField("_threadLocalElementsList", BindingFlags.NonPublic | BindingFlags.Static);
            fi.Should().NotBeNull("internal thread-local field should exist");

            var threadLocalObj = fi!.GetValue(null);
            threadLocalObj.Should().NotBeNull();

            var tlType = threadLocalObj!.GetType();
            var valueProp = tlType.GetProperty("Value");
            valueProp.Should().NotBeNull();

            var listInstance = System.Activator.CreateInstance(valueProp!.PropertyType);
            var addMethod = valueProp.PropertyType.GetMethod("Add");
            addMethod!.Invoke(listInstance, [null]);
            addMethod!.Invoke(listInstance, [null]);

            valueProp.SetValue(threadLocalObj, listInstance);

            var countProp = valueProp.PropertyType.GetProperty("Count");
            var initialCount = (int)countProp!.GetValue(listInstance)!;
            initialCount.Should().BeGreaterThan(0);

            global::ClickIt.Utils.LabelUtils.ClearThreadLocalStorage();

            var afterVal = valueProp.GetValue(threadLocalObj);
            var afterCount = (int)countProp!.GetValue(afterVal)!;
            afterCount.Should().Be(0);
        }
    }
}
