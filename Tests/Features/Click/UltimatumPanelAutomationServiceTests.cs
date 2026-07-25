namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumPanelAutomationServiceTests
    {
        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenOtherUltimatumClickIsDisabled_WithoutTouchingPanelQueries()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            bool gruelingCheckInvoked = false;
            bool gruelingDebugInvoked = false;
            bool clickInvoked = false;
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = false;
            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: () =>
                {
                    gruelingCheckInvoked = true;
                    return true;
                },
                getGruelingGauntletDetectionForDebug: () =>
                {
                    gruelingDebugInvoked = true;
                    return true;
                },
                tryClickElement: (_, _, _, _, _) =>
                {
                    clickInvoked = true;
                    return false;
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            clickInvoked.Should().BeFalse();
            gruelingCheckInvoked.Should().BeFalse();
            gruelingDebugInvoked.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelSkip");
            debugEvents[0].Notes.Should().Be("Other Ultimatum click setting disabled");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenPanelIsMissing_AndPublishesPanelMissingDebug()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            bool gruelingCheckInvoked = false;
            bool gruelingDebugInvoked = false;
            bool clickInvoked = false;
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;
            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                useNullGameController: true,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: () =>
                {
                    gruelingCheckInvoked = true;
                    return true;
                },
                getGruelingGauntletDetectionForDebug: () =>
                {
                    gruelingDebugInvoked = true;
                    return true;
                },
                tryClickElement: (_, _, _, _, _) =>
                {
                    clickInvoked = true;
                    return true;
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            clickInvoked.Should().BeFalse();
            gruelingCheckInvoked.Should().BeFalse();
            gruelingDebugInvoked.Should().BeTrue();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelMissing");
            debugEvents[0].IsGruelingGauntletActive.Should().BeTrue();
            debugEvents[0].Notes.Should().Be("Ultimatum panel not visible/available");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenVisiblePanelObjectHasWrongRuntimeType()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            bool gruelingCheckInvoked = false;
            bool gruelingDebugInvoked = false;
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;
            UltimatumPanelUiQueryTests.FakeGameControllerShim gameController =
                (UltimatumPanelUiQueryTests.FakeGameControllerShim)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanelUiQueryTests.FakeGameControllerShim));
            gameController.IngameState = new UltimatumPanelUiQueryTests.FakeIngameState
            {
                IngameUi = new UltimatumPanelUiQueryTests.FakeIngameUi
                {
                    UltimatumPanel = new UltimatumPanelUiQueryTests.FakeUltimatumPanel { IsVisible = true }
                }
            };

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                gameController: gameController,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: () =>
                {
                    gruelingCheckInvoked = true;
                    return true;
                },
                getGruelingGauntletDetectionForDebug: () =>
                {
                    gruelingDebugInvoked = true;
                    return false;
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            gruelingCheckInvoked.Should().BeFalse();
            gruelingDebugInvoked.Should().BeTrue();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelMissing");
            debugEvents[0].IsGruelingGauntletActive.Should().BeFalse();
            debugEvents[0].Notes.Should().Be("Ultimatum panel not visible/available");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenVisiblePanelObjectIsHidden()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            bool gruelingCheckInvoked = false;
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;
            UltimatumPanelUiQueryTests.FakeGameControllerShim gameController =
                (UltimatumPanelUiQueryTests.FakeGameControllerShim)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanelUiQueryTests.FakeGameControllerShim));
            gameController.IngameState = new UltimatumPanelUiQueryTests.FakeIngameState
            {
                IngameUi = new UltimatumPanelUiQueryTests.FakeIngameUi
                {
                    UltimatumPanel = new UltimatumPanelUiQueryTests.FakeUltimatumPanel { IsVisible = false }
                }
            };

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                gameController: gameController,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: () =>
                {
                    gruelingCheckInvoked = true;
                    return true;
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            gruelingCheckInvoked.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelMissing");
            debugEvents[0].Notes.Should().Be("Ultimatum panel not visible/available");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsTrue_WhenChoiceAndConfirmClicksSucceed_ThroughRuntimeSeam()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            List<Element> clickedElements = [];
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            Element choiceElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            Element confirmElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add,
                tryClickElement: (element, _, _, _, _) =>
                {
                    clickedElements.Add(element);
                    return true;
                },
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    VisiblePanel = panel,
                    ChoiceCandidates = [new UltimatumPanelChoiceCandidate(choiceElement, "Ruin II", 0, false)],
                    ConfirmButton = confirmElement
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeTrue();
            clickedElements.Should().Equal(choiceElement, confirmElement);
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelHandled");
            debugEvents[0].ClickedChoice.Should().BeTrue();
            debugEvents[0].ClickedConfirm.Should().BeTrue();
            debugEvents[0].ClickedTakeRewards.Should().BeFalse();
            debugEvents[0].Notes.Should().Be("Panel action executed");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenChoiceClickFails_AndConfirmCannotBeResolved_ThroughRuntimeSeam()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            Element choiceElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add,
                tryClickElement: (element, _, _, _, _) => element != choiceElement,
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    VisiblePanel = panel,
                    ChoiceCandidates = [new UltimatumPanelChoiceCandidate(choiceElement, "Ruin II", 0, false)]
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelHandled");
            debugEvents[0].ClickedChoice.Should().BeFalse();
            debugEvents[0].ClickedConfirm.Should().BeFalse();
            debugEvents[0].ClickedTakeRewards.Should().BeFalse();
            debugEvents[0].Notes.Should().Be("No panel action executed");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsTrue_WhenGruelingTakeRewardsClickSucceeds_ThroughRuntimeSeam()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            List<Element> clickedElements = [];
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;
            settings.ClickUltimatumTakeRewardButton.Value = true;
            settings.UltimatumTakeRewardModifierNames = new HashSet<string>(["Ruin I"], StringComparer.OrdinalIgnoreCase);
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            Element takeRewardsElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: static () => true,
                tryClickElement: (element, _, _, _, _) =>
                {
                    clickedElements.Add(element);
                    return true;
                },
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    VisiblePanel = panel,
                    ChoiceCandidates = [new UltimatumPanelChoiceCandidate((Element)RuntimeHelpers.GetUninitializedObject(typeof(Element)), "Ruin I", 0, true)],
                    TakeRewardsButton = takeRewardsElement
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeTrue();
            clickedElements.Should().Equal(takeRewardsElement);
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelGruelingHandled");
            debugEvents[0].ClickedChoice.Should().BeFalse();
            debugEvents[0].ClickedConfirm.Should().BeFalse();
            debugEvents[0].ClickedTakeRewards.Should().BeTrue();
            debugEvents[0].ShouldTakeReward.Should().BeTrue();
            debugEvents[0].Action.Should().Be("TakeRewards");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenGruelingTakeRewardsDecisionIsSuppressedBySettings_ThroughRuntimeSeam()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            bool clickInvoked = false;
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;
            settings.ClickUltimatumTakeRewardButton.Value = false;
            settings.UltimatumTakeRewardModifierNames = new HashSet<string>(["Ruin I"], StringComparer.OrdinalIgnoreCase);
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: static () => true,
                tryClickElement: (_, _, _, _, _) =>
                {
                    clickInvoked = true;
                    return true;
                },
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    VisiblePanel = panel,
                    ChoiceCandidates = [new UltimatumPanelChoiceCandidate((Element)RuntimeHelpers.GetUninitializedObject(typeof(Element)), "Ruin I", 0, true)]
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            clickInvoked.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelGruelingHandled");
            debugEvents[0].ClickedTakeRewards.Should().BeFalse();
            debugEvents[0].ShouldTakeReward.Should().BeTrue();
            debugEvents[0].Notes.Should().Be("Take Reward matched but Click Take Reward Button is disabled; no click performed");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenGruelingTakeRewardsClickFails_ThroughRuntimeSeam()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;
            settings.ClickUltimatumTakeRewardButton.Value = true;
            settings.UltimatumTakeRewardModifierNames = new HashSet<string>(["Ruin I"], StringComparer.OrdinalIgnoreCase);
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            Element takeRewardsElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: static () => true,
                tryClickElement: static (_, _, _, _, _) => false,
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    VisiblePanel = panel,
                    ChoiceCandidates = [new UltimatumPanelChoiceCandidate((Element)RuntimeHelpers.GetUninitializedObject(typeof(Element)), "Ruin I", 0, true)],
                    TakeRewardsButton = takeRewardsElement
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelGruelingHandled");
            debugEvents[0].Action.Should().Be("TakeRewards");
            debugEvents[0].ClickedTakeRewards.Should().BeFalse();
            debugEvents[0].Notes.Should().Be("Take Rewards action selected but click failed");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsTrue_WhenGruelingCollectorFindsNoCandidates_AndConfirmClickSucceeds_ThroughRuntimeSeam()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            List<Element> clickedElements = [];
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            Element confirmElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: static () => true,
                tryClickElement: (element, _, _, _, _) =>
                {
                    clickedElements.Add(element);
                    return true;
                },
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    VisiblePanel = panel,
                    ConfirmButton = confirmElement
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeTrue();
            clickedElements.Should().Equal(confirmElement);
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelGruelingHandled");
            debugEvents[0].CandidateCount.Should().Be(0);
            debugEvents[0].Action.Should().Be("ConfirmOnly");
            debugEvents[0].ClickedConfirm.Should().BeTrue();
            debugEvents[0].ClickedTakeRewards.Should().BeFalse();
            debugEvents[0].Notes.Should().Be("Confirm clicked");
        }

        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenGruelingConfirmActionClickFails_ThroughRuntimeSeam()
        {
            List<UltimatumDebugEvent> debugEvents = [];
            Element confirmElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = true;

            UltimatumPanelAutomationService service = CreateService(
                settings: settings,
                publishUltimatumDebug: debugEvents.Add,
                isGruelingGauntletPassiveActive: static () => true,
                tryClickElement: static (_, _, _, _, _) => false,
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    VisiblePanel = panel,
                    ChoiceCandidates = [new UltimatumPanelChoiceCandidate((Element)RuntimeHelpers.GetUninitializedObject(typeof(Element)), "Safe Mod", 0, false)],
                    ConfirmButton = confirmElement
                });

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
            debugEvents.Should().ContainSingle();
            debugEvents[0].Stage.Should().Be("PanelGruelingHandled");
            debugEvents[0].Action.Should().Be("ConfirmOnly");
            debugEvents[0].ClickedConfirm.Should().BeFalse();
            debugEvents[0].ClickedTakeRewards.Should().BeFalse();
            debugEvents[0].Notes.Should().Be("Confirm action selected but click failed");
        }

        [TestMethod]
        public void TryClickPanelChoice_ReturnsFalse_WhenGruelingGauntletIsActive_EvenWhenCandidatesExist()
        {
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            Element choiceElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            bool clickInvoked = false;

            UltimatumPanelAutomationService service = CreateService(
                isGruelingGauntletPassiveActive: static () => true,
                tryClickElement: (_, _, _, _, _) =>
                {
                    clickInvoked = true;
                    return true;
                },
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    ChoiceCandidates = [new UltimatumPanelChoiceCandidate(choiceElement, "Ruin II", 0, true)]
                });

            bool result = InvokePrivate<bool>(service, "TryClickPanelChoice", panel, Vector2.Zero);

            result.Should().BeFalse();
            clickInvoked.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickPanelChoice_ReturnsFalse_WhenCollectorReturnsNoCandidates()
        {
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));

            UltimatumPanelAutomationService service = CreateService(
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam());

            bool result = InvokePrivate<bool>(service, "TryClickPanelChoice", panel, Vector2.Zero);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickPanelChoice_ReturnsFalse_WhenCandidatesExistButNoRankedChoiceCanBeSelected()
        {
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            bool clickInvoked = false;

            UltimatumPanelAutomationService service = CreateService(
                tryClickElement: (_, _, _, _, _) =>
                {
                    clickInvoked = true;
                    return true;
                },
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam
                {
                    ChoiceCandidates = [new UltimatumPanelChoiceCandidate((Element)RuntimeHelpers.GetUninitializedObject(typeof(Element)), "Unranked", int.MaxValue, false)]
                });

            bool result = InvokePrivate<bool>(service, "TryClickPanelChoice", panel, Vector2.Zero);

            result.Should().BeFalse();
            clickInvoked.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickPanelTakeRewards_ReturnsFalse_WhenTakeRewardsButtonCannotBeResolved()
        {
            UltimatumPanel panel = (UltimatumPanel)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumPanel));
            bool clickInvoked = false;

            UltimatumPanelAutomationService service = CreateService(
                tryClickElement: (_, _, _, _, _) =>
                {
                    clickInvoked = true;
                    return true;
                },
                panelRuntimeSeam: new StubUltimatumPanelRuntimeSeam());

            bool result = InvokePrivate<bool>(service, "TryClickPanelTakeRewards", panel, Vector2.Zero);

            result.Should().BeFalse();
            clickInvoked.Should().BeFalse();
        }

        private static UltimatumPanelAutomationService CreateService(
            ClickItSettings? settings = null,
            GameController? gameController = null,
            bool useNullGameController = false,
            Action<UltimatumDebugEvent>? publishUltimatumDebug = null,
            Func<Element, Vector2, string, string, string, bool>? tryClickElement = null,
            Func<bool>? isGruelingGauntletPassiveActive = null,
            Func<bool>? getGruelingGauntletDetectionForDebug = null,
            IUltimatumPanelRuntimeSeam? panelRuntimeSeam = null)
        {
            settings ??= new ClickItSettings();
            if (!useNullGameController)
                gameController ??= ExileCoreOpaqueFactory.CreateOpaqueGameController();
            publishUltimatumDebug ??= static _ => { };
            tryClickElement ??= static (_, _, _, _, _) => false;
            isGruelingGauntletPassiveActive ??= static () => false;
            getGruelingGauntletDetectionForDebug ??= static () => false;

            UltimatumAutomationServiceDependencies automation = new(
                settings,
                gameController!,
                new TimeCache<List<LabelOnGround>>(() => [], 50),
                _ => true,
                (_, _) => true,
                _ => { },
                (_, _) => { },
                () => { },
                () => false,
                publishUltimatumDebug);

            return new UltimatumPanelAutomationService(new UltimatumPanelAutomationServiceDependencies(
                automation,
                tryClickElement,
                isGruelingGauntletPassiveActive,
                getGruelingGauntletDetectionForDebug,
                panelRuntimeSeam));
        }

        private static T InvokePrivate<T>(UltimatumPanelAutomationService service, string methodName, params object?[] args)
        {
            MethodInfo method = typeof(UltimatumPanelAutomationService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
            return (T)method.Invoke(service, args)!;
        }

        private sealed class StubUltimatumPanelRuntimeSeam : IUltimatumPanelRuntimeSeam
        {
            public UltimatumPanel? VisiblePanel { get; init; }

            public List<UltimatumPanelChoiceCandidate>? ChoiceCandidates { get; init; }

            public Element? ConfirmButton { get; init; }

            public Element? TakeRewardsButton { get; init; }

            public bool TryGetVisiblePanel(GameController? gameController, bool logFailures, Action<string> debugLog, out UltimatumPanel? panelObj)
            {
                panelObj = VisiblePanel;
                if (panelObj != null)
                    return true;

                if (logFailures)
                    debugLog("[TryHandleUltimatumPanelUi] UltimatumPanel not available.");
                return false;
            }

            public bool TryCollectPanelChoiceCandidates(
                UltimatumPanel panelObj,
                IReadOnlyList<string> priorities,
                bool isGruelingGauntletActive,
                bool logFailures,
                Action<string> debugLog,
                out List<UltimatumPanelChoiceCandidate> candidates)
            {
                candidates = ChoiceCandidates?.ToList() ?? [];
                return candidates.Count > 0;
            }

            public bool TryResolveConfirmButton(UltimatumPanel panelObj, Action<string> debugLog, out Element resolved)
            {
                if (ConfirmButton != null)
                {
                    resolved = ConfirmButton;
                    return true;
                }

                resolved = null!;
                debugLog("[TryClickUltimatumPanelConfirm] ConfirmButton missing.");
                return false;
            }

            public bool TryResolveTakeRewardsButton(UltimatumPanel panelObj, Action<string> debugLog, out Element resolved)
            {
                if (TakeRewardsButton != null)
                {
                    resolved = TakeRewardsButton;
                    return true;
                }

                resolved = null!;
                debugLog("[TryClickUltimatumPanelTakeRewards] Take Rewards button missing at UltimatumPanel.Child(1).Child(4).Child(0).");
                return false;
            }
        }
    }
}