using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
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

        // RuntimeHelpers.GetUninitializedObject LabelOnGround here. The
        // runtime NullReferenceException in tests. This branch is validated
        // other consumers of LabelUtils, so we avoid unsafe low-level calls here.
    }
}
