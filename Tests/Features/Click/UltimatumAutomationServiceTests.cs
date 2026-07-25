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

        [TestMethod]
        public void TryClickElement_ReturnsFalse_WhenCursorCannotBeMovedInsideWindow()
        {
            UltimatumAutomationService service = CreateService(
                ensureCursorInsideGameWindowForClick: static _ => false);

            bool result = InvokePrivate<bool>(
                service,
                "TryClickElement",
                new TestOptionElement(new RectangleF(10f, 20f, 30f, 40f)),
                new Vector2(100f, 200f),
                "outside",
                "reject",
                "click");

            result.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickElement_ReturnsFalse_WhenClickableAreaRejectsCenter()
        {
            List<string> logs = [];
            UltimatumAutomationService service = CreateService(
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: static (_, _) => false);
            SetDebugLog(service, messageFactory => logs.Add(messageFactory()));

            bool result = InvokePrivate<bool>(
                service,
                "TryClickElement",
                new TestOptionElement(new RectangleF(10f, 20f, 30f, 40f)),
                new Vector2(100f, 200f),
                "outside",
                "reject",
                "click");

            result.Should().BeFalse();
            logs.Should().ContainSingle().Which.Should().Contain("reject");
        }

        [TestMethod]
        public void TryClickElement_ReturnsTrue_WhenClickExecutes()
        {
            List<(Vector2 ClickPosition, Element? ClickElement)> clicks = [];
            int clickIntervals = 0;
            UltimatumAutomationService service = CreateService(
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: static (_, _) => true,
                performClick: (clickPosition, element) => clicks.Add((clickPosition, element)),
                recordClickInterval: () => clickIntervals++);
            TestOptionElement element = new(new RectangleF(10f, 20f, 30f, 40f));

            bool result = InvokePrivate<bool>(
                service,
                "TryClickElement",
                element,
                new Vector2(100f, 200f),
                "outside",
                "reject",
                "click");

            result.Should().BeTrue();
            clicks.Should().ContainSingle();
            clicks[0].ClickPosition.Should().Be(new Vector2(125f, 240f));
            clicks[0].ClickElement.Should().BeSameAs(element);
            clickIntervals.Should().Be(1);
        }

        [TestMethod]
        public void TryClickBeginButton_ReturnsFalse_WhenLabelTreeDoesNotResolveButton()
        {
            UltimatumAutomationService service = CreateService();
            LabelOnGround label = CreateUltimatumLabel(beginButton: null);

            bool result = InvokePrivate<bool>(service, "TryClickBeginButton", label, Vector2.Zero);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickBeginButton_ReturnsFalse_WhenResolvedButtonClickFails()
        {
            LabelOnGround label = CreateUltimatumLabel(beginButton: new TestOptionElement(new RectangleF(10f, 20f, 30f, 40f)));
            UltimatumAutomationService service = CreateService(
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: static (_, _) => false);

            bool result = InvokePrivate<bool>(service, "TryClickBeginButton", label, new Vector2(100f, 200f));

            result.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickBeginButton_ReturnsTrue_WhenResolvedButtonClickSucceeds()
        {
            List<(Vector2 ClickPosition, Element? ClickElement)> clicks = [];
            int clickIntervals = 0;
            TestOptionElement beginButton = new(new RectangleF(10f, 20f, 30f, 40f));
            LabelOnGround label = CreateUltimatumLabel(beginButton: beginButton);
            UltimatumAutomationService service = CreateService(
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: static (_, _) => true,
                performClick: (clickPosition, element) => clicks.Add((clickPosition, element)),
                recordClickInterval: () => clickIntervals++);

            bool result = InvokePrivate<bool>(service, "TryClickBeginButton", label, new Vector2(100f, 200f));

            result.Should().BeTrue();
            clicks.Should().ContainSingle();
            clicks[0].ClickPosition.Should().Be(new Vector2(125f, 240f));
            clicks[0].ClickElement.Should().BeSameAs(beginButton);
            clickIntervals.Should().Be(1);
        }

        [TestMethod]
        public void PublishInitialFailure_ReturnsFalse_AndIncludesCandidateCount_WhenDebugCaptureIsEnabled()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            UltimatumAutomationService service = CreateService(
                shouldCaptureUltimatumDebug: () => true,
                publishUltimatumDebug: debugEvents.Add,
                useNullGameController: true);

            bool result = InvokePrivate<bool>(service, "PublishInitialFailure", "InitialNoOptions", "No options discovered from ultimatum label tree", 3);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialNoOptions");
            debugEvents[0].CandidateCount.Should().Be(3);
            debugEvents[0].Notes.Should().Be("No options discovered from ultimatum label tree");
            debugEvents[0].IsGruelingGauntletActive.Should().BeFalse();
        }

        [TestMethod]
        public void GetGruelingGauntletDetectionForDebug_ReturnsFalse_WhenDebugCaptureIsDisabled()
        {
            ResetGruelingDetectionStore();
            UltimatumAutomationService service = CreateService(
                shouldCaptureUltimatumDebug: static () => false,
                useNullGameController: true);

            bool result = InvokePrivate<bool>(service, "GetGruelingGauntletDetectionForDebug");

            result.Should().BeFalse();
            UltimatumGruelingGauntletDetectionStore.TryGet(out _).Should().BeFalse();
        }

        [TestMethod]
        public void GetGruelingGauntletDetectionForDebug_PublishesFalseDetection_WhenDebugCaptureIsEnabledWithoutIngameData()
        {
            ResetGruelingDetectionStore();
            UltimatumAutomationService service = CreateService(
                shouldCaptureUltimatumDebug: static () => true,
                useNullGameController: true);

            bool result = InvokePrivate<bool>(service, "GetGruelingGauntletDetectionForDebug");

            result.Should().BeFalse();
            UltimatumGruelingGauntletDetectionStore.TryGet(out bool isActive).Should().BeTrue();
            isActive.Should().BeFalse();
        }

        [TestMethod]
        public void IsGruelingGauntletPassiveActive_ReturnsTrue_WhenAtlasPassiveExists()
        {
            ResetGruelingDetectionStore();
            GameController gameController = CreateGameControllerWithIngameData(new FakeIngameData
            {
                ServerData = new FakeServerData
                {
                    AtlasPassiveSkillIds = [9882]
                }
            });
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                gameController: gameController);

            bool result = InvokePrivate<bool>(service, "IsGruelingGauntletPassiveActive");

            result.Should().BeTrue();
            UltimatumGruelingGauntletDetectionStore.TryGet(out bool isActive).Should().BeTrue();
            isActive.Should().BeTrue();
        }

        [TestMethod]
        public void IsGruelingGauntletPassiveActive_ReturnsFalse_WhenIngameStateHasNoDataMember()
        {
            ResetGruelingDetectionStore();
            FakeGameControllerShim gameController = (FakeGameControllerShim)RuntimeHelpers.GetUninitializedObject(typeof(FakeGameControllerShim));
            gameController.IngameState = new object();
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                gameController: gameController);

            bool result = InvokePrivate<bool>(service, "IsGruelingGauntletPassiveActive");

            result.Should().BeFalse();
            UltimatumGruelingGauntletDetectionStore.TryGet(out bool isActive).Should().BeTrue();
            isActive.Should().BeFalse();
        }

        [TestMethod]
        public void CreateUltimatumLabel_ProducesVisibleUltimatumProbeShape()
        {
            LabelOnGround label = CreateUltimatumLabel();

            UltimatumLabelMath.TryGetLabelItemPath(label, out string path).Should().BeTrue();
            path.Should().Contain(Constants.UltimatumChallengeInteractablePath);
            UltimatumLabelMath.IsUltimatumLabel(label).Should().BeTrue();
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenNoOptionsAreResolved()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            LabelOnGround label = CreateUltimatumLabel();
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                publishUltimatumDebug: debugEvents.Add);

            bool result = service.TryClickPreferredModifier(label, Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialNoOptions");
            debugEvents[0].Notes.Should().Be("No options discovered from ultimatum label tree");
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenCollectorFindsNoEligibleCandidates()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            LabelOnGround label = CreateUltimatumLabel(options:
            [
                (CreateOpaqueElement(isValid: false), "Ignored")
            ]);
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                publishUltimatumDebug: debugEvents.Add);

            bool result = service.TryClickPreferredModifier(label, Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialNoPriorityCandidate");
            debugEvents[0].CandidateCount.Should().Be(1);
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenAllCandidatesAreUnranked()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            LabelOnGround label = CreateUltimatumLabel(options:
            [
                (new TestOptionElement(new RectangleF(10f, 20f, 30f, 40f)), "Unknown Modifier")
            ]);
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                publishUltimatumDebug: debugEvents.Add);

            bool result = service.TryClickPreferredModifier(label, Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialNoPriorityCandidate");
            debugEvents[0].Notes.Should().Be("No candidate matched ultimatum priority table");
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenPreferredChoiceClickFails()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            TestOptionElement option = new(new RectangleF(10f, 20f, 30f, 40f));
            LabelOnGround label = CreateUltimatumLabel(options: [(option, "Ruin III")]);
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                publishUltimatumDebug: debugEvents.Add,
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: static (_, _) => false);

            bool result = service.TryClickPreferredModifier(label, new Vector2(100f, 200f));

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialChoiceClickFailed");
            debugEvents[0].BestModifier.Should().Be("Ruin III");
            debugEvents[0].ClickedChoice.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenChoiceClickSucceedsButBeginClickFails()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            List<(Vector2 ClickPosition, Element? ClickElement)> clicks = [];
            TestOptionElement option = new(new RectangleF(10f, 20f, 30f, 40f));
            TestOptionElement beginButton = new(new RectangleF(50f, 60f, 30f, 40f));
            LabelOnGround label = CreateUltimatumLabel(options: [(option, "Ruin III")], beginButton: beginButton);
            int clickableCalls = 0;
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                publishUltimatumDebug: debugEvents.Add,
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: (_, _) => clickableCalls++ == 0,
                performClick: (clickPosition, element) => clicks.Add((clickPosition, element)));

            bool result = service.TryClickPreferredModifier(label, new Vector2(100f, 200f));

            result.Should().BeFalse();
            clicks.Should().ContainSingle();
            clicks[0].ClickElement.Should().BeSameAs(option);
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialHandled");
            debugEvents[0].ClickedChoice.Should().BeTrue();
            debugEvents[0].ClickedConfirm.Should().BeFalse();
            debugEvents[0].Notes.Should().Be("Choice clicked but begin click failed");
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsTrue_WhenChoiceAndBeginClicksSucceed()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            List<(Vector2 ClickPosition, Element? ClickElement)> clicks = [];
            TestOptionElement option = new(new RectangleF(10f, 20f, 30f, 40f));
            TestOptionElement beginButton = new(new RectangleF(50f, 60f, 30f, 40f));
            LabelOnGround label = CreateUltimatumLabel(options: [(option, "Ruin III")], beginButton: beginButton);
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                publishUltimatumDebug: debugEvents.Add,
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: static (_, _) => true,
                performClick: (clickPosition, element) => clicks.Add((clickPosition, element)),
                recordClickInterval: static () => { });

            bool result = service.TryClickPreferredModifier(label, new Vector2(100f, 200f));

            result.Should().BeTrue();
            clicks.Should().HaveCount(2);
            clicks[0].ClickElement.Should().BeSameAs(option);
            clicks[1].ClickElement.Should().BeSameAs(beginButton);
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialHandled");
            debugEvents[0].ClickedChoice.Should().BeTrue();
            debugEvents[0].ClickedConfirm.Should().BeTrue();
            debugEvents[0].Notes.Should().Be("Clicked preferred choice and begin");
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsFalse_WhenGruelingBeginClickFails()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            TestOptionElement beginButton = new(new RectangleF(50f, 60f, 30f, 40f));
            LabelOnGround label = CreateUltimatumLabel(
                options: [(new TestOptionElement(new RectangleF(10f, 20f, 30f, 40f)) { IsSaturated = true }, "Ruin I")],
                beginButton: beginButton);
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                gameController: CreateGameControllerWithIngameData(new FakeIngameData
                {
                    ServerData = new FakeServerData
                    {
                        AtlasPassiveSkillIds = [9882]
                    }
                }),
                publishUltimatumDebug: debugEvents.Add,
                shouldCaptureUltimatumDebug: static () => true,
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: static (_, _) => false);

            bool result = service.TryClickPreferredModifier(label, new Vector2(100f, 200f));

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialGruelingHandled");
            debugEvents[0].IsGruelingGauntletActive.Should().BeTrue();
            debugEvents[0].ShouldTakeReward.Should().BeFalse();
            debugEvents[0].ClickedConfirm.Should().BeFalse();
            debugEvents[0].Notes.Should().Be("Begin/confirm click failed on initial label");
        }

        [TestMethod]
        public void TryClickPreferredModifier_ReturnsTrue_WhenGruelingBeginClickSucceeds()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            List<(Vector2 ClickPosition, Element? ClickElement)> clicks = [];
            TestOptionElement beginButton = new(new RectangleF(50f, 60f, 30f, 40f));
            LabelOnGround label = CreateUltimatumLabel(
                options: [(new TestOptionElement(new RectangleF(10f, 20f, 30f, 40f)) { IsSaturated = true }, "Ruin I")],
                beginButton: beginButton);
            UltimatumAutomationService service = CreateService(
                settings: CreateInitialUltimatumEnabledSettings(),
                gameController: CreateGameControllerWithIngameData(new FakeIngameData
                {
                    ServerData = new FakeServerData
                    {
                        AtlasPassiveSkillIds = [9882]
                    }
                }),
                publishUltimatumDebug: debugEvents.Add,
                shouldCaptureUltimatumDebug: static () => true,
                ensureCursorInsideGameWindowForClick: static _ => true,
                isClickableInEitherSpace: static (_, _) => true,
                performClick: (clickPosition, element) => clicks.Add((clickPosition, element)));

            bool result = service.TryClickPreferredModifier(label, new Vector2(100f, 200f));

            result.Should().BeTrue();
            clicks.Should().ContainSingle();
            clicks[0].ClickElement.Should().BeSameAs(beginButton);
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("InitialGruelingHandled");
            debugEvents[0].ClickedConfirm.Should().BeTrue();
            debugEvents[0].HasSaturatedChoice.Should().BeTrue();
            debugEvents[0].Notes.Should().Be("Clicked begin/confirm path on initial label");
        }

        private static UltimatumAutomationService CreateService(
            ClickItSettings? settings = null,
            GameController? gameController = null,
            TimeCache<List<LabelOnGround>>? cachedLabels = null,
            bool useNullGameController = false,
            Action<UltimatumDebugEvent>? publishUltimatumDebug = null,
            Func<string, bool>? ensureCursorInsideGameWindowForClick = null,
            Func<Vector2, string, bool>? isClickableInEitherSpace = null,
            Action<Vector2, Element?>? performClick = null,
            Action? recordClickInterval = null,
            Func<bool>? shouldCaptureUltimatumDebug = null)
        {
            settings ??= new ClickItSettings();
            if (!useNullGameController)
                gameController ??= (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));

            cachedLabels ??= new TimeCache<List<LabelOnGround>>(() => [], 50);
            publishUltimatumDebug ??= static _ => { };
            ensureCursorInsideGameWindowForClick ??= static _ => true;
            isClickableInEitherSpace ??= static (_, _) => true;
            performClick ??= static (_, _) => { };
            recordClickInterval ??= static () => { };
            shouldCaptureUltimatumDebug ??= static () => false;

            return new UltimatumAutomationService(new UltimatumAutomationServiceDependencies(
                settings,
                gameController!,
                cachedLabels,
                ensureCursorInsideGameWindowForClick,
                isClickableInEitherSpace,
                _ => { },
                performClick,
                recordClickInterval,
                shouldCaptureUltimatumDebug,
                publishUltimatumDebug));
        }

        private static void SetDebugLog(UltimatumAutomationService service, Action<Func<string>> debugLog)
        {
            object dependencies = RuntimeMemberAccessor.GetRequiredMemberValue(service, "_dependencies")!;
            RuntimeMemberAccessor.SetRequiredMember(dependencies, "DebugLog", debugLog);
        }

        private static void ResetGruelingDetectionStore()
        {
            typeof(UltimatumGruelingGauntletDetectionStore)
                .GetField("_isActive", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, false);
            typeof(UltimatumGruelingGauntletDetectionStore)
                .GetField("_hasValue", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, false);
        }

        private static ClickItSettings CreateInitialUltimatumEnabledSettings()
        {
            ClickItSettings settings = new();
            settings.ClickInitialUltimatum.Value = true;
            settings.ClickUltimatumChoices.Value = true;
            return settings;
        }

        private static LabelOnGround CreateUltimatumLabel(
            string itemPath = "Metadata/Leagues/Ultimatum/Objects/UltimatumChallengeInteractable",
            IReadOnlyList<(Element OptionElement, string ModifierName)>? options = null,
            Element? beginButton = null,
            bool isVisible = true)
        {
            Entity item = EntityProbeFactory.Create(path: itemPath);
            UltimatumLabelProbe label = (UltimatumLabelProbe)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumLabelProbe));
            Element root = CreateUltimatumRoot(options ?? [], beginButton, isVisible);

            label.ItemOnGround = item;
            label.Label = root;
            return label;
        }

        private static Element CreateUltimatumRoot(
            IReadOnlyList<(Element OptionElement, string ModifierName)> options,
            Element? beginButton,
            bool isVisible)
        {
            UltimatumUiTreeResolverTests.ReflectionFriendlyNode optionContainer = new()
            {
                Children = options
                    .Select(static option => (object?)new UltimatumUiTreeResolverTests.ReflectionFriendlyChoiceOption
                    {
                        OptionElement = option.OptionElement,
                        Text = option.ModifierName
                    })
                    .ToArray()
            };

            UltimatumTreeElement branch = new()
            {
                Children = new object?[]
                {
                    null,
                    null,
                    new UltimatumUiTreeResolverTests.ReflectionFriendlyNode { Children = new object?[] { optionContainer } },
                    null,
                    new UltimatumTreeElement { Children = beginButton != null ? new object?[] { beginButton } : Array.Empty<object?>() }
                }
            };

            return new UltimatumTreeElement
            {
                Children = new object?[] { new UltimatumTreeElement { IsVisible = isVisible, Children = new object?[] { branch } } }
            };
        }

        private static GameController CreateGameControllerWithIngameData(object? ingameData)
        {
            FakeGameControllerShim gameController = (FakeGameControllerShim)RuntimeHelpers.GetUninitializedObject(typeof(FakeGameControllerShim));
            gameController.IngameState = new FakeIngameStateShim
            {
                Data = ingameData
            };
            return gameController;
        }

        private static Element CreateOpaqueElement(bool isValid)
        {
            return new TestOptionElement(new RectangleF(10f, 20f, 30f, 40f))
            {
                IsValid = isValid
            };
        }

        private static T InvokePrivate<T>(UltimatumAutomationService service, string methodName, params object?[] args)
        {
            MethodInfo method = typeof(UltimatumAutomationService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
            return (T)method.Invoke(service, args)!;
        }

        public sealed class TestOptionElement(RectangleF clientRect) : Element
        {
            public new bool IsValid { get; set; } = true;

            public new bool IsSaturated { get; set; }

            public override RectangleF GetClientRect() => clientRect;
        }

        public sealed class UltimatumTreeElement : Element
        {
            public new bool IsVisible { get; set; }

            public new IList? Children { get; set; }

            public new object? GetChildAtIndex(int index)
                => Children != null && index >= 0 && index < Children.Count ? Children[index] : null;
        }

        public sealed class UltimatumLabelProbe : LabelOnGround
        {
            public new Entity? ItemOnGround { get; set; }

            public new Element? Label { get; set; }
        }

        public sealed class FakeIngameData
        {
            public object? ServerData { get; set; }
        }

        public sealed class FakeServerData
        {
            public object[] AtlasPassiveSkillIds { get; set; } = [];
        }

        public sealed class FakeGameControllerShim : GameController
        {
            public FakeGameControllerShim()
                : base(null!, null!, null!, null!)
            {
            }

            public new object? IngameState { get; set; }
        }

        public sealed class FakeIngameStateShim
        {
            public object? Data { get; set; }
        }

    }
}