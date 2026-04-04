namespace ClickIt.Tests.Features.Labels.Selection
{
    [TestClass]
    public class ClickSettingsFactoryTests
    {
        [TestMethod]
        public void Create_UsesSettingsValues_WhenNoLazyRestrictionApplied()
        {
            var settings = new ClickItSettings();
            var snapshotProvider = new MechanicPrioritySnapshotService();

            var factory = new ClickSettingsFactory(
                settings,
                snapshotProvider,
                _ => false,
                _ => false);

            ClickSettings result = factory.Create(null);

            result.ClickDistance.Should().Be(settings.ClickDistance.Value);
            result.ClickLeagueChests.Should().Be(settings.ClickLeagueChests.Value);
            result.ClickSettlersOre.Should().Be(settings.ClickSettlersOre.Value);
            result.ClickStrongboxes.Should().Be(settings.ClickStrongboxes.Value);
            result.ClickLabyrinthTrials.Should().Be(settings.ClickLabyrinthTrials.Value);
        }

        [TestMethod]
        public void Create_DisablesLeagueChestsAndSettlers_WhenLazyRestrictionIsActive()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.ClickLeagueChests.Value = true;
            settings.ClickSettlersOre.Value = true;

            var factory = new ClickSettingsFactory(
                settings,
                new MechanicPrioritySnapshotService(),
                _ => true,
                _ => false);

            ClickSettings result = factory.Create(new List<LabelOnGround>());

            result.ClickLeagueChests.Should().BeFalse();
            result.ClickSettlersOre.Should().BeFalse();
        }

        [TestMethod]
        public void Create_KeepsSettlersEnabled_WhenHotkeyHeld_DuringLazyRestriction()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.ClickSettlersOre.Value = true;

            var factory = new ClickSettingsFactory(
                settings,
                new MechanicPrioritySnapshotService(),
                _ => true,
                _ => true);

            ClickSettings result = factory.Create(null);

            result.ClickSettlersOre.Should().BeTrue();
        }

        [TestMethod]
        public void BuildEnabledLeagueChestSpecificIds_ReflectsSpecificSettings()
        {
            var settings = new ClickItSettings();
            settings.ClickLeagueChests.Value = true;
            settings.ClickMirageGoldenDjinnCache.Value = true;
            settings.ClickMirageSilverDjinnCache.Value = false;
            settings.ClickHeistSecureLocker.Value = true;

            IReadOnlySet<string> enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, leagueChestsEnabled: true);

            enabled.Should().Contain(MechanicIds.MirageGoldenDjinnCache);
            enabled.Should().Contain(MechanicIds.HeistSecureLocker);
            enabled.Should().NotContain(MechanicIds.MirageSilverDjinnCache);
        }
    }
}