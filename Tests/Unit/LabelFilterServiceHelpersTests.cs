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
    }
}
