using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Services.Click
{
    [TestClass]
    public class UltimatumLabelMathTests
    {
        [TestMethod]
        public void IsUltimatumPath_ReturnsTrue_OnlyForUltimatumInteractablePaths()
        {
            UltimatumLabelMath.IsUltimatumPath("Metadata/Terrain/Leagues/Ultimatum/Objects/UltimatumChallengeInteractable").Should().BeTrue();
            UltimatumLabelMath.IsUltimatumPath("Metadata/Terrain/Chests/Strongbox").Should().BeFalse();
            UltimatumLabelMath.IsUltimatumPath(null).Should().BeFalse();
            UltimatumLabelMath.IsUltimatumPath(string.Empty).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressInactiveUltimatumLabelCore_RequiresUltimatumPathAndInactiveLabel()
        {
            UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(isUltimatumPath: true, isUltimatumLabel: false).Should().BeTrue();
            UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(isUltimatumPath: true, isUltimatumLabel: true).Should().BeFalse();
            UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(isUltimatumPath: false, isUltimatumLabel: false).Should().BeFalse();
        }
    }
}