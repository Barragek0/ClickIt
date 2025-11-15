using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class WeightCalculatorAltarWeightsTests
    {
        [TestMethod]
        public void AltarWeights_InitializeFromArrays_And_Indexer_Getters()
        {
            // Arrange
            var topDown = new decimal[8];
            var bottomDown = new decimal[8];
            var topUp = new decimal[8];
            var bottomUp = new decimal[8];
            topUp[0] = 7m;
            topUp[1] = 3m;
            bottomUp[0] = 2m;
            bottomDown[0] = 1m;

            var aw = new AltarWeights();

            // Act
            aw.InitializeFromArrays(topDownside: topDown, bottomDownside: bottomDown, topUpside: topUp, bottomUpside: bottomUp);

            // Assert - array getters
            var tops = aw.GetTopUpsideWeights();
            tops[0].Should().Be(7m);
            tops[1].Should().Be(3m);

            // Assert - indexer access using expected weight type keys (lowercase)
            aw["topupside", 0].Should().Be(7m);
            aw["bottomupside", 0].Should().Be(2m);
            aw["topdownside", 0].Should().Be(0m); // we set topDown[0] = 0
        }

        [TestMethod]
        public void CalculateAltarWeights_ZeroDownsides_ProducesZeroRatios()
        {
            // Arrange: set upsides but leave downsides empty so TopWeight/BottomWeight should become 0 (safe-division)
            var calc = ClickIt.Tests.Shared.TestHelpers.CreateWeightCalculator(new System.Collections.Generic.Dictionary<string, int>
            {
                ["u1"] = 10,
                ["u2"] = 5
            });

            var top = new ClickIt.Components.SecondaryAltarComponent(new System.Collections.Generic.List<string> { "u1", "u2" }, new System.Collections.Generic.List<string> { "" });
            var bottom = new ClickIt.Components.SecondaryAltarComponent(new System.Collections.Generic.List<string> { "" }, new System.Collections.Generic.List<string> { "" });
            var primary = new ClickIt.Components.PrimaryAltarComponent(top, bottom);

            // Act
            var weights = calc.CalculateAltarWeights(primary);

            // Assert: since downsides totals are zero, TopWeight and BottomWeight should be 0 (implementation uses safe division)
            weights.TopWeight.Should().Be(0m);
            weights.BottomWeight.Should().Be(0m);
            weights.TopUpsideWeight.Should().Be(15m);
        }
    }
}
