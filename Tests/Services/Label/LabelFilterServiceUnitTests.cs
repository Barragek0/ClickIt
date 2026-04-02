using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services.Label.Classification;

namespace ClickIt.Tests.Label
{
    [TestClass]
    public class LabelFilterServiceUnitTests
    {
        [TestMethod]
        public void ShouldClickAltar_RespectsFlagsAndPath()
        {
            MechanicClassifier.ShouldClickAltar(false, false, false, false, string.Empty).Should().BeFalse();

            MechanicClassifier.ShouldClickAltar(false, false, false, false, "CleansingFireAltar").Should().BeFalse();

            MechanicClassifier.ShouldClickAltar(true, false, false, false, "CleansingFireAltar").Should().BeTrue();
            MechanicClassifier.ShouldClickAltar(false, true, false, false, "TangleAltar").Should().BeTrue();
            MechanicClassifier.ShouldClickAltar(false, false, true, false, "CleansingFireAltar").Should().BeTrue();
        }
    }
}
