using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt;
using ClickIt.Components;
using ExileCore.PoEMemory;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class WeightCalculatorZeroAndNegativeTests
    {
        [TestMethod]
        public void CalculateUpsideWeight_HandlesZeroAndNegative_Tiers()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["zero_mod"] = 0;
            settings.ModTiers["neg_mod"] = -2;

            var calc = new WeightCalculator(settings);
            var list = new List<string> { "zero_mod", "neg_mod" };

            // sum should be 0 + (-2) = -2
            calc.CalculateUpsideWeight(list).Should().Be(-2m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_HandlesZeroAndNegative_Tiers()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["zero_mod"] = 0;
            settings.ModTiers["neg_mod"] = -1;

            var calc = new WeightCalculator(settings);
            var list = new List<string> { "zero_mod", "neg_mod" };

            // downside returns 1 + sum => 1 + (0 + -1) = 0
            calc.CalculateDownsideWeight(list).Should().Be(0m);
        }

        [TestMethod]
        public void CalculateAltarWeights_SafeDivision_WhenAllModStringsEmpty_DownsideZero_ProducesZeroWeights()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            // prepare secondary components where every mod string is empty (-> weight 0)
            var elTop = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var elBottom = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topMods = new SecondaryAltarComponent(elTop, new System.Collections.Generic.List<string>(), new System.Collections.Generic.List<string>());
            var bottomMods = new SecondaryAltarComponent(elBottom, new System.Collections.Generic.List<string>(), new System.Collections.Generic.List<string>());

            var primary = new PrimaryAltarComponent(ClickIt.AltarType.Unknown, topMods, new AltarButton(null), bottomMods, new AltarButton(null));

            var weights = calc.CalculateAltarWeights(primary);

            weights.TopDownsideWeight.Should().Be(0m);
            weights.TopUpsideWeight.Should().Be(0m);
            weights.BottomDownsideWeight.Should().Be(0m);
            weights.BottomUpsideWeight.Should().Be(0m);
            weights.TopWeight.Should().Be(0m);
            weights.BottomWeight.Should().Be(0m);
        }
    }
}
