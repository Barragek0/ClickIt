using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services.Label.Classification;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceAltarTests
    {
        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenNoFlagsEnabledOrPathEmpty()
        {
            bool res = MechanicClassifier.ShouldClickAltar(false, false, false, false, string.Empty);
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsTrue_WhenFlagEnabled_AndPathContainsAltar()
        {
            bool res1 = MechanicClassifier.ShouldClickAltar(true, false, false, false, "CleansingFireAltar/abc");
            res1.Should().BeTrue();

            bool res2 = MechanicClassifier.ShouldClickAltar(false, true, false, true, "TangleAltar/whatever");
            res2.Should().BeTrue();
        }
    }
}
