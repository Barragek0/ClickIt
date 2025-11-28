using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceSpecialPathTests
    {
        private static Services.LabelFilterService CreateService()
        {
            var settings = new ClickItSettings();
            var ess = new Services.EssenceService(settings);
            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });
            var svc = new Services.LabelFilterService(settings, ess, err, null);

            // Override key state provider to avoid native key queries
            var ksField = typeof(Services.LabelFilterService).GetField("KeyStateProvider", BindingFlags.NonPublic | BindingFlags.Static);
            ksField.SetValue(null, new System.Func<System.Windows.Forms.Keys, bool>((k) => false));

            // Disable lazy mode checks for deterministic behaviour
            // LazyModeRestrictedChecker is a static property now - assign directly to avoid reflection fragility
            global::ClickIt.Services.LabelFilterService.LazyModeRestrictedChecker = (svc2, labels) => false;

            return svc;
        }

        private static object CreateClickSettings(Services.LabelFilterService svc)
        {
            var method = typeof(Services.LabelFilterService).GetMethod("CreateClickSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            return method.Invoke(svc, [null])!;
        }

        [TestMethod]
        public void ShouldClickSpecialPath_NearestHarvest_ReturnsTrue_ForHarvestPaths()
        {
            var svc = CreateService();
            var cs = CreateClickSettings(svc);

            var csType = cs.GetType();
            csType.GetProperty("NearestHarvest").SetValue(cs, true);

            var shouldMethod = typeof(Services.LabelFilterService).GetMethod("ShouldClickSpecialPath", BindingFlags.NonPublic | BindingFlags.Static);
            bool res = (bool)shouldMethod.Invoke(null, [cs, "something/Harvest/Irrigator/abc", null])!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickSpecialPath_Sanctum_ReturnsTrue_WhenEnabled()
        {
            var svc = CreateService();
            var cs = CreateClickSettings(svc);
            var csType = cs.GetType();
            csType.GetProperty("ClickSanctum").SetValue(cs, true);

            var shouldMethod = typeof(Services.LabelFilterService).GetMethod("ShouldClickSpecialPath", BindingFlags.NonPublic | BindingFlags.Static);
            bool res = (bool)shouldMethod.Invoke(null, [cs, "blabla/Sanctum/thing", null])!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickSpecialPath_Sulphite_ReturnsTrue_WhenEnabled()
        {
            var svc = CreateService();
            var cs = CreateClickSettings(svc);
            var csType = cs.GetType();
            csType.GetProperty("ClickSulphite").SetValue(cs, true);

            var shouldMethod = typeof(Services.LabelFilterService).GetMethod("ShouldClickSpecialPath", BindingFlags.NonPublic | BindingFlags.Static);
            bool res = (bool)shouldMethod.Invoke(null, [cs, "DelveMineral/abc", null])!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickSpecialPath_ReturnsFalse_WhenNoMatchingFlags()
        {
            var svc = CreateService();
            var cs = CreateClickSettings(svc);

            var shouldMethod = typeof(Services.LabelFilterService).GetMethod("ShouldClickSpecialPath", BindingFlags.NonPublic | BindingFlags.Static);
            bool res = (bool)shouldMethod.Invoke(null, [cs, "completely/unrelated/path", null])!;
            res.Should().BeFalse();
        }
    }
}
