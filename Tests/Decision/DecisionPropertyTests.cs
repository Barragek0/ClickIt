#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Linq;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class DecisionPropertyTests
    {
        [TestMethod]
        public void Decision_ShouldBeDeterministic_ForSameInput()
        {
            var settings = new Tests.MockClickItSettings();
            var calc = new MockWeightCalculator(settings);

            var altar = TestFactories.CreateComplexTestAltarComponent();

            var w1 = calc.CalculateAltarWeights(altar);
            var w2 = calc.CalculateAltarWeights(altar);

            w1.TopWeight.Should().Be(w2.TopWeight);
            w1.BottomWeight.Should().Be(w2.BottomWeight);
        }

        [TestMethod]
        public void IncreasingTopModWeight_ShouldNotDecreaseTopWeight()
        {
            var settings = new Tests.MockClickItSettings();
            var calc = new MockWeightCalculator(settings);

            // Create an altar with a known top mod
            var altar = TestFactories.CreateTestAltarComponent();
            var topMod = altar.TopMods.Upsides.First();

            // Baseline
            settings.ModTiers[topMod] = 10;
            var baseline = calc.CalculateAltarWeights(altar).TopWeight;

            // Increase the weight for that mod
            settings.ModTiers[topMod] = 90;
            var increased = calc.CalculateAltarWeights(altar).TopWeight;

            increased.Should().BeGreaterOrEqualTo(baseline, "increasing a top mod's weight should not reduce the computed top weight");
        }
    }
}
#endif
