namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginDelveFlarePolicyTests
    {
        [TestMethod]
        public void ShouldUseFlare_ReturnsTrue_WhenAllThresholdsAreMet()
        {
            PluginDelveFlarePolicy.ShouldUseFlare(
                darknessDebuffCharges: 5,
                darknessDebuffThreshold: 5,
                healthPercent: 75f,
                healthThreshold: 75,
                energyShieldPercent: 75f,
                energyShieldThreshold: 75)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldUseFlare_ReturnsFalse_WhenDarknessDebuffIsBelowThreshold()
        {
            PluginDelveFlarePolicy.ShouldUseFlare(
                darknessDebuffCharges: 4,
                darknessDebuffThreshold: 5,
                healthPercent: 10f,
                healthThreshold: 75,
                energyShieldPercent: 10f,
                energyShieldThreshold: 75)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldUseFlare_ReturnsFalse_WhenHealthIsAboveThreshold()
        {
            PluginDelveFlarePolicy.ShouldUseFlare(
                darknessDebuffCharges: 5,
                darknessDebuffThreshold: 5,
                healthPercent: 76f,
                healthThreshold: 75,
                energyShieldPercent: 10f,
                energyShieldThreshold: 75)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldUseFlare_ReturnsFalse_WhenEnergyShieldIsAboveThreshold()
        {
            PluginDelveFlarePolicy.ShouldUseFlare(
                darknessDebuffCharges: 5,
                darknessDebuffThreshold: 5,
                healthPercent: 10f,
                healthThreshold: 75,
                energyShieldPercent: 76f,
                energyShieldThreshold: 75)
                .Should().BeFalse();
        }
    }
}