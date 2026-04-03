using ClickIt.Features.Labels.Classification;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Labels.Classification
{
    [TestClass]
    public class MechanicClassifierAltarTests
    {
        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenNoFlagsEnabledOrPathEmpty()
        {
            MechanicClassifier.ShouldClickAltar(false, false, false, false, string.Empty).Should().BeFalse();
            MechanicClassifier.ShouldClickAltar(false, false, false, false, "CleansingFireAltar").Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsTrue_WhenEnabledFlagMatchesAltarPath()
        {
            MechanicClassifier.ShouldClickAltar(true, false, false, false, "CleansingFireAltar/abc").Should().BeTrue();
            MechanicClassifier.ShouldClickAltar(false, true, false, true, "TangleAltar/whatever").Should().BeTrue();
            MechanicClassifier.ShouldClickAltar(false, false, true, false, "CleansingFireAltar").Should().BeTrue();
        }
    }
}