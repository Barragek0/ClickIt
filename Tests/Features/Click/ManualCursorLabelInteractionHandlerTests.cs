namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ManualCursorLabelInteractionHandlerTests
    {
        [TestMethod]
        public void TryClickCandidate_ReturnsFalse_WhenHoveredLabelIsNull()
        {
            var handler = CreateHandler(new ClickItSettings(), []);

            bool clicked = handler.TryClickCandidate(null!, null, Vector2.Zero, Vector2.Zero, null);

            clicked.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickPreferredAltarOption_ReturnsFalse_WhenNoTrackedAltarsExist()
        {
            var handler = CreateHandler(new ClickItSettings(), []);

            bool clicked = handler.TryClickPreferredAltarOption(Vector2.Zero, Vector2.Zero);

            clicked.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickCandidate_ReturnsFalse_WhenClickPositionCannotBeResolved()
        {
            var settings = new ClickItSettings();
            var handler = CreateHandler(
                settings,
                [],
                labelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(
                    labelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                    tryResolveClickPosition: static (_, _, _, _) => (false, default)));

            bool clicked = handler.TryClickCandidate(
                ExileCoreOpaqueFactory.CreateOpaqueLabel(),
                null,
                Vector2.Zero,
                Vector2.Zero,
                null);

            clicked.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickCandidate_ReturnsFalse_WhenManualCursorInteractionFails()
        {
            var settings = new ClickItSettings();
            var handler = CreateHandler(
                settings,
                [],
                labelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(
                    labelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                    tryResolveClickPosition: static (_, _, _, _) => (true, new Vector2(25f, 40f)),
                    executeInteraction: static _ => false));

            bool clicked = handler.TryClickCandidate(
                ExileCoreOpaqueFactory.CreateOpaqueLabel(),
                null,
                new Vector2(10f, 10f),
                Vector2.Zero,
                null);

            clicked.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickCandidate_ReturnsTrue_WhenManualCursorInteractionSucceeds()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = { Value = false },
                LazyMode = { Value = false }
            };

            var requests = new List<InteractionExecutionRequest>();
            var handler = CreateHandler(
                settings,
                [],
                labelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(
                    labelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                    tryResolveClickPosition: static (_, _, _, _) => (true, new Vector2(25f, 40f)),
                    executeInteraction: request =>
                    {
                        requests.Add(request);
                        return true;
                    }));

            bool clicked = handler.TryClickCandidate(
                ExileCoreOpaqueFactory.CreateOpaqueLabel(),
                null,
                new Vector2(10f, 10f),
                Vector2.Zero,
                null);

            clicked.Should().BeTrue();
            requests.Should().ContainSingle();
            requests[0].ClickPosition.Should().Be(new Vector2(25f, 40f));
            requests[0].AllowWhenHotkeyInactive.Should().BeTrue();
            requests[0].AvoidCursorMove.Should().BeTrue();
            requests[0].UseHoldClick.Should().BeFalse();
        }

        private static ManualCursorLabelInteractionHandler CreateHandler(
            ClickItSettings settings,
            IReadOnlyList<PrimaryAltarComponent> altarSnapshot,
            ClickLabelInteractionService? labelInteraction = null)
        {
            return new ManualCursorLabelInteractionHandler(new ManualCursorLabelInteractionHandlerDependencies(
                Settings: settings,
                AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings, altarSnapshot),
                LabelInteraction: labelInteraction ?? ClickTestServiceFactory.CreateLabelInteractionService(),
                ChestLootSettlement: CreateChestLootSettlementTracker(),
                PathfindingLabelSuppression: new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, new ClickRuntimeState())),
                PathfindingService: (PathfindingService)RuntimeHelpers.GetUninitializedObject(typeof(PathfindingService)),
                UltimatumAutomation: ClickTestServiceFactory.CreateUltimatumAutomationService(settings)));
        }

        private static ChestLootSettlementTracker CreateChestLootSettlementTracker()
        {
            return new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                Settings: new ClickItSettings(),
                State: new ChestLootSettlementState(),
                GroundLabelEntityAddresses: (GroundLabelEntityAddressProvider)RuntimeHelpers.GetUninitializedObject(typeof(GroundLabelEntityAddressProvider)),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService()));
        }
    }
}