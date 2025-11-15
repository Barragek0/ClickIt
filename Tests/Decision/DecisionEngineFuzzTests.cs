#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Linq;
using ClickIt.Tests;
using ClickIt.Constants;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class DecisionEngineFuzzTests
    {
        private static readonly Random Rng = new Random(0);

        [TestMethod]
        public void RandomizedAltars_ShouldNotThrow_AndBeDeterministic()
        {
            var settings = new Tests.MockClickItSettings();
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
#endif
