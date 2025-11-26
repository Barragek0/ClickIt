using ClickIt.Constants;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItSettingsDefaultsTests
    {
        [TestMethod]
        public void EnsureAllModsHaveWeights_Populates_ModTiers_For_All_ModDefinitions()
        {
            var settings = new ClickItSettings();
            settings.ModTiers.Clear();
            settings.ModAlerts.Clear();

            settings.EnsureAllModsHaveWeights();

            // All downside mods should be present in ModTiers
            foreach (var (id, _, type, _) in AltarModsConstants.DownsideMods)
            {
                var composite = type + "|" + id;
                settings.ModTiers.Should().ContainKey(composite);
                // downside mods do not have ModAlerts entries by default (alerts only for upside mods)
            }

            // All upside mods should be present in ModTiers and also have ModAlerts entries
            foreach (var (id, _, type, _) in AltarModsConstants.UpsideMods)
            {
                var composite = type + "|" + id;
                settings.ModTiers.Should().ContainKey(composite);
                settings.ModAlerts.Should().ContainKey(composite);
            }
        }

        [TestMethod]
        public void GetModTier_ByCompositeKey_Returns_DefaultValue_WhenInitialized()
        {
            var settings = new ClickItSettings();
            settings.EnsureAllModsHaveWeights();

            // pick first upside / downside and verify values match defaults
            var firstDown = AltarModsConstants.DownsideMods[0];
            settings.GetModTier(firstDown.Id, firstDown.Type).Should().Be(firstDown.DefaultValue);

            var firstUp = AltarModsConstants.UpsideMods[0];
            settings.GetModTier(firstUp.Id, firstUp.Type).Should().Be(firstUp.DefaultValue);
        }

        [TestMethod]
        public void GetModTier_IdOnly_FallsBack_ToOne_WhenMissing()
        {
            var settings = new ClickItSettings();
            settings.ModTiers.Clear();

            settings.GetModTier("some-unknown-mod").Should().Be(1);
        }

        [TestMethod]
        public void GetModAlert_ReturnsCompositeAlert_AndFallsBack_To_IdOnly()
        {
            var settings = new ClickItSettings();
            settings.EnsureAllModsHaveWeights();

            var first = AltarModsConstants.UpsideMods[0];
            string composite = first.Type + "|" + first.Id;

            // composite key exists
            settings.ModAlerts.ContainsKey(composite).Should().BeTrue();
            settings.GetModAlert(first.Id, first.Type).Should().Be(settings.ModAlerts[composite]);

            // id-only fallback
            string arbitraryId = "__test_id_only__";
            settings.ModAlerts[arbitraryId] = true;
            settings.GetModAlert(arbitraryId, "SomeRandomType").Should().BeTrue();
        }
    }
}
