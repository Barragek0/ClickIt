namespace ClickIt.Tests.Features.Skills;

[TestClass]
public sealed class MonsterDistancePreviewOverlayTests
{
    [TestMethod]
    public void ResolveDistance_UsesEachCurrentSetting()
    {
        var settings = new ClickItSettings();
        settings.VaalMoltenShellNormalDistance.Value = 11;
        settings.VaalMoltenShellMagicDistance.Value = 22;
        settings.VaalMoltenShellRareDistance.Value = 33;
        settings.VaalMoltenShellUniqueDistance.Value = 44;
        settings.LazyModeNormalMonsterBlockDistance = 55;
        settings.LazyModeMagicMonsterBlockDistance = 66;
        settings.LazyModeRareMonsterBlockDistance = 77;
        settings.LazyModeUniqueMonsterBlockDistance = 88;
        settings.ClickDistance.Value = 100;
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "VaalNormal").Should().Be(11);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "VaalMagic").Should().Be(22);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "VaalRare").Should().Be(33);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "VaalUnique").Should().Be(44);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "LazyNormal").Should().Be(55);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "LazyMagic").Should().Be(66);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "LazyRare").Should().Be(77);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "LazyUnique").Should().Be(88);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "ClickSearchRadius").Should().Be(100);
        settings.ClickDistance.Value = 147;
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "ClickSearchRadius").Should().Be(147);
        settings.VaalMoltenShellNormalDistance.Value = 17;
        MonsterDistancePreviewOverlay.ResolveDistance(settings, "VaalNormal").Should().Be(17);
        MonsterDistancePreviewOverlay.ResolveDistance(settings, null).Should().Be(0);
    }
}
