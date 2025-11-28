using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;
using System.Reflection;
using ClickIt.Tests.TestUtils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarServiceUnitTests
    {
        [TestMethod]
        public void DetermineAltarType_MatchesPathsCorrectly()
        {
            // Use reflection to call the private static DetermineAltarType method
            var method = typeof(AltarService).GetMethod("DetermineAltarType", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var searing = (ClickIt.AltarType)method.Invoke(null, ["Some/Path/CleansingFireAltar/Thing"]);
            searing.Should().Be(ClickIt.AltarType.SearingExarch);

            var eater = (ClickIt.AltarType)method.Invoke(null, ["path/to/TangleAltar/item"]);
            eater.Should().Be(ClickIt.AltarType.EaterOfWorlds);

            var unknown = (ClickIt.AltarType)method.Invoke(null, [string.Empty]);
            unknown.Should().Be(ClickIt.AltarType.Unknown);
        }

        [TestMethod]
        public void ProcessAltarScanningLogic_ClearsWhenNoLabels()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            // cachedLabels null to simulate no cached labels available
            var svc = new AltarService(clickIt, settings, null);

            // Add a dummy primary component so ClearAltarComponents would remove it
            var comp = TestBuilders.BuildPrimary();
            svc.AddAltarComponent(comp).Should().BeTrue();
            svc.GetAltarComponents().Should().HaveCountGreaterOrEqualTo(1);

            svc.ProcessAltarScanningLogic();

            svc.GetAltarComponents().Should().BeEmpty();
        }
    }
}
