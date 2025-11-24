using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Linq;
using ClickIt.Tests;
using ClickIt.Constants;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class DecisionTests
    {
        private static readonly Random Rng = new Random(0);

        [TestMethod]
        public void Decision_ShouldBeDeterministic_ForSameInput()
        {
            var settings = new MockClickItSettings();
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
            var settings = new MockClickItSettings();
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

        [TestMethod]
        public void RandomizedAltars_ShouldNotThrow_AndBeDeterministic()
        {
            var settings = new MockClickItSettings();
            var calc = new MockWeightCalculator(settings);

            for (int i = 0; i < 300; i++)
            {
                var altar = GenerateRandomAltar();

                // Act
                var w1 = calc.CalculateAltarWeights(altar);
                var w2 = calc.CalculateAltarWeights(altar);

                // Assert: deterministic for same input and non-negative weights
                w1.TopWeight.Should().Be(w2.TopWeight);
                w1.BottomWeight.Should().Be(w2.BottomWeight);
                w1.TopWeight.Should().BeGreaterOrEqualTo(0);
                w1.BottomWeight.Should().BeGreaterOrEqualTo(0);
            }
        }

        private static MockAltarComponent GenerateRandomAltar()
        {
            var allUpside = AltarModsConstants.UpsideMods.Select(m => m.Id).ToArray();
            var allDownside = AltarModsConstants.DownsideMods.Select(m => m.Id).ToArray();

            string Pick(string[] pool) => pool[Rng.Next(pool.Length)];

            var topUps = Enumerable.Range(0, Rng.Next(1, 4)).Select(_ => Pick(allUpside)).Distinct().ToList();
            var topDowns = Enumerable.Range(0, Rng.Next(0, 3)).Select(_ => Pick(allDownside)).Distinct().ToList();
            var bottomUps = Enumerable.Range(0, Rng.Next(1, 4)).Select(_ => Pick(allUpside)).Distinct().ToList();
            var bottomDowns = Enumerable.Range(0, Rng.Next(0, 3)).Select(_ => Pick(allDownside)).Distinct().ToList();

            return new MockAltarComponent
            {
                TopMods = new MockSecondaryAltarComponent { Upsides = topUps, Downsides = topDowns },
                BottomMods = new MockSecondaryAltarComponent { Upsides = bottomUps, Downsides = bottomDowns }
            };
        }
    }
}
