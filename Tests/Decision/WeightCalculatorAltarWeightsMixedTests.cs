using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt.Components;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class WeightCalculatorAltarWeightsMixedTests
    {
        [TestMethod]
        public void InitializeFromArrays_MixedNullAndNonNull_PreservesProvided()
        {
            // Arrange
            var topDown = (decimal[])null;
            var bottomDown = new decimal[8];
            bottomDown[3] = 5m;
            var topUp = new decimal[8];
            topUp[0] = 2m;
            var bottomUp = (decimal[])null;

            var aw = new AltarWeights();

            // Act
            aw.InitializeFromArrays(topDown, bottomDown, topUp, bottomUp);

            // Assert
            aw.GetBottomDownsideWeights()[3].Should().Be(5m);
            aw.GetTopUpsideWeights()[0].Should().Be(2m);
            // Other slots should be zero
            aw.GetTopUpsideWeights()[1].Should().Be(0m);
            aw.GetBottomUpsideWeights()[0].Should().Be(0m);
        }

        [TestMethod]
        public void Indexer_SetAcrossTypes_StoredSeparately()
        {
            // Arrange
            var aw = new AltarWeights();
            aw.InitializeFromArrays(null, null, null, null);

            // Act - set same index across different weight type keys
            aw["topdownside", 1] = 11m;
            aw["bottomupside", 1] = 22m;
            aw["topupside", 1] = 33m;

            // Assert - each stored value should be retrievable independently
            aw["topdownside", 1].Should().Be(11m);
            aw["bottomupside", 1].Should().Be(22m);
            aw["topupside", 1].Should().Be(33m);

            // And array getters should reflect values
            aw.GetTopDownsideWeights()[1].Should().Be(11m);
            aw.GetBottomUpsideWeights()[1].Should().Be(22m);
            aw.GetTopUpsideWeights()[1].Should().Be(33m);
        }
    }
}
