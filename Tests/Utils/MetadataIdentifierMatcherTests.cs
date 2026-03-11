using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class MetadataIdentifierMatcherTests
    {
        [TestMethod]
        public void ContainsSingle_PathFragment_WithSegmentBoundaries_Matches()
        {
            bool result = global::ClickIt.Utils.MetadataIdentifierMatcher.ContainsSingle(
                "Metadata/Chests/StrongBoxes/Arcanist",
                string.Empty,
                "StrongBoxes/Arcanist");

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsSingle_PathFragment_DoesNotMatchInsideLongerSegment()
        {
            bool result = global::ClickIt.Utils.MetadataIdentifierMatcher.ContainsSingle(
                "Metadata/Chests/StrongBoxes/StrongboxScarab",
                string.Empty,
                "StrongBoxes/Strongbox");

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ContainsSingle_NameIdentifier_UsesItemNameContains()
        {
            bool result = global::ClickIt.Utils.MetadataIdentifierMatcher.ContainsSingle(
                "Metadata/Chests/StrongBoxes/Strongbox",
                "Perandus Bank",
                "name:Perandus Bank");

            result.Should().BeTrue();
        }
    }
}
