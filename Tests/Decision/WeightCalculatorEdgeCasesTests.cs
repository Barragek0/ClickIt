// Disabled: duplicated edge-case tests consolidated into `WeightCalculatorTests.cs`
#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using ClickIt.Utils;
using ClickIt.Components;
using System.Collections.Generic;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class WeightCalculatorEdgeCasesTests
    {
        [TestMethod]
        public void CalculateAltarWeights_NullPrimary_ThrowsArgumentException()
        {
            var settings = new ClickIt.ClickItSettings();
            var calc = new WeightCalculator(settings);
            Action act = () => calc.CalculateAltarWeights(null);
            act.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void CalculateAltarWeights_MissingElements_ThrowsArgumentException()
        {
            var settings = new ClickIt.ClickItSettings();
            var top = new SecondaryAltarComponent(new List<string> { "a" }, new List<string> { "b" });
            var bottom = new SecondaryAltarComponent(new List<string> { "c" }, new List<string> { "d" });
            // make top element null to simulate invalid element
            top.Element = null;
            var primary = new PrimaryAltarComponent(top, bottom);
            var calc = new WeightCalculator(settings);
            Action act = () => calc.CalculateAltarWeights(primary);
            act.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void AltarWeights_UnknownType_IndexerReturnsZero()
        {
            var w = new AltarWeights();
            w["unknown_type", 0] = 5.5m; // setter silently ignores unknown
            w["unknown_type", 0].Should().Be(0);
        }
    }
}
#endif
