using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class WeightCalculatorAltarTests
    {
        [TestMethod]
        public void CalculateAltarWeights_NullAltar_ThrowsArgumentException()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            Assert.ThrowsException<System.ArgumentException>(() => calc.CalculateAltarWeights(null!));
        }

        [TestMethod]
        public void CalculateAltarWeights_MissingComponents_ThrowsArgumentException()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            var altar = new PrimaryAltarComponent(AltarType.Unknown, null!, new AltarButton(null), null!, new AltarButton(null));
            Assert.ThrowsException<System.ArgumentException>(() => calc.CalculateAltarWeights(altar));
        }

        [TestMethod]
        public void CalculateAltarWeights_NullElements_ThrowsArgumentException()
        {
            var settings = new ClickItSettings();
            var calc = new WeightCalculator(settings);

            var top = new SecondaryAltarComponent(null, ["up0"], ["down0"]);
            var bottom = new SecondaryAltarComponent(null, ["up1"], ["down1"]);

            var altar = new PrimaryAltarComponent(AltarType.Unknown, top, new AltarButton(null), bottom, new AltarButton(null));

            Assert.ThrowsException<System.ArgumentException>(() => calc.CalculateAltarWeights(altar));
        }

        [TestMethod]
        public void CalculateAltarWeights_ValidComponents_ComputesExpectedTotalsAndRatios()
        {
            var settings = new ClickItSettings();

            settings.ModTiers["up0"] = 2;
            settings.ModTiers["up1"] = 3;
            settings.ModTiers["down0"] = 4;
            settings.ModTiers["down1"] = 2;

            var calc = new WeightCalculator(settings);

            var elt = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.PoEMemory.Element)) as ExileCore.PoEMemory.Element;

            var top = new SecondaryAltarComponent(elt, ["up0", "up1"], ["down0"]);
            var bottom = new SecondaryAltarComponent(elt, ["up1"], ["down1"]);

            var altar = new PrimaryAltarComponent(AltarType.Unknown, top, new AltarButton(null), bottom, new AltarButton(null));

            var weights = calc.CalculateAltarWeights(altar);

            weights.TopUpsideWeight.Should().Be(5m);
            weights.TopDownsideWeight.Should().Be(4m);
            weights.BottomUpsideWeight.Should().Be(3m);
            weights.BottomDownsideWeight.Should().Be(2m);

            weights.TopWeight.Should().Be(125);
            weights.BottomWeight.Should().Be(150);
        }
    }
}
