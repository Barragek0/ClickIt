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
            bool res = Services.LabelFilterService.ShouldClickAltar(false, false, false, false, string.Empty);
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsTrue_WhenFlagEnabled_AndPathContainsAltar()
        {
            bool res1 = Services.LabelFilterService.ShouldClickAltar(true, false, false, false, "CleansingFireAltar/abc");
            res1.Should().BeTrue();

            bool res2 = Services.LabelFilterService.ShouldClickAltar(false, true, false, true, "TangleAltar/whatever");
            res2.Should().BeTrue();
        }
    }
}
