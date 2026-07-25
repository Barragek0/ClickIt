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

        [TestMethod]
        public void LabelDebugService_GetSelectionDebugSummary_ReturnsDefault_WhenLabelsMissing()
        {
            var service = CreateLabelDebugService(CreateClickSettings());

            service.GetSelectionDebugSummary(allLabels: null, startIndex: 0, maxCount: 1).Should().Be(default(SelectionDebugSummary));
            service.GetSelectionDebugSummary([], startIndex: 0, maxCount: 1).Should().Be(default(SelectionDebugSummary));
        }

        [TestMethod]
        public void LabelDebugService_GetSelectionDebugSummary_ReturnsDefault_WhenRangeDoesNotIncludeAnyLabels()
        {
            var service = CreateLabelDebugService(CreateClickSettings());
            List<LabelOnGround> labels = [ExileCoreVisibleObjectBuilder.CreateSelectableLabel()];

            service.GetSelectionDebugSummary(labels, startIndex: 5, maxCount: 1).Should().Be(default(SelectionDebugSummary));
            service.GetSelectionDebugSummary(labels, startIndex: 0, maxCount: 0).Should().Be(default(SelectionDebugSummary));
        }

        [TestMethod]
        public void LabelDebugService_BuildSelectionDebugSummary_CountsMixedCases_ThroughEntityValueSeam()
        {
            ClickSettings clickSettings = CreateClickSettings(clickDistance: 10);
            LabelDebugCandidate[] candidates =
            [
                LabelDebugCandidate.NullLabel,
                LabelDebugCandidate.NullEntity,
                new LabelDebugCandidate(
                    HasLabel: true,
                    HasItem: true,
                    Path: MechanicIds.SettlersPetrifiedWoodMarker,
                    Type: EntityType.WorldItem,
                    DistancePlayer: 25f,
                    IsTargetable: true,
                    MechanicId: null,
                    AllowWorldItemByMetadata: false),
                new LabelDebugCandidate(
                    HasLabel: true,
                    HasItem: true,
                    Path: MechanicIds.SettlersCopperMarker,
                    Type: EntityType.WorldItem,
                    DistancePlayer: 5f,
                    IsTargetable: true,
                    MechanicId: null,
                    AllowWorldItemByMetadata: true),
                new LabelDebugCandidate(
                    HasLabel: true,
                    HasItem: true,
                    Path: MechanicIds.SettlersVerisiumMarker,
                    Type: EntityType.WorldItem,
                    DistancePlayer: 5f,
                    IsTargetable: true,
                    MechanicId: MechanicIds.SettlersVerisium,
                    AllowWorldItemByMetadata: true),
                new LabelDebugCandidate(
                    HasLabel: true,
                    HasItem: true,
                    Path: MechanicIds.SettlersBismuthMarker,
                    Type: EntityType.WorldItem,
                    DistancePlayer: 5f,
                    IsTargetable: false,
                    MechanicId: null,
                    AllowWorldItemByMetadata: true)
            ];

            SelectionDebugSummary summary = LabelDebugService.BuildSelectionDebugSummary(candidates, startIndex: 0, maxCount: candidates.Length, clickSettings);

            summary.Total.Should().Be(6);
            summary.NullLabel.Should().Be(1);
            summary.NullEntity.Should().Be(1);
            summary.WorldItem.Should().Be(4);
            summary.WorldItemMetadataRejected.Should().Be(1);
            summary.OutOfDistance.Should().Be(1);
            summary.Untargetable.Should().Be(1);
            summary.NoMechanic.Should().Be(1);
            summary.SettlersPathSeen.Should().Be(4);
            summary.SettlersMechanicMatched.Should().Be(1);
            summary.SettlersMechanicDisabled.Should().Be(1);
        }

        [TestMethod]
        public void LabelDebugService_LogSelectionDiagnostics_LogsNone_WhenNoLabelsAreAvailable()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            List<string> messages = [];
            var service = CreateLabelDebugService(CreateClickSettings(), settings: settings, messages: messages);

            service.LogSelectionDiagnostics(allLabels: null, startIndex: 0, maxCount: 1);

            messages.Should().ContainSingle();
            messages[0].Should().Be("[LabelFilterDiag] none");
        }

        [TestMethod]
        public void LabelDebugService_LogSelectionDiagnostics_LogsBadRange_WhenWindowIsEmpty()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            List<string> messages = [];
            var service = CreateLabelDebugService(CreateClickSettings(), settings: settings, messages: messages);

            service.LogSelectionDiagnostics([ExileCoreVisibleObjectBuilder.CreateSelectableLabel()], 5, 1);

            messages.Should().ContainSingle(message => message.Contains("[LabelFilterDiag] bad-range", StringComparison.Ordinal));
        }

        private static LabelMechanicResolutionService CreateMechanicResolutionService()
        {
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

        private static LabelDebugService CreateLabelDebugService(
            ClickSettings clickSettings,
            ClickItSettings? settings = null,
            List<string>? messages = null)
        {
            settings ??= new ClickItSettings();
            messages ??= [];

            return new LabelDebugService(
                settings,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)),
                gameController: null,
                createClickSettings: _ => clickSettings,
                shouldAllowWorldItemByMetadata: static (_, _, _, _) => true,
                mechanicResolutionService: CreateMechanicResolutionService(clickSettings),
                diagnostics: new LabelSelectionDiagnostics(24));
        }

        private static LabelMechanicResolutionService CreateMechanicResolutionService(ClickSettings clickSettings)
        {
            return new LabelMechanicResolutionService(
                gameController: null,
                createClickSettings: _ => clickSettings,
                getClassificationDependencies: () => MechanicClassifierDependenciesFactory.Create(
                    new WorldItemMetadataPolicy(),
                    new LabelInteractionRuleService(
                        new WorldItemMetadataPolicy(),
                        InventoryDomainFactory.Create(new InventoryDomainFactoryDependencies(
                            new WorldItemMetadataPolicy().GetWorldItemBaseName,
                            InventoryMetadataIdentifiers.StoneOfPassage)).InteractionPolicy)));
        }

        private static ClickSettings CreateClickSettings(
            int clickDistance = 50,
            bool clickSettlersOre = true,
            bool clickSettlersCopper = true,
            bool clickSettlersPetrifiedWood = false,
            bool clickSettlersBismuth = false,
            bool clickSettlersVerisium = false)
            => new()
            {
                ClickDistance = clickDistance,
                ClickSettlersOre = clickSettlersOre,
                ClickSettlersCopper = clickSettlersCopper,
                ClickSettlersPetrifiedWood = clickSettlersPetrifiedWood,
                ClickSettlersBismuth = clickSettlersBismuth,
                ClickSettlersVerisium = clickSettlersVerisium,
            };
    }
}