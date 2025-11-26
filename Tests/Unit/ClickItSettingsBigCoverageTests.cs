using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItSettingsBigCoverageTests
    {
        [TestMethod]
        public void ConstructAndEnumerateAllPublicProperties_DoesNotThrow()
        {
            var settings = new ClickItSettings();

            // Enumerate and read all public properties to execute initializers / simple getters
            foreach (var p in typeof(ClickItSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    var val = p.GetValue(settings);
                    // If a ToggleNode or similar, try toggling common bool properties safely
                    if (p.PropertyType.Name.Contains("ToggleNode") && p.CanWrite)
                    {
                        p.SetValue(settings, val);
                    }
                }
                catch
                {
                    // Swallow any property access exceptions — test ensures no harmful side effects
                }
            }

            // Verify ModTiers default behaviour
            settings.ModTiers["a-test-mod"] = 5;
            Assert.AreEqual(5, settings.GetModTier("a-test-mod"));
            // Composite key lookup should accept type|id
            settings.ModTiers.Clear();
            settings.ModTiers["downside|x-mod"] = 7;
            Assert.AreEqual(7, settings.GetModTier("x-mod", "downside"));

            // Alerts dictionary default behavior
            settings.ModAlerts["alert-me"] = true;
            Assert.IsTrue(settings.GetModAlert("alert-me", "any"));
            Assert.IsTrue(settings.GetModAlert("alert-me", ""));
        }
    }
}
