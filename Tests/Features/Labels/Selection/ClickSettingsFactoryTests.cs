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
            result.ClickHeistDoors.Should().Be(settings.ClickHeistDoors.Value);
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
            settings.ClickHeistSecureRepository.Value = true;
            settings.ClickHeistHazards.Value = true;

            IReadOnlySet<string> enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, leagueChestsEnabled: true);

            enabled.Should().Contain(MechanicIds.MirageGoldenDjinnCache);
            enabled.Should().Contain(MechanicIds.HeistSecureLocker);
            enabled.Should().Contain(MechanicIds.HeistSecureRepository);
            enabled.Should().Contain(MechanicIds.HeistHazards);
            enabled.Should().NotContain(MechanicIds.MirageSilverDjinnCache);
        }

        [TestMethod]
        public void Create_SpecDefaults_DisabledByDefaultMechanicsStayOff_EnabledByDefaultStayOn()
        {
            // Spec 20.3: basic chests, area transitions, labyrinth trials, altar clicks, and ultimatum initial/choices are DISABLED by default; strongboxes, items, essences, shrines, and the ultimatum take-reward button are ENABLED by default.
            var settings = new ClickItSettings();
            var factory = new ClickSettingsFactory(
                settings,
                new MechanicPrioritySnapshotService(),
                _ => false,
                _ => false);

            ClickSettings result = factory.Create(null);

            result.ClickBasicChests.Should().BeFalse("basic chests are disabled by default (spec 14/20)");
            result.ClickAreaTransitions.Should().BeFalse("area transitions are disabled by default (spec 14/20)");
            result.ClickLabyrinthTrials.Should().BeFalse("labyrinth trials are disabled by default (spec 14/20)");
            result.ClickEater.Should().BeFalse("eater altar clicks are disabled by default (spec 22/20)");
            result.ClickExarch.Should().BeFalse("exarch altar clicks are disabled by default (spec 22/20)");
            result.ClickInitialUltimatum.Should().BeFalse("initial ultimatum clicks are disabled by default (spec 23/20)");
            result.ClickOtherUltimatum.Should().BeFalse("ultimatum choice-panel clicks are disabled by default (spec 23/20)");
            result.ClickStrongboxes.Should().BeTrue("strongbox clicks are enabled by default (spec 14)");
            result.ClickItems.Should().BeTrue("ground item pickup is enabled by default (spec 14)");
            result.ClickEssences.Should().BeTrue("essence clicks are enabled by default (spec 14)");

            settings.ClickShrines.Value.Should().BeTrue("shrine clicks are enabled by default (spec 14)");
            settings.ClickUltimatumTakeRewardButton.Value.Should().BeTrue("the ultimatum take-reward button is enabled by default (spec 23)");
        }
    }
}
