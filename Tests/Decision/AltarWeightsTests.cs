using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class AltarWeightsTests
    {
        [TestMethod]
        public void Indexer_GetSet_WorksForTopUpside()
        {
            var weights = new AltarWeights();
            weights[WeightTypeConstants.TopUpside, 2] = 3.5m;
            weights[WeightTypeConstants.TopUpside, 2].Should().Be(3.5m);
        }

        [TestMethod]
        public void InitializeFromArrays_PreservesValues()
        {
            var topDown = new decimal[8];
            topDown[0] = 1m;
            var bottomDown = new decimal[8];
            bottomDown[1] = 2m;
            var topUp = new decimal[8];
            topUp[2] = 3m;
            var bottomUp = new decimal[8];
            bottomUp[3] = 4m;

            var weights = new AltarWeights();
            weights.InitializeFromArrays(topDown, bottomDown, topUp, bottomUp);

            weights.GetTopDownsideWeights()[0].Should().Be(1m);
            weights.GetBottomDownsideWeights()[1].Should().Be(2m);
            weights.GetTopUpsideWeights()[2].Should().Be(3m);
            weights.GetBottomUpsideWeights()[3].Should().Be(4m);
        }
    }
}
