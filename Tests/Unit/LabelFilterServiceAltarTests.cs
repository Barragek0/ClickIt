using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceAltarTests
    {
        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenNoFlagsEnabledOrPathEmpty()
        {
            // Call private static ShouldClickAltar directly via reflection
            var mi = typeof(Services.LabelFilterService).GetMethod("ShouldClickAltar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            bool res = (bool)mi.Invoke(null, new object[] { false, false, false, false, "" })!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsTrue_WhenFlagEnabled_AndPathContainsAltar()
        {
            var mi = typeof(Services.LabelFilterService).GetMethod("ShouldClickAltar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

            // enable highlightEater but use CleansingFireAltar path -> should still match
            bool res1 = (bool)mi.Invoke(null, new object[] { true, false, false, false, "CleansingFireAltar/abc" })!;
            res1.Should().BeTrue();

            // enable clickExarch and use TangleAltar path
            bool res2 = (bool)mi.Invoke(null, new object[] { false, true, false, true, "TangleAltar/whatever" })!;
            res2.Should().BeTrue();
        }
    }
}
