using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceSpecialPathAdditionalTests
    {
        private static object CreateClickSettingsStub(bool nearestHarvest = false, bool clickSulphite = false, bool clickDelveSpawners = false,
            bool strongboxes = false, bool clickSanctum = false, bool clickBreach = false, bool clickSettlers = false)
        {
            var type = typeof(Services.LabelFilterService);
            var ksType = type.GetNestedType("ClickSettings", System.Reflection.BindingFlags.NonPublic)!;
            var inst = System.Activator.CreateInstance(ksType)!;

            // set common flags via reflection on the instance
            void Set(string name, object val)
            {
                var f = ksType.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) f.SetValue(inst, val);
            }

            Set("NearestHarvest", nearestHarvest);
            Set("ClickSulphite", clickSulphite);
            Set("ClickDelveSpawners", clickDelveSpawners);
            Set("RegularStrongbox", strongboxes);
            Set("ClickSanctum", clickSanctum);
            Set("ClickBreach", clickBreach);
            Set("ClickSettlersOre", clickSettlers);

            return inst!;
        }

        [TestMethod]
        public void ShouldClickSpecialPath_MatchesVariousPaths_WhenFlagsEnabled()
        {
            var mi = typeof(Services.LabelFilterService).GetMethod("ShouldClickSpecialPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

            var settings1 = CreateClickSettingsStub(nearestHarvest: true);
            bool r1 = (bool)mi.Invoke(null, new object[] { settings1, "Harvest/Irrigator/abc", null })!;
            r1.Should().BeTrue();

            var settings2 = CreateClickSettingsStub(clickSulphite: true);
            bool r2 = (bool)mi.Invoke(null, new object[] { settings2, "DelveMineral/foo", null })!;
            r2.Should().BeTrue();

            var settings3 = CreateClickSettingsStub(clickDelveSpawners: true);
            bool r3 = (bool)mi.Invoke(null, new object[] { settings3, "Delve/Objects/Encounter/bar", null })!;
            r3.Should().BeTrue();

            var settings4 = CreateClickSettingsStub(clickBreach: true);
            bool r4 = (bool)mi.Invoke(null, new object[] { settings4, "Brequel/something", null })!;
            r4.Should().BeTrue();

            var settings5 = CreateClickSettingsStub(clickSanctum: true);
            bool r5 = (bool)mi.Invoke(null, new object[] { settings5, "Sanctum/step", null })!;
            r5.Should().BeTrue();
        }
    }
}
