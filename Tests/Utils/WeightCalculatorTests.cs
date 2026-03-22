using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Components;
using ClickIt.Utils;
using ClickIt.Tests.TestUtils;
using System;
using ExileCore.PoEMemory;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class WeightCalculatorTests
    {
        [TestMethod]
        public void CalculateUpsideWeight_ReturnsSumOfModTiers()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            var upsides = new List<string> { "mod_a", "mod_b" };

            var result = calc.CalculateUpsideWeight(upsides);

            result.Should().Be(2m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_ReturnsOnePlusSumWhenNullOrEmpty()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            calc.CalculateDownsideWeight((List<string>?)null!).Should().Be(1m);
            calc.CalculateDownsideWeight([]).Should().Be(1m);
        }

        [TestMethod]
        public void CalculateAltarWeights_ThrowsWhenElementsNull()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            var primary = TestBuilders.BuildPrimary();
            Action act = () => calc.CalculateAltarWeights(primary);
            act.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void CalculateAltarWeights_ReturnsExpectedWeights()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["Player|upA"] = 50;
            settings.ModTiers["Boss|downA"] = 25;

            var calc = new WeightCalculator(settings);

            var top = TestBuilders.BuildSecondary(["Player|upA"], ["Boss|downA"]);
            var bottom = TestBuilders.BuildSecondary(["Player|upA"], ["Boss|downA"]);

            var elemType = typeof(Element);
            var topElem = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(elemType) as Element;
            var bottomElem = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(elemType) as Element;
            top.Element = topElem;
            bottom.Element = bottomElem;

            var primary = TestBuilders.BuildPrimary(top, bottom);

            var weights = calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(50m);
            weights.TopDownsideWeight.Should().Be(25m);
            weights.TopWeight.Should().Be(200);
        }

        [TestMethod]
        public void GetModWeightFromString_UsesCompositeKeyWhenPresent_AndReturnsOneWhenMissing()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["Player|upA"] = 99;
            var calc = new WeightCalculator(settings);

            var ups = new List<string> { "Player|upA" };
            calc.CalculateUpsideWeight(ups).Should().Be(99m);

            var settings2 = new ClickItSettings();
            settings2.ModTiers["upA"] = 20;
            var calc2 = new WeightCalculator(settings2);
            calc2.CalculateUpsideWeight(["Player|upA"]).Should().Be(1m);
        }

        [TestMethod]
        public void CalculateWeightFromList_IgnoresNullOrWhitespaceEntries()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["a"] = 5;
            var calc = new WeightCalculator(settings);

            var list = new List<string?> { "a", "", "   ", null };
            calc.CalculateUpsideWeight(list!).Should().Be(5m);
            calc.CalculateDownsideWeight(list!).Should().Be(1 + 5m);
        }

        [TestMethod]
        public void Private_GetModString_HandlesBoundsAndNullViaReflection()
        {
            var sec = new SecondaryAltarComponent(null, ["up0", "up1"], ["down0", "down1"], false);

            var m = typeof(WeightCalculator).GetMethod("GetModString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            m.Should().NotBeNull();

            var valUp = (string)(m!.Invoke(null, [sec, 1, false])!);
            valUp.Should().Be("up1");

            var valDown = (string)(m.Invoke(null, [sec, 0, true])!);
            valDown.Should().Be("down0");

            var outVal = (string)(m.Invoke(null, [sec, 10, false])!);
            outVal.Should().Be("");

            var nullVal = (string)(m.Invoke(null, [null!, 0, true])!);
            nullVal.Should().Be("");
        }

        [DataTestMethod]
        [DataRow(null, 0.0d)]
        [DataRow("", 0.0d)]
        public void CalculateUpsideWeight_NullOrEmpty_ReturnsZero(object listObj, double expected)
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            List<string>? list = listObj as List<string>;
            var result = calc.CalculateUpsideWeight(list!);
            ((double)result).Should().Be(expected);
        }

        [TestMethod]
        public void CalculateUpsideWeight_UsesModTiersAndCompositeKeys()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["mod_a"] = 5;
            settings.ModTiers["Player|special"] = 7;

            var calc = new WeightCalculator(settings);

            var upsides = new List<string> { "mod_a", "Player|special" };
            var result = calc.CalculateUpsideWeight(upsides);

            result.Should().Be(12m);
        }

        [TestMethod]
        public void CalculateUpsideWeight_UsesDefaultTier_WhenTypedModHasTrailingSeparator()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["Player|"] = 11;
            var calc = new WeightCalculator(settings);

            var result = calc.CalculateUpsideWeight(["Player|"]);

            result.Should().Be(1m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_IncludesConfiguredTiers()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["d1"] = 2; // downside tier

            var calc = new WeightCalculator(settings);
            var downsides = new List<string> { "d1" };

            calc.CalculateDownsideWeight(downsides).Should().Be(3m);
        }

        // --- Merged tests from WeightCalculatorEdgeCaseTests.cs ---
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

            calc.CalculateUpsideWeight([]).Should().Be(0m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_EmptyList_ReturnsOne()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            calc.CalculateDownsideWeight([]).Should().Be(1m);
        }

        [TestMethod]
        public void CalculateWeightFromList_SkipsEmptyStrings()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            var list = new List<string> { "", "" };
            calc.CalculateUpsideWeight(list).Should().Be(0m);
        }

        [TestMethod]
        public void CalculateUpsideWeight_HandlesZeroAndNegative_Tiers()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["zero_mod"] = 0;
            settings.ModTiers["neg_mod"] = -2;

            var calc = new WeightCalculator(settings);
            var list = new List<string> { "zero_mod", "neg_mod" };

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

            calc.CalculateDownsideWeight(list).Should().Be(0m);
        }

        [TestMethod]
        public void CalculateAltarWeights_SafeDivision_WhenAllModStringsEmpty_DownsideZero_ProducesZeroWeights()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            var elTop = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var elBottom = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topMods = new SecondaryAltarComponent(elTop, [], []);
            var bottomMods = new SecondaryAltarComponent(elBottom, [], []);

            var primary = new PrimaryAltarComponent(AltarType.Unknown, topMods, new AltarButton(null), bottomMods, new AltarButton(null));

            var weights = calc.CalculateAltarWeights(primary);

            weights.TopDownsideWeight.Should().Be(0m);
            weights.TopUpsideWeight.Should().Be(0m);
            weights.BottomDownsideWeight.Should().Be(0m);
            weights.BottomUpsideWeight.Should().Be(0m);
            weights.TopWeight.Should().Be(0);
            weights.BottomWeight.Should().Be(0);
        }

        [TestMethod]
        public void CalculateAltarWeights_Throws_WhenTopOrBottomElementNull()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            var top = new SecondaryAltarComponent(null, [], []);
            var bottom = new SecondaryAltarComponent((Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element)), [], []);
            var primary = new PrimaryAltarComponent(AltarType.Unknown, top, new AltarButton(null), bottom, new AltarButton(null));

            Action act = () => calc.CalculateAltarWeights(primary);
            act.Should().Throw<ArgumentException>();

            var top2 = new SecondaryAltarComponent((Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element)), [], []);
            var bottom2 = new SecondaryAltarComponent(null, [], []);
            var primary2 = new PrimaryAltarComponent(AltarType.Unknown, top2, new AltarButton(null), bottom2, new AltarButton(null));

            Action act2 = () => calc.CalculateAltarWeights(primary2);
            act2.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void CalculateAltarWeights_ComputesRatiosAndSums_Correctly()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["modTop1"] = 3;
            settings.ModTiers["modTop2"] = 2;
            settings.ModTiers["modBottom1"] = 4;

            var calc = new WeightCalculator(settings);

            var elTop = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var elBottom = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topMods = new SecondaryAltarComponent(elTop, ["modTop1", "modTop2"], ["modTopD1"]);
            var bottomMods = new SecondaryAltarComponent(elBottom, ["modBottomU1"], ["modBottom1"]);

            var primary = new PrimaryAltarComponent(AltarType.Unknown, topMods, new AltarButton(null), bottomMods, new AltarButton(null));

            var weights = calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(5m);

            weights.TopDownsideWeight.Should().Be(1m);

            weights.TopWeight.Should().Be(500);

            weights.BottomDownsideWeight.Should().Be(4m);

            weights.BottomUpsideWeight.Should().Be(1m);

            weights.BottomWeight.Should().Be(25);
        }
    }
}
