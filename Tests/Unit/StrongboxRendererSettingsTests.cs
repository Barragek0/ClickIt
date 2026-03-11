using System.Reflection;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class StrongboxRendererSettingsTests
    {
        private static bool InvokeIsStrongboxClickableBySettings(
            string path,
            string name,
            string[] clickMetadata,
            string[] dontClickMetadata)
        {
            return InvokeIsStrongboxClickableBySettings(path, name, clickMetadata, dontClickMetadata, isUniqueStrongbox: false);
        }

        private static bool InvokeIsStrongboxClickableBySettings(
            string path,
            string name,
            string[] clickMetadata,
            string[] dontClickMetadata,
            bool isUniqueStrongbox)
        {
            MethodInfo? mi = typeof(Rendering.StrongboxRenderer)
                .GetMethod("IsStrongboxClickableBySettings", BindingFlags.NonPublic | BindingFlags.Static, null,
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(IReadOnlyList<string>),
                        typeof(IReadOnlyList<string>),
                        typeof(bool)
                    },
                    null);
            mi.Should().NotBeNull();

            object? result = mi!.Invoke(null, new object[] { path, name, clickMetadata, dontClickMetadata, isUniqueStrongbox });
            result.Should().BeOfType<bool>();
            return (bool)result!;
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_MatchesMetadataFragment()
        {
            bool result = InvokeIsStrongboxClickableBySettings(
                "Metadata/Chests/StrongBoxes/Arcanist",
                "Arcanist's Strongbox",
                new[] { "StrongBoxes/Arcanist" },
                System.Array.Empty<string>());

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_MatchesNameRule()
        {
            bool result = InvokeIsStrongboxClickableBySettings(
                "Metadata/Chests/StrongBoxes/Strongbox",
                "Perandus Bank",
                new[] { "name:Perandus Bank" },
                System.Array.Empty<string>());

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_DontClickRuleWins()
        {
            bool result = InvokeIsStrongboxClickableBySettings(
                "Metadata/Chests/StrongBoxes/Arcanist",
                "Arcanist's Strongbox",
                new[] { "StrongBoxes/Arcanist" },
                new[] { "StrongBoxes/Arcanist" });

            result.Should().BeFalse();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_OperativeDoesNotMatchRegularStrongboxRule()
        {
            bool result = InvokeIsStrongboxClickableBySettings(
                "Metadata/Chests/StrongBoxes/StrongboxScarab",
                "Operative's Strongbox",
                new[] { "StrongBoxes/StrongboxScarab" },
                new[] { "StrongBoxes/Strongbox" });

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_UniqueRuleMatchesByRarity()
        {
            bool result = InvokeIsStrongboxClickableBySettings(
                "Metadata/Chests/StrongBoxes/Strongbox",
                "Strongbox",
                new[] { "special:strongbox-unique" },
                System.Array.Empty<string>(),
                isUniqueStrongbox: true);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsStrongboxClickableBySettings_UniqueDontClickRuleWinsByRarity()
        {
            bool result = InvokeIsStrongboxClickableBySettings(
                "Metadata/Chests/StrongBoxes/Strongbox",
                "Strongbox",
                new[] { "StrongBoxes/Strongbox" },
                new[] { "special:strongbox-unique" },
                isUniqueStrongbox: true);

            result.Should().BeFalse();
        }
    }
}
