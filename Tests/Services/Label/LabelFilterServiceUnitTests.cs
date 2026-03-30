using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceUnitTests
    {
        [TestMethod]
        public void ShouldClickAltar_RespectsFlagsAndPath()
        {
            LabelFilterService.ShouldClickAltar(false, false, false, false, string.Empty).Should().BeFalse();

            LabelFilterService.ShouldClickAltar(false, false, false, false, "CleansingFireAltar").Should().BeFalse();

            LabelFilterService.ShouldClickAltar(true, false, false, false, "CleansingFireAltar").Should().BeTrue();
            LabelFilterService.ShouldClickAltar(false, true, false, false, "TangleAltar").Should().BeTrue();
            LabelFilterService.ShouldClickAltar(false, false, true, false, "CleansingFireAltar").Should().BeTrue();
        }
    }
}
