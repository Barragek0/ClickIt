using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceHelpersTests
    {
        private static MethodInfo GetShouldClickAltarMethod()
        {
            var t = typeof(Services.LabelFilterService);
            return t.GetMethod("ShouldClickAltar", BindingFlags.NonPublic | BindingFlags.Static)!;
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenNoFlagsEnabled()
        {
            var method = GetShouldClickAltarMethod();
            bool res = (bool)method.Invoke(null, [false, false, false, false, "CleansingFireAltar"])!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsTrue_WhenClickEaterEnabledAndPathMatches()
        {
            var method = GetShouldClickAltarMethod();
            bool res = (bool)method.Invoke(null, [false, false, true, false, "Some/Path/CleansingFireAltar"])!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsTrue_WhenHighlightExarchEnabledAndPathMatchesTangle()
        {
            var method = GetShouldClickAltarMethod();
            bool res = (bool)method.Invoke(null, [false, true, false, false, ".../TangleAltar/..."])!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenPathDoesNotMatch()
        {
            var method = GetShouldClickAltarMethod();
            bool res = (bool)method.Invoke(null, [true, true, true, true, "SomeOtherPath"])!;
            res.Should().BeFalse();
        }

        // Consolidated private-method tests (previously in LabelFilterServicePrivateTests.cs)
        [DataTestMethod]
        [DataRow("Harvest/Irrigator/whatever", true)]
        [DataRow("Harvest/Extractor/whatever", true)]
        [DataRow("NotHarvest/Thing", false)]
        public void IsHarvestPath_VariousInputs(string path, bool expect)
        {
            var mi = typeof(Services.LabelFilterService).GetMethod("IsHarvestPath", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();
            var res = (bool)mi.Invoke(null, new object[] { path })!;
            res.Should().Be(expect);
        }

        [DataTestMethod]
        [DataRow("CrimsonIron/path", true)]
        [DataRow("copper_altar/something", true)]
        [DataRow("PetrifiedWood/x", true)]
        [DataRow("Bismuth/a", true)]
        [DataRow("OtherThing", false)]
        public void IsSettlersOrePath_VariousInputs(string path, bool expect)
        {
            var mi = typeof(Services.LabelFilterService).GetMethod("IsSettlersOrePath", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();
            var res = (bool)mi.Invoke(null, new object[] { path })!;
            res.Should().Be(expect);
        }

        [DataTestMethod]
        [DataRow(false, false, false, false, "", false)]
        [DataRow(false, false, false, false, "CleansingFireAltar", false)]
        [DataRow(true, false, false, false, "CleansingFireAltar", true)]
        [DataRow(false, true, false, false, "TangleAltar", true)]
        [DataRow(false, false, true, false, "TangleAltar", true)]
        public void ShouldClickAltar_VariousFlagCombinations(bool f1, bool f2, bool f3, bool f4, string path, bool expect)
        {
            var mi = typeof(Services.LabelFilterService).GetMethod("ShouldClickAltar", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();
            var res = (bool)mi.Invoke(null, new object[] { f1, f2, f3, f4, path })!;
            res.Should().Be(expect);
        }
    }
}
