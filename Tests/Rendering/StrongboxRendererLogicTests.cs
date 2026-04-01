using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Rendering
{
    [TestClass]
    public class StrongboxRendererLogicTests
    {
        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsTrue_WhenSpecialIdentifierExists()
        {
            bool contains = global::ClickIt.Rendering.StrongboxRenderer.ContainsStrongboxUniqueIdentifier(
                new List<string> { "special:strongbox-unique", "strongbox:armourers" });

            contains.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsFalse_WhenMissingOrEmpty()
        {
            global::ClickIt.Rendering.StrongboxRenderer.ContainsStrongboxUniqueIdentifier(new List<string>()).Should().BeFalse();
            global::ClickIt.Rendering.StrongboxRenderer.ContainsStrongboxUniqueIdentifier((IReadOnlyList<string>?)null).Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_UniqueStrongbox_RespectsUniqueIdentifierLists()
        {
            string path = "Metadata/Chests/StrongBoxes/Arcanist";
            string name = "Arcanist's Strongbox";

            bool clickable = global::ClickIt.Rendering.StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "special:strongbox-unique" },
                new List<string>(),
                true);

            bool blocked = global::ClickIt.Rendering.StrongboxRenderer.IsStrongboxClickableBySettings(
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

            bool clickable = global::ClickIt.Rendering.StrongboxRenderer.IsStrongboxClickableBySettings(
                path,
                name,
                new List<string> { "StrongBoxes/Arcanist" },
                new List<string>(),
                false);

            bool blocked = global::ClickIt.Rendering.StrongboxRenderer.IsStrongboxClickableBySettings(
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