using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt.Components;
using System.Collections.Generic;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class WeightCalculatorAltarTests
    {
        [TestMethod]
        public void CalculateAltarWeights_NullAltar_ThrowsArgumentException()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            Assert.ThrowsException<System.ArgumentException>(() => calc.CalculateAltarWeights(null));
        }

        [TestMethod]
        public void CalculateAltarWeights_MissingComponents_ThrowsArgumentException()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            // PrimaryAltarComponent can be constructed with null TopMods/BottomMods
            var altar = new PrimaryAltarComponent(global::ClickIt.ClickIt.AltarType.Unknown, null!, new AltarButton(null), null!, new AltarButton(null));
            Assert.ThrowsException<System.ArgumentException>(() => calc.CalculateAltarWeights(altar));
        }

        [TestMethod]
        public void CalculateAltarWeights_NullElements_ThrowsArgumentException()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            // SecondaryAltarComponent with null Element should trigger the null-element validation
            var top = new SecondaryAltarComponent(null, new List<string> { "up0" }, new List<string> { "down0" });
            var bottom = new SecondaryAltarComponent(null, new List<string> { "up1" }, new List<string> { "down1" });

            var altar = new PrimaryAltarComponent(global::ClickIt.ClickIt.AltarType.Unknown, top, new AltarButton(null), bottom, new AltarButton(null));

            Assert.ThrowsException<System.ArgumentException>(() => calc.CalculateAltarWeights(altar));
        }

        [TestMethod]
        public void CalculateAltarWeights_ValidComponents_ComputesExpectedTotalsAndRatios()
        {
            var settings = new ClickItSettings();

            // Define a handful of distinct mods with explicit tiers
            settings.ModTiers["up0"] = 2;
            settings.ModTiers["up1"] = 3;
            settings.ModTiers["down0"] = 4;
            settings.ModTiers["down1"] = 2;

            var calc = new WeightCalculator(settings);

            // Create a (uninitialized) Element so the component elements are non-null
            var elt = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.PoEMemory.Element)) as ExileCore.PoEMemory.Element;

            var top = new SecondaryAltarComponent(elt, new List<string> { "up0", "up1" }, new List<string> { "down0" });
            var bottom = new SecondaryAltarComponent(elt, new List<string> { "up1" }, new List<string> { "down1" });

            var altar = new PrimaryAltarComponent(global::ClickIt.ClickIt.AltarType.Unknown, top, new AltarButton(null), bottom, new AltarButton(null));

            var weights = calc.CalculateAltarWeights(altar);

            // TopUpsideWeight: up0(2) + up1(3) = 5
            weights.TopUpsideWeight.Should().Be(5m);
            // TopDownsideWeight: down0(4)
            weights.TopDownsideWeight.Should().Be(4m);
            // BottomUpsideWeight: up1(3)
            weights.BottomUpsideWeight.Should().Be(3m);
            // BottomDownsideWeight: down1(2)
            weights.BottomDownsideWeight.Should().Be(2m);

            // TopWeight = round(TopUpside/TopDownside, 2) = round(5 / 4, 2) = 1.25
            weights.TopWeight.Should().Be(1.25m);
            // BottomWeight = round(3 / 2, 2) = 1.5
            weights.BottomWeight.Should().Be(1.5m);
        }
    }
}
