using System.Reflection;
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
            MethodInfo? mi = typeof(Rendering.StrongboxRenderer)
                .GetMethod("IsStrongboxClickableBySettings", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            object? result = mi!.Invoke(null, new object[] { path, name, clickMetadata, dontClickMetadata });
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
    }
}
