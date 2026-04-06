namespace ClickIt.Tests.Features.Labels.Application
{
    [TestClass]
    public class LabelFilterPortTelemetryTests
    {
        [TestMethod]
        public void GetVisibleLabelCounts_ReturnsUnavailable_WhenGameControllerMissing()
        {
            var settings = new ClickItSettings();
            var labelFilterPort = new LabelFilterPort(
                settings,
                new EssenceService(settings),
                new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { }),
                gameController: null);

            (bool labelsAvailable, int totalVisibleLabels, int validVisibleLabels) = labelFilterPort.GetVisibleLabelCounts();

            labelsAvailable.Should().BeFalse();
            totalVisibleLabels.Should().Be(0);
            validVisibleLabels.Should().Be(0);
        }

        [TestMethod]
        public void LabelDebugService_GetVisibleLabelCounts_ReturnsUnavailable_WhenGameControllerMissing()
        {
            var settings = new ClickItSettings();
            var service = new LabelDebugService(
                settings,
                new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { }),
                gameController: null,
                createClickSettings: static _ => new ClickSettings(),
                shouldAllowWorldItemByMetadata: static (_, _, _, _) => true,
                mechanicResolutionService: CreateMechanicResolutionService(),
                diagnostics: new LabelSelectionDiagnostics(24));

            (bool labelsAvailable, int totalVisibleLabels, int validVisibleLabels) = service.GetVisibleLabelCounts();

            labelsAvailable.Should().BeFalse();
            totalVisibleLabels.Should().Be(0);
            validVisibleLabels.Should().Be(0);
        }

        [TestMethod]
        public void LabelDebugService_GetLatestDebugAndTrail_ReturnEmptyState_WhenNoEventsPublished()
        {
            var settings = new ClickItSettings();
            var service = new LabelDebugService(
                settings,
                new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { }),
                gameController: null,
                createClickSettings: static _ => new ClickSettings(),
                shouldAllowWorldItemByMetadata: static (_, _, _, _) => true,
                mechanicResolutionService: CreateMechanicResolutionService(),
                diagnostics: new LabelSelectionDiagnostics(24));

            LabelDebugSnapshot snapshot = service.GetLatestDebug();
            IReadOnlyList<string> trail = service.GetLatestDebugTrail();

            snapshot.Should().Be(LabelDebugSnapshot.Empty);
            trail.Should().BeEmpty();
        }

        private static LabelMechanicResolutionService CreateMechanicResolutionService()
        {
            var settings = new ClickItSettings();

            return new LabelMechanicResolutionService(
                gameController: null,
                createClickSettings: static _ => new ClickSettings(),
                getClassificationDependencies: () => MechanicClassifierDependenciesFactory.Create(
                    new WorldItemMetadataPolicy(),
                    new LabelInteractionRuleService(
                        new WorldItemMetadataPolicy(),
                        InventoryDomainFactory.Create(new InventoryDomainFactoryDependencies(
                            new WorldItemMetadataPolicy().GetWorldItemBaseName,
                            InventoryMetadataIdentifiers.StoneOfPassage)).InteractionPolicy)));
        }
    }
}