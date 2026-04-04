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