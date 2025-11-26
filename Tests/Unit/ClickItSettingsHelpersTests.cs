using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItSettingsHelpersTests
    {
        [TestMethod]
        public void MatchesSearchFilter_BehavesAsExpected()
        {
            var type = typeof(ClickItSettings);
            var mi = type.GetMethod("MatchesSearchFilter", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            // empty filter should match
            ((bool)mi!.Invoke(null, ["My Name", "Type", string.Empty])).Should().BeTrue();

            // matches name case-insensitive
            ((bool)mi.Invoke(null, ["My Name", "Type", "my"])).Should().BeTrue();

            // matches type
            ((bool)mi.Invoke(null, ["Foo", "Player", "player"])).Should().BeTrue();

            // non-match
            ((bool)mi.Invoke(null, ["Foo", "Bar", "zzz"])).Should().BeFalse();
        }

        [TestMethod]
        public void GetUpsideSectionHeader_MapsTypesCorrectly()
        {
            var type = typeof(ClickItSettings);
            var mi = type.GetMethod("GetUpsideSectionHeader", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            ((string)mi!.Invoke(null, ["Minion"])).Should().Be("Minion Drops");
            ((string)mi.Invoke(null, ["Boss"])).Should().Be("Boss Drops");
            ((string)mi.Invoke(null, ["Player"])).Should().Be("Player Bonuses");
            ((string)mi.Invoke(null, ["Unknown"])).Should().Be(string.Empty);
        }

        [TestMethod]
        public void GetDownsideSectionHeader_MapsWeightsCorrectly()
        {
            var type = typeof(ClickItSettings);
            var mi = type.GetMethod("GetDownsideSectionHeader", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            ((string)mi!.Invoke(null, [100])).Should().Be("Build Bricking Modifiers");
            ((string)mi.Invoke(null, [80])).Should().Be("Very Dangerous Modifiers");
            ((string)mi.Invoke(null, [50])).Should().Be("Dangerous Modifiers");
            ((string)mi.Invoke(null, [10])).Should().Be("Ok Modifiers");
            ((string)mi.Invoke(null, [0])).Should().Be("Free Modifiers");
        }
    }
}
