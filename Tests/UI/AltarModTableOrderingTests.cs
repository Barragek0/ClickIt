namespace ClickIt.Tests.UI;

[TestClass]
public class AltarModTableOrderingTests
{
    [TestMethod]
    public void UpsideRowsAreGroupedMinionsThenBossesThenPlayerBonuses()
    {
        (string id, string name, string type, int defaultWeight)[] mods =
        [
            ("boss-a", "Boss A", ClickItSettings.AltarTypeBoss, 50),
            ("player-a", "Player A", ClickItSettings.AltarTypePlayer, 40),
            ("minion-a", "Minion A", ClickItSettings.AltarTypeMinion, 40),
            ("boss-b", "Boss B", ClickItSettings.AltarTypeBoss, 20),
            ("minion-b", "Minion B", ClickItSettings.AltarTypeMinion, 20),
        ];

        string[] sections = SettingsUiRenderHelpers.OrderAltarModRows(mods, isUpside: true)
            .Select(static mod => SettingsUiRenderHelpers.GetAltarUpsideSectionStyle(mod.type).HeaderText)
            .ToArray();
        string[] sectionOrder = sections
            .Where((section, index) => index == 0 || section != sections[index - 1])
            .ToArray();

        sectionOrder.Should().Equal("Minion Drops", "Boss Drops", "Player Bonuses");
    }

    [TestMethod]
    public void DownsideRowsKeepEachDangerSectionContiguous()
    {
        (string id, string name, string type, int defaultWeight)[] mods =
        [
            ("player-ok", "Player OK", ClickItSettings.AltarTypePlayer, 35),
            ("minion-danger", "Minion Dangerous", ClickItSettings.AltarTypeMinion, 50),
            ("boss-brick", "Boss Bricking", ClickItSettings.AltarTypeBoss, 100),
            ("boss-very", "Boss Very Dangerous", ClickItSettings.AltarTypeBoss, 75),
            ("minion-ok", "Minion OK", ClickItSettings.AltarTypeMinion, 20),
        ];

        string[] sections = SettingsUiRenderHelpers.OrderAltarModRows(mods, isUpside: false)
            .Select(static mod => SettingsUiRenderHelpers.GetAltarDownsideSectionStyle(mod.defaultWeight).HeaderText)
            .ToArray();
        string[] sectionOrder = sections
            .Where((section, index) => index == 0 || section != sections[index - 1])
            .ToArray();

        sectionOrder.Should().Equal("Build Bricking Modifiers", "Very Dangerous Modifiers", "Dangerous Modifiers", "Ok Modifiers");
    }
}
