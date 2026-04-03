using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.UserInterface
{
    [TestClass]
    public class StrongboxRendererLogicTests
    {
        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsTrue_WhenSpecialIdentifierExists()
        {
            bool contains = StrongboxRenderer.ContainsStrongboxUniqueIdentifier(
                new List<string> { "special:strongbox-unique", "strongbox:armourers" });

            contains.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsFalse_WhenMissingOrEmpty()
        {
            StrongboxRenderer.ContainsStrongboxUniqueIdentifier(new List<string>()).Should().BeFalse();
            StrongboxRenderer.ContainsStrongboxUniqueIdentifier((IReadOnlyList<string>?)null).Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_UniqueStrongbox_RespectsUniqueIdentifierLists()
        {
            string path = "Metadata/Chests/StrongBoxes/Arcanist";
            string name = "Arcanist's Strongbox";

            bool clickable = StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "special:strongbox-unique" },
                new List<string>(),
                true);

            bool blocked = StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "special:strongbox-unique" },
                new List<string> { "special:strongbox-unique" },
                true);

            clickable.Should().BeTrue();
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_NonUnique_UsesDontClickPrecedence()
        {
            string path = "Metadata/Chests/StrongBoxes/Arcanist";
            string name = "Arcanist's Strongbox";

            bool clickable = StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "StrongBoxes/Arcanist" },
                new List<string>(),
                false);

            bool blocked = StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "StrongBoxes/Arcanist" },
                new List<string> { "StrongBoxes/Arcanist" },
                false);

            clickable.Should().BeTrue();
            blocked.Should().BeFalse();
        }
    }
}