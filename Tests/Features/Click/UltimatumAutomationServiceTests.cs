namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumAutomationServiceTests
    {
        [TestMethod]
        public void TryGetOptionPreview_ReturnsFalse_WhenPanelIsMissing_AndCachedLabelsAreEmpty()
        {
            UltimatumAutomationService service = CreateService(
                useNullGameController: true,
                cachedLabels: new TimeCache<List<LabelOnGround>>(() => [], 50));

            bool result = service.TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeFalse();
            previews.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetOptionPreview_ReturnsFalse_WhenCachedLabelsContainOnlyNullEntries()
        {
            UltimatumAutomationService service = CreateService(
            useNullGameController: true,
            cachedLabels: new TimeCache<List<LabelOnGround>>(() => [null!], 50));

            bool result = service.TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeFalse();
            previews.Should().BeEmpty();
        }

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

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenLabelIsNull()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            UltimatumAutomationService service = CreateService(publishUltimatumDebug: debugEvents.Add);

            bool result = service.TryClickPreferredModifier(null!, Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialLabelNull");
            debugEvents[0].Notes.Should().Be("Label was null");
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenInitialUltimatumClickIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.ClickInitialUltimatum.Value = false;
            UltimatumAutomationService service = CreateService(settings: settings);

            bool result = service.TryClickPreferredModifier(null!, Vector2.Zero);

            result.Should().BeFalse();
        }

        private static UltimatumAutomationService CreateService(
            ClickItSettings? settings = null,
            GameController? gameController = null,
            TimeCache<List<LabelOnGround>>? cachedLabels = null,
            bool useNullGameController = false,
            Action<UltimatumDebugEvent>? publishUltimatumDebug = null)
        {
            settings ??= new ClickItSettings();
            if (!useNullGameController)
                gameController ??= (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            cachedLabels ??= new TimeCache<List<LabelOnGround>>(() => [], 50);
            publishUltimatumDebug ??= static _ => { };

            return new UltimatumAutomationService(new UltimatumAutomationServiceDependencies(
                settings,
                gameController!,
                cachedLabels,
                _ => true,
                (_, _) => true,
                _ => { },
                (_, _) => { },
                () => { },
                () => false,
                publishUltimatumDebug));
        }
    }
}