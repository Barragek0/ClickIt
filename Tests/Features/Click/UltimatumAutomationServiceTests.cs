namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumAutomationServiceTests
    {
        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenOtherUltimatumClickIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = false;
            var gameController = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => [], 50);

            var service = new UltimatumAutomationService(new UltimatumAutomationServiceDependencies(
                settings,
                gameController,
                cachedLabels,
                _ => true,
                (_, _) => true,
                _ => { },
                (_, _) => { },
                () => { },
                () => false,
                _ => { }));

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
        }
    }
}