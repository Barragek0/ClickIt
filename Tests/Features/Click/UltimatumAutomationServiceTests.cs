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
            List<UltimatumDebugEvent> debugEvents = [];
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
                debugEvents.Add));

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelSkip");
            debugEvents[0].Notes.Should().Be("Other Ultimatum click setting disabled");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenPanelIsMissing_AndPublishesPanelMissingDebug()
        {
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;
            List<UltimatumDebugEvent> debugEvents = [];
            UltimatumAutomationService service = CreateService(
                settings: settings,
                useNullGameController: true,
                publishUltimatumDebug: debugEvents.Add);

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelMissing");
            debugEvents[0].Notes.Should().Be("Ultimatum panel not visible/available");
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
            ClickItSettings settings = new();
            settings.ClickInitialUltimatum.Value = false;
            List<UltimatumDebugEvent> debugEvents = [];
            UltimatumAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add);

            bool result = service.TryClickPreferredModifier(
                ExileCoreOpaqueFactory.CreateOpaqueLabel(),
                Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialDisabled");
            debugEvents[0].Notes.Should().Be("Initial ultimatum click setting disabled");
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenLabelIsNotUltimatum()
        {
            ClickItSettings settings = new();
            settings.ClickInitialUltimatum.Value = true;
            List<UltimatumDebugEvent> debugEvents = [];
            UltimatumAutomationService service = CreateService(settings: settings, publishUltimatumDebug: debugEvents.Add);

            bool result = service.TryClickPreferredModifier(
                ExileCoreVisibleObjectBuilder.CreateSelectableLabel(),
                Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialNotUltimatum");
            debugEvents[0].Notes.Should().Be("Label path is not ultimatum interactable");
        }

        [TestMethod]
        public void TryGetOptionPreview_ReturnsFalse_WhenCachedLabelsContainOnlyNonUltimatumLabels()
        {
            UltimatumAutomationService service = CreateService(
                useNullGameController: true,
                cachedLabels: new TimeCache<List<LabelOnGround>>(() => [ExileCoreVisibleObjectBuilder.CreateSelectableLabel()], 50));

            bool result = service.TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeFalse();
            previews.Should().BeEmpty();
        }

        [TestMethod]
        public void TryClickBeginButton_ReturnsFalse_WhenLabelIsNull()
        {
            UltimatumAutomationService service = CreateService();
            List<string> logs = [];
            SetDebugLog(service, messageFactory => logs.Add(messageFactory()));

            bool result = InvokePrivate<bool>(service, "TryClickBeginButton", null!, Vector2.Zero);

            result.Should().BeFalse();
            logs.Should().Contain(log => log.Contains("Begin button not found", StringComparison.Ordinal));
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

        private static void SetDebugLog(UltimatumAutomationService service, Action<Func<string>> debugLog)
        {
            object dependencies = RuntimeMemberAccessor.GetRequiredMemberValue(service, "_dependencies")!;
            RuntimeMemberAccessor.SetRequiredMember(dependencies, "DebugLog", debugLog);
        }

        private static T InvokePrivate<T>(UltimatumAutomationService service, string methodName, params object?[] args)
        {
            MethodInfo method = typeof(UltimatumAutomationService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
            return (T)method.Invoke(service, args)!;
        }
    }
}