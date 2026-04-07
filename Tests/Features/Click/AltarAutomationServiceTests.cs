namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class AltarAutomationServiceTests
    {
        [TestMethod]
        public void HasClickableAltars_ReturnsFalse_WhenBothAltarTypesAreDisabled()
        {
            var settings = new ClickItSettings
            {
                ClickEaterAltars = new(false),
                ClickExarchAltars = new(false)
            };

            var service = CreateService(settings, [CreateAltar(AltarType.EaterOfWorlds)]);

            service.HasClickableAltars().Should().BeFalse();
        }

        [TestMethod]
        public void TryClickManualCursorPreferredAltarOption_ReturnsFalse_WhenThereAreNoTrackedAltars()
        {
            var service = CreateService(new ClickItSettings(), []);

            bool clicked = service.TryClickManualCursorPreferredAltarOption(new Vector2(10f, 10f), new Vector2(0f, 0f));

            clicked.Should().BeFalse();
        }

        [TestMethod]
        public void ProcessAltarClicking_CompletesImmediately_WhenThereAreNoTrackedAltars()
        {
            var service = CreateService(new ClickItSettings(), []);

            var enumerator = service.ProcessAltarClicking();

            enumerator.MoveNext().Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenAltarTypeIsNotEnabled()
        {
            var service = CreateService(new ClickItSettings(), []);
            var altar = CreateAltar(AltarType.Unknown);

            service.ShouldClickAltar(altar, clickEater: true, clickExarch: true).Should().BeFalse();
        }

        [TestMethod]
        public void HasClickableAltars_ReturnsFalse_AndLogsValidationFailure_WhenEnabledAltarIsNotValidCached()
        {
            List<string> debugLogs = [];
            var settings = new ClickItSettings
            {
                ClickEaterAltars = new(true),
                ClickExarchAltars = new(false)
            };
            var service = CreateService(settings, [CreateAltar(AltarType.EaterOfWorlds)], debugLogs: debugLogs);

            bool result = service.HasClickableAltars();

            result.Should().BeFalse();
            debugLogs.Should().ContainSingle().Which.Should().Be("Skipping altar - Validation failed");
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_AndLogsValidationFailure_WhenEnabledAltarIsNotValidCached()
        {
            List<string> debugLogs = [];
            var altar = CreateAltar(AltarType.EaterOfWorlds);
            var service = CreateService(
                new ClickItSettings(),
                [altar],
                configureSettings: static settings =>
                {
                    settings.ClickEaterAltars = new(true);
                    settings.ClickExarchAltars = new(false);
                },
                debugLogs: debugLogs);

            bool shouldClick = service.ShouldClickAltar(altar, clickEater: true, clickExarch: false);

            shouldClick.Should().BeFalse();
            debugLogs.Should().ContainSingle().Which.Should().Be("Skipping altar - Validation failed");
        }

        [TestMethod]
        public void ProcessAltarClicking_SkipsInvalidAltars_WithoutExecutingInteraction()
        {
            int executionCalls = 0;
            List<string> debugLogs = [];
            var settings = new ClickItSettings
            {
                ClickEaterAltars = new(true),
                ClickExarchAltars = new(false)
            };
            var service = CreateService(
                settings,
                [CreateAltar(AltarType.EaterOfWorlds)],
                executeInteraction: _ =>
                {
                    executionCalls++;
                    return true;
                },
                debugLogs: debugLogs);

            var enumerator = service.ProcessAltarClicking();

            enumerator.MoveNext().Should().BeFalse();
            executionCalls.Should().Be(0);
            debugLogs.Should().ContainSingle().Which.Should().Be("Skipping altar - Validation failed");
        }

        private static AltarAutomationService CreateService(
            ClickItSettings settings,
            IReadOnlyList<PrimaryAltarComponent> snapshot,
            Action<ClickItSettings>? configureSettings = null,
            Func<InteractionExecutionRequest, bool>? executeInteraction = null,
            List<string>? debugLogs = null)
        {
            configureSettings?.Invoke(settings);

            return new AltarAutomationService(new AltarAutomationServiceDependencies(
                Settings: settings,
                GameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(0f, 0f, 1280f, 720f)),
                GetAltarSnapshot: () => snapshot,
                RemoveTrackedAltarByElement: static _ => { },
                CalculateAltarWeights: static _ => TestBuilders.BuildAltarWeights(topWeight: 50, bottomWeight: 10),
                DetermineAltarChoice: static (_, _, _, _, _) => null,
                IsClickableInEitherSpace: static (_, _) => false,
                EnsureCursorInsideGameWindowForClick: static _ => true,
                ExecuteInteraction: executeInteraction ?? (_ => false),
                DebugLog: message => debugLogs?.Add(message),
                LogError: static (_, _) => { },
                ElementAccessLock: new object()));
        }

        private static PrimaryAltarComponent CreateAltar(AltarType altarType)
        {
            var altar = TestBuilders.BuildPrimary(
                new SecondaryAltarComponent(null, [], []),
                new SecondaryAltarComponent(null, [], []));
            altar.AltarType = altarType;
            return altar;
        }
    }
}