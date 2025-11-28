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
            // Arrange
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            var upsides = new List<string> { "mod_a", "mod_b" };

            // Act
            var result = calc.CalculateUpsideWeight(upsides);

            // Assert
            // Default GetModTier returns 1 for unknown mods, so sum should be 2
            result.Should().Be(2m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_ReturnsOnePlusSumWhenNullOrEmpty()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);
            // null
            calc.CalculateDownsideWeight((List<string>?)null!).Should().Be(1m);
            // empty
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
            // Set explicit tiers for composite keys used by GetModWeightFromString
            settings.ModTiers["Player|upA"] = 50;
            settings.ModTiers["Boss|downA"] = 25;

            var calc = new WeightCalculator(settings);

            // Build secondary components with one upside and one downside each
            var top = TestBuilders.BuildSecondary(["Player|upA"], ["Boss|downA"]);
            var bottom = TestBuilders.BuildSecondary(["Player|upA"], ["Boss|downA"]);

            // Provide non-null Element instances for both components by creating uninitialized objects
            var elemType = typeof(Element);
            var topElem = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(elemType) as Element;
            var bottomElem = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(elemType) as Element;
            top.Element = topElem;
            bottom.Element = bottomElem;

            var primary = TestBuilders.BuildPrimary(top, bottom);

            var weights = calc.CalculateAltarWeights(primary);

            // Expect sum weights equal configured tiers
            weights.TopUpsideWeight.Should().Be(50m);
            weights.TopDownsideWeight.Should().Be(25m);
            // Ratio rounded to 2 decimals: 50 / 25 = 2.00
            weights.TopWeight.Should().Be(2.00m);
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

        // --- Merged tests from WeightCalculatorParameterizedTests.cs ---
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
            // set id-only tier
            settings.ModTiers["mod_a"] = 5;
            // set composite tier
            settings.ModTiers["Player|special"] = 7;

            var calc = new WeightCalculator(settings);

            var upsides = new List<string> { "mod_a", "Player|special" };
            var result = calc.CalculateUpsideWeight(upsides);

            result.Should().Be(12m);
        }

        [TestMethod]
        public void CalculateDownsideWeight_IncludesConfiguredTiers()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["d1"] = 2; // downside tier

            var calc = new WeightCalculator(settings);
            var downsides = new List<string> { "d1" };

            // downside weight should be 1 + sum(tiers)
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

        // --- Merged tests from WeightCalculatorZeroAndNegativeTests.cs ---
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

            var topMods = new SecondaryAltarComponent(elTop, [], []);
            var bottomMods = new SecondaryAltarComponent(elBottom, [], []);

            var primary = new PrimaryAltarComponent(ClickIt.AltarType.Unknown, topMods, new AltarButton(null), bottomMods, new AltarButton(null));

            var weights = calc.CalculateAltarWeights(primary);

            weights.TopDownsideWeight.Should().Be(0m);
            weights.TopUpsideWeight.Should().Be(0m);
            weights.BottomDownsideWeight.Should().Be(0m);
            weights.BottomUpsideWeight.Should().Be(0m);
            weights.TopWeight.Should().Be(0m);
            weights.BottomWeight.Should().Be(0m);
        }

        // --- Merged tests from WeightCalculatorDeepTests.cs ---
        [TestMethod]
        public void CalculateAltarWeights_Throws_WhenTopOrBottomElementNull()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            // TopMods.Element == null should cause an ArgumentException
            var top = new SecondaryAltarComponent(null, [], []);
            var bottom = new SecondaryAltarComponent((Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element)), [], []);
            var primary = new PrimaryAltarComponent(ClickIt.AltarType.Unknown, top, new AltarButton(null), bottom, new AltarButton(null));

            Action act = () => calc.CalculateAltarWeights(primary);
            act.Should().Throw<ArgumentException>();

            // Bottom.Element == null
            var top2 = new SecondaryAltarComponent((Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element)), [], []);
            var bottom2 = new SecondaryAltarComponent(null, [], []);
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

            var elTop = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var elBottom = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));

            // Use first 3 positions for testing: top upsides/modTop1 and modTop2, bottom downside modBottom1
            var topMods = new SecondaryAltarComponent(elTop, ["modTop1", "modTop2"], ["modTopD1"]);
            var bottomMods = new SecondaryAltarComponent(elBottom, ["modBottomU1"], ["modBottom1"]);

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
