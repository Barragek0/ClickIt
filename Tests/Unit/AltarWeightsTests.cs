using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarWeightsTests
    {
        [TestMethod]
        public void InitializeFromArrays_PopulatesArraysAndIndexerWorks()
        {
            var weights = new AltarWeights();

            var topDown = new decimal[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var bottomDown = new decimal[] { 2, 2, 2, 2, 2, 2, 2, 2 };
            var topUp = new decimal[] { 10, 9, 8, 7, 6, 5, 4, 3 };
            var bottomUp = new decimal[] { 0, 0, 1, 0, 0, 0, 0, 0 };

            weights.InitializeFromArrays(topDown, bottomDown, topUp, bottomUp);

            // Validate array getters
            weights.GetTopDownsideWeights().Should().Equal(topDown);
            weights.GetBottomDownsideWeights().Should().Equal(bottomDown);
            weights.GetTopUpsideWeights().Should().Equal(topUp);
            weights.GetBottomUpsideWeights().Should().Equal(bottomUp);

            // Indexer set + get - change a value and read it back
            weights["topupside", 0] = 42m;
            weights["topupside", 0].Should().Be(42m);

            // Unknown weight type returns 0
            weights["not-a-type", 3].Should().Be(0m);
        }

        [TestMethod]
        public void InitializeFromArrays_AllowsNullArrays_DefaultsToEightZeros()
        {
            var weights = new AltarWeights();
            weights.InitializeFromArrays(null, null, null, null);

            weights.GetTopDownsideWeights().Length.Should().Be(8);
            weights.GetBottomDownsideWeights().Length.Should().Be(8);
            weights.GetTopUpsideWeights().Length.Should().Be(8);
            weights.GetBottomUpsideWeights().Length.Should().Be(8);
        }
    }
}
