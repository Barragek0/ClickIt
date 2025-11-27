using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class WeightCalculatorEdgeCaseTests
    {
        [TestMethod]
        public void AltarWeights_Indexer_SetAndGet_Works()
        {
            var w = new AltarWeights();
            w[WeightTypeConstants.TopUpside, 2] = 42m;
            w[WeightTypeConstants.TopUpside, 2].Should().Be(42m);
        }

        [TestMethod]
        public void AltarWeights_InitializeFromArrays_PersistsValues()
        {
            var w = new AltarWeights();
            decimal[] topDown = new decimal[8];
            decimal[] bottomDown = new decimal[8];
            decimal[] topUp = new decimal[8];
            decimal[] bottomUp = new decimal[8];
            topUp[0] = 7m;

            w.InitializeFromArrays(topDown, bottomDown, topUp, bottomUp);
            w.GetTopUpsideWeights()[0].Should().Be(7m);
        }

        [TestMethod]
        public void CalculateUpsideWeight_EmptyList_ReturnsZero()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            calc.CalculateUpsideWeight(new System.Collections.Generic.List<string>()).Should().Be(0m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_EmptyList_ReturnsOne()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            calc.CalculateDownsideWeight(new System.Collections.Generic.List<string>()).Should().Be(1m);
        }

        [TestMethod]
        public void CalculateWeightFromList_SkipsEmptyStrings()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            var list = new System.Collections.Generic.List<string> { "", "" };
            calc.CalculateUpsideWeight(list).Should().Be(0m);
        }
    }
}
