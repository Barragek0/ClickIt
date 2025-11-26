using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServicePrivateTests
    {
        private static MethodInfo GetPrivate(string name)
        {
            var t = typeof(Services.LabelFilterService);
            var mi = t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull($"private method {name} should exist");
            return mi!;
        }

        [TestMethod]
        public void IsHarvestPath_MatchesIrrigatorAndExtractor()
        {
            var fn = GetPrivate("IsHarvestPath");
            ((bool)fn.Invoke(null, ["Harvest/Irrigator/whatever"])).Should().BeTrue();
            ((bool)fn.Invoke(null, ["Harvest/Extractor/whatever"])).Should().BeTrue();
            ((bool)fn.Invoke(null, ["NotHarvest/Thing"])).Should().BeFalse();
        }

        [TestMethod]
        public void IsSettlersOrePath_MatchesKnownSettlersPaths()
        {
            var fn = GetPrivate("IsSettlersOrePath");
            ((bool)fn.Invoke(null, ["CrimsonIron/path"])).Should().BeTrue();
            ((bool)fn.Invoke(null, ["copper_altar/something"])).Should().BeTrue();
            ((bool)fn.Invoke(null, ["PetrifiedWood/x"])).Should().BeTrue();
            ((bool)fn.Invoke(null, ["Bismuth/a"])).Should().BeTrue();
            ((bool)fn.Invoke(null, ["OtherThing"])).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_RequiresFlagsAndPath()
        {
            var fn = GetPrivate("ShouldClickAltar");
            // no path -> false
            ((bool)fn.Invoke(null, [false, false, false, false, string.Empty])).Should().BeFalse();

            // path contains altar but no flags -> false
            ((bool)fn.Invoke(null, [false, false, false, false, "CleansingFireAltar"])).Should().BeFalse();

            // any flag true + path containing altar -> true
            ((bool)fn.Invoke(null, [true, false, false, false, "CleansingFireAltar"])).Should().BeTrue();
            ((bool)fn.Invoke(null, [false, true, false, false, "TangleAltar"])).Should().BeTrue();
            ((bool)fn.Invoke(null, [false, false, true, false, "TangleAltar"])).Should().BeTrue();
        }
    }
}
