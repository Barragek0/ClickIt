using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Reflection;

namespace ClickIt.Tests.Rendering
{
    [TestClass]
    public class StrongboxRendererLogicTests
    {
        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsTrue_WhenSpecialIdentifierExists()
        {
            bool contains = InvokePrivateStatic<bool>(
                "ContainsStrongboxUniqueIdentifier",
                new List<string> { "special:strongbox-unique", "strongbox:armourers" });

            contains.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsStrongboxUniqueIdentifier_ReturnsFalse_WhenMissingOrEmpty()
        {
            InvokePrivateStatic<bool>("ContainsStrongboxUniqueIdentifier", new List<string>()).Should().BeFalse();
            InvokePrivateStatic<bool>("ContainsStrongboxUniqueIdentifier", (IReadOnlyList<string>?)null).Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_UniqueStrongbox_RespectsUniqueIdentifierLists()
        {
            string path = "Metadata/Chests/StrongBoxes/Arcanist";
            string name = "Arcanist's Strongbox";

            bool clickable = InvokePrivateStatic<bool>(
                "IsStrongboxClickableBySettings",
                path,
                name,
                new List<string> { "special:strongbox-unique" },
                new List<string>(),
                true);

            bool blocked = InvokePrivateStatic<bool>(
                "IsStrongboxClickableBySettings",
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

            bool clickable = InvokePrivateStatic<bool>(
                "IsStrongboxClickableBySettings",
                path,
                name,
                new List<string> { "StrongBoxes/Arcanist" },
                new List<string>(),
                false);

            bool blocked = InvokePrivateStatic<bool>(
                "IsStrongboxClickableBySettings",
                path,
                name,
                new List<string> { "StrongBoxes/Arcanist" },
                new List<string> { "StrongBoxes/Arcanist" },
                false);

            clickable.Should().BeTrue();
            blocked.Should().BeFalse();
        }

        private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
        {
            var method = typeof(global::ClickIt.Rendering.StrongboxRenderer)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

            method.Should().NotBeNull();
            return (T)method!.Invoke(null, args)!;
        }
    }
}