namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class CandidateAcquisitionEngineTests
    {
        private static CandidateAcquisitionEngine CreateEngine(
            ClickItSettings? settings = null,
            ILabelInteractionPort? labelInteractionPort = null,
            IVisibleMechanicSelectionSource? visibleMechanics = null,
            LabelSelectionCoordinator? labelSelection = null,
            ClickDebugPublicationService? clickDebugPublisher = null)
        {
            ClickItSettings resolvedSettings = settings ?? new ClickItSettings();
            ILabelInteractionPort resolvedPort = labelInteractionPort ?? new FakeLabelInteractionPort();

            return new CandidateAcquisitionEngine(new CandidateAcquisitionEngineDependencies(
                Settings: resolvedSettings,
                LabelInteractionPort: resolvedPort,
                VisibleMechanics: visibleMechanics ?? new StubVisibleMechanicSelectionSource(),
                LabelSelection: labelSelection ?? CreateLabelSelectionCoordinator(resolvedSettings, resolvedPort),
                ClickDebugPublisher: clickDebugPublisher ?? ClickTestDebugPublisherFactory.Create(),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(labelInteractionPort: resolvedPort),
                ShouldCaptureClickDebug: static () => false));
        }

        private static LabelSelectionCoordinator CreateLabelSelectionCoordinator(ClickItSettings settings, ILabelInteractionPort labelInteractionPort)
        {
            GameController gameController = ExileCoreOpaqueFactory.CreateOpaqueGameController();
            var scanEngine = new LabelSelectionScanEngine(new LabelSelectionScanEngineDependencies(
                gameController,
                labelInteractionPort,
                new LabelClickPointResolver(settings),
                ShouldSuppressLeverClick: static _ => false,
                ShouldSuppressInactiveUltimatumLabel: static _ => false,
                ClickTestServiceFactory.CreateLabelInteractionService(gameController: gameController, labelInteractionPort: labelInteractionPort),
                new MechanicPriorityContextProvider(settings, new MechanicPrioritySnapshotService()),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
                DebugLog: static _ => { }));

            return new LabelSelectionCoordinator(new LabelSelectionCoordinatorDependencies(
                GameController: gameController,
                ScanEngine: scanEngine,
                ManualCursorLabelSelector: null!,
                ManualCursorVisibleMechanicSelector: null!,
                SpecialLabelInteractionHandler: null!,
                ManualCursorLabelInteractionHandler: null!));
        }

        private sealed class FakeLabelInteractionPort(
            Func<IReadOnlyList<LabelOnGround>?, int, int, LabelOnGround?>? getNextLabelToClick = null,
            Func<LabelOnGround?, string?>? getMechanicIdForLabel = null) : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => getMechanicIdForLabel?.Invoke(label);

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => getNextLabelToClick?.Invoke(allLabels, startIndex, maxCount);

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }

        private sealed class StubVisibleMechanicSelectionSource(
            LostShipmentCandidate? hiddenLostShipment = null,
            SettlersOreCandidate? hiddenSettlers = null) : IVisibleMechanicSelectionSource
        {
            public int HiddenFallbackCalls { get; private set; }
            public int VisibleCandidateCalls { get; private set; }

            public Entity? ResolveNextShrineCandidate()
                => null;

            public bool HasClickableShrine()
                => false;

            public void ResolveVisibleMechanicCandidates(
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate,
                IReadOnlyList<LabelOnGround>? labelsOverride = null)
            {
                VisibleCandidateCalls++;
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                HiddenFallbackCalls++;
                lostShipmentCandidate = hiddenLostShipment;
                settlersOreCandidate = hiddenSettlers;
            }

            public (LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers) GetVisibleMechanicCandidates()
                => (null, null);

            public (bool HasLostShipment, bool HasSettlers) GetVisibleMechanicAvailability()
                => (false, false);
        }
    }
}