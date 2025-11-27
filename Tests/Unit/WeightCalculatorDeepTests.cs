using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using ClickIt.Utils;
using ClickIt.Components;
using ExileCore.PoEMemory;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class WeightCalculatorDeepTests
    {
        [TestMethod]
        public void CalculateAltarWeights_Throws_WhenTopOrBottomElementNull()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            // TopMods.Element == null should cause an ArgumentException
            var top = new SecondaryAltarComponent(null, new List<string>(), new List<string>());
            var bottom = new SecondaryAltarComponent((Element)RuntimeHelpers.GetUninitializedObject(typeof(Element)), new List<string>(), new List<string>());
            var primary = new PrimaryAltarComponent(ClickIt.AltarType.Unknown, top, new AltarButton(null), bottom, new AltarButton(null));

            Action act = () => calc.CalculateAltarWeights(primary);
            act.Should().Throw<ArgumentException>();

            // Bottom.Element == null
            var top2 = new SecondaryAltarComponent((Element)RuntimeHelpers.GetUninitializedObject(typeof(Element)), new List<string>(), new List<string>());
            var bottom2 = new SecondaryAltarComponent(null, new List<string>(), new List<string>());
            var primary2 = new PrimaryAltarComponent(ClickIt.AltarType.Unknown, top2, new AltarButton(null), bottom2, new AltarButton(null));

            Action act2 = () => calc.CalculateAltarWeights(primary2);
            act2.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void CalculateAltarWeights_ComputesRatiosAndSums_Correctly()
        {
            var settings = new ClickItSettings();
            // set tiers for mods used in this test
            settings.ModTiers["modTop1"] = 3;
            settings.ModTiers["modTop2"] = 2;
            settings.ModTiers["modBottom1"] = 4;

            var calc = new WeightCalculator(settings);

            var elTop = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var elBottom = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            // Use first 3 positions for testing: top upsides/modTop1 and modTop2, bottom downside modBottom1
            var topMods = new SecondaryAltarComponent(elTop, new List<string> { "modTop1", "modTop2" }, new List<string> { "modTopD1" });
            var bottomMods = new SecondaryAltarComponent(elBottom, new List<string> { "modBottomU1" }, new List<string> { "modBottom1" });

            var primary = new PrimaryAltarComponent(ClickIt.AltarType.Unknown, topMods, new AltarButton(null), bottomMods, new AltarButton(null));

            var weights = calc.CalculateAltarWeights(primary);

            // TopUpsideWeight should be sum of modTop1 + modTop2 => 3 + 2 = 5
            weights.TopUpsideWeight.Should().Be(5m);

            // TopDownsideWeight is modTopD1 (not configured -> default 1)
            weights.TopDownsideWeight.Should().Be(1m);

            // TopWeight = round(5 / 1, 2) -> 5.00
            weights.TopWeight.Should().Be(5.00m);

            // BottomDownsideWeight = modBottom1 => 4
            weights.BottomDownsideWeight.Should().Be(4m);

            // BottomUpsideWeight was provided as unknown -> default mod tier 1
            weights.BottomUpsideWeight.Should().Be(1m);

            // BottomWeight = 1 / 4 = 0.25
            weights.BottomWeight.Should().Be(0.25m);
        }
    }
}
