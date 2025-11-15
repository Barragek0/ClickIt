using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class WeightCalculatorAltarWeightsEdgeTests
    {
        [TestMethod]
        public void InitializeFromArrays_NullArguments_UsesDefaultArrays()
        {
            // Arrange
            var aw = new AltarWeights();

            // Act - pass null arrays
            aw.InitializeFromArrays(null, null, null, null);

            // Assert - getters should return arrays of length 8 filled with zeros
            var topUps = aw.GetTopUpsideWeights();
            topUps.Should().HaveCount(8);
            foreach (var v in topUps) v.Should().Be(0m);

            // Indexer should return 0 for valid indexes
            aw["topupside", 0].Should().Be(0m);
            aw["bottomupside", 7].Should().Be(0m);
        }

        [TestMethod]
        public void Indexer_SetGet_And_OutOfRange_Throws()
        {
            // Arrange
            var aw = new AltarWeights();
            aw.InitializeFromArrays(null, null, null, null);

            // Act - set a valid index
            aw["topupside", 2] = 42m;

            // Assert - read back
            aw["topupside", 2].Should().Be(42m);

            // Out of range index should throw
            Action act = () => aw["topupside", 8].ToString();
            act.Should().Throw<IndexOutOfRangeException>();
        }
    }
}
