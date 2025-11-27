using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsNullAndBasicTests
    {
        [TestMethod]
        public void GetElementsByStringContains_NullElement_ReturnsEmptyList()
        {
            var result = LabelUtils.GetElementsByStringContains(null, "whatever");
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [TestMethod]
        public void GetElementByString_NullRoot_ReturnsNull()
        {
            var result = LabelUtils.GetElementByString(null, "abc");
            result.Should().BeNull();
        }

        // Note: we intentionally don't invoke HasEssenceImprisonmentText with a
        // RuntimeHelpers.GetUninitializedObject LabelOnGround here. The
        // external ExileCore type performs unsafe native memory access when its
        // properties are accessed on uninitialized instances which will cause a
        // runtime NullReferenceException in tests. This branch is validated
        // indirectly by higher-level tests that exercise LabelFilterService and
        // other consumers of LabelUtils, so we avoid unsafe low-level calls here.
    }
}
