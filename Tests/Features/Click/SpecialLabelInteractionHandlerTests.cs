namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class SpecialLabelInteractionHandlerTests
    {
        [TestMethod]
        public void TryHandle_ReturnsFalse_WhenLabelHasNoSpecialState_AndUltimatumIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.ClickInitialUltimatum.Value = false;
            var handler = CreateHandler(settings, altarSnapshot: []);

            bool handled = handler.TryHandle(null!, Vector2.Zero);

            handled.Should().BeFalse();
        }

        [TestMethod]
        public void TryHandle_ReturnsFalse_WhenLabelIsNotUltimatum_AndNoOtherSpecialHandlingApplies()
        {
            var settings = new ClickItSettings();
            settings.ClickInitialUltimatum.Value = true;
            var handler = CreateHandler(settings, altarSnapshot: []);

            bool handled = handler.TryHandle(null!, Vector2.Zero);

            handled.Should().BeFalse();
        }

        [TestMethod]
        public void TryHandle_ReturnsTrue_ForAltarLabel_WhenAltarChoicesAreNotClickableYet()
        {
            // Regression: a hovered/selected altar label whose choices are not clickable yet must consume the tick (return true) so the generic label click never fires - a blind click lands on an arbitrary altar option (the wrong-option pick bug).
            var settings = new ClickItSettings();
            settings.ClickInitialUltimatum.Value = false;
            Entity altar = EntityProbeFactory.Create(path: Constants.TangleAltar);
            LabelOnGround altarLabel = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(altar);
            var handler = CreateHandler(settings, altarSnapshot: []);

            bool handled = handler.TryHandle(altarLabel, Vector2.Zero);

            handled.Should().BeTrue();
        }

        private static SpecialLabelInteractionHandler CreateHandler(
            ClickItSettings settings,
            IReadOnlyList<PrimaryAltarComponent> altarSnapshot)
        {
            return new SpecialLabelInteractionHandler(new SpecialLabelInteractionHandlerDependencies(
                Settings: settings,
                AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings, altarSnapshot),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(
                    labelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort()),
                UltimatumAutomation: ClickTestServiceFactory.CreateUltimatumAutomationService(settings),
                DebugLog: static _ => { }));
        }
    }
}