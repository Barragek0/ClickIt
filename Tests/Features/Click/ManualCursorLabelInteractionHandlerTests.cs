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

        private static ManualCursorLabelInteractionHandler CreateHandler(
            ClickItSettings settings,
            IReadOnlyList<PrimaryAltarComponent> altarSnapshot)
        {
            return new ManualCursorLabelInteractionHandler(new ManualCursorLabelInteractionHandlerDependencies(
                Settings: settings,
                AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings, altarSnapshot),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(),
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