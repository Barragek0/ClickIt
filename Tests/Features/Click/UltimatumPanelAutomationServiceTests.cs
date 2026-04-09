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

        private static UltimatumPanelAutomationService CreateService(
            ClickItSettings? settings = null,
            GameController? gameController = null,
            bool useNullGameController = false,
            Action<UltimatumDebugEvent>? publishUltimatumDebug = null,
            Func<Element, Vector2, string, string, string, bool>? tryClickElement = null,
            Func<bool>? isGruelingGauntletPassiveActive = null,
            Func<bool>? getGruelingGauntletDetectionForDebug = null)
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
                getGruelingGauntletDetectionForDebug));
        }
    }
}