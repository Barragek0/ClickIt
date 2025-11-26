using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt;
using ClickIt.Components;
using System.Collections.Generic;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class WeightCalculatorExtraTests
    {
        [TestMethod]
        public void GetModWeightFromString_UsesCompositeKeyWhenPresent_AndReturnsOneWhenMissing()
        {
            var settings = new ClickItSettings();
            // composite present
            settings.ModTiers["Player|upA"] = 99;
            var calc = new WeightCalculator(settings);

            // Upside with composite key should return composite value
            var ups = new List<string> { "Player|upA" };
            calc.CalculateUpsideWeight(ups).Should().Be(99m);

            // Now ensure composite missing falls back to 1 (no id-only fallback)
            var settings2 = new ClickItSettings();
            settings2.ModTiers["upA"] = 20; // id-only exists but composite lookup should not use this
            var calc2 = new WeightCalculator(settings2);
            calc2.CalculateUpsideWeight(new List<string> { "Player|upA" }).Should().Be(1m);
        }

        [TestMethod]
        public void CalculateWeightFromList_IgnoresNullOrWhitespaceEntries()
        {
            var settings = new ClickItSettings();
            settings.ModTiers["a"] = 5;
            var calc = new WeightCalculator(settings);

            var list = new List<string> { "a", "", "   ", null };
            // Upside should sum only the 'a' entry
            calc.CalculateUpsideWeight(list).Should().Be(5m);
            // Downside should be 1 + sum
            calc.CalculateDownsideWeight(list).Should().Be(1 + 5m);
        }

        [TestMethod]
        public void Private_GetModString_HandlesBoundsAndNullViaReflection()
        {
            var sec = new SecondaryAltarComponent(null, new List<string> { "up0", "up1" }, new List<string> { "down0", "down1" }, false);

            // private static string GetModString(SecondaryAltarComponent component, int index, bool isDownside)
            var m = typeof(WeightCalculator).GetMethod("GetModString", BindingFlags.NonPublic | BindingFlags.Static);
            m.Should().NotBeNull();

            // upside index in range
            var valUp = (string)m.Invoke(null, [sec, 1, false]);
            valUp.Should().Be("up1");

            // downside index in range
            var valDown = (string)m.Invoke(null, [sec, 0, true]);
            valDown.Should().Be("down0");

            // out of range should return empty
            var outVal = (string)m.Invoke(null, [sec, 10, false]);
            outVal.Should().Be("");

            // null component returns empty
            var nullVal = (string)m.Invoke(null, [null, 0, true]);
            nullVal.Should().Be("");
        }
    }
}
