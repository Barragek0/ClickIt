namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class CandidateAcquisitionEngineTests
    {
        [TestMethod]
        public void Collect_UsesHiddenFallbackSelection_WhenGroundItemsAreHidden()
        {
            LostShipmentCandidate hiddenLostShipment = CreateLostShipmentCandidate(new Vector2(11f, 22f));
            SettlersOreCandidate hiddenSettlers = CreateSettlersCandidate(MechanicIds.SettlersCopper);
            var visibleMechanics = new StubVisibleMechanicSelectionSource(
                visibleLostShipment: null,
                visibleSettlers: null,
                hiddenLostShipment: hiddenLostShipment,
                hiddenSettlers: hiddenSettlers);
            ClickDebugSnapshot? latestSnapshot = null;
            CandidateAcquisitionEngine engine = CreateEngine(
                visibleMechanics: visibleMechanics,
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshot => latestSnapshot = snapshot));

            ClickCandidates candidates = engine.Collect(new ClickTickContext(
                WindowTopLeft: default,
                CursorAbsolute: default,
                Now: 100,
                IsPostChestLootSettleBlocking: false,
                ChestLootSettleReason: string.Empty,
                AllLabels: null,
                NextShrine: null,
                MechanicPriorityContext: default,
                GroundItemsVisible: false));

            visibleMechanics.HiddenFallbackCalls.Should().Be(1);
            visibleMechanics.VisibleCandidateCalls.Should().Be(0);
            candidates.LostShipment.Should().NotBeNull();
            candidates.LostShipment!.Value.ClickPosition.Should().Be(new Vector2(11f, 22f));
            candidates.SettlersOre.Should().NotBeNull();
            candidates.SettlersOre!.Value.MechanicId.Should().Be(MechanicIds.SettlersCopper);
            candidates.NextLabel.Should().BeNull();
            candidates.NextLabelMechanicId.Should().BeNull();
            latestSnapshot.Should().NotBeNull();
            latestSnapshot!.Stage.Should().Be("GroundItemsHidden");
        }

        [TestMethod]
        public void Collect_UsesVisibleMechanicSelection_WhenGroundItemsAreVisible()
        {
            LostShipmentCandidate visibleLostShipment = CreateLostShipmentCandidate(new Vector2(15f, 25f));
            SettlersOreCandidate visibleSettlers = CreateSettlersCandidate(MechanicIds.SettlersVerisium);
            var visibleMechanics = new StubVisibleMechanicSelectionSource(
                visibleLostShipment: visibleLostShipment,
                visibleSettlers: visibleSettlers);
            CandidateAcquisitionEngine engine = CreateEngine(visibleMechanics: visibleMechanics);

            ClickCandidates candidates = engine.Collect(new ClickTickContext(
                WindowTopLeft: default,
                CursorAbsolute: default,
                Now: 100,
                IsPostChestLootSettleBlocking: false,
                ChestLootSettleReason: string.Empty,
                AllLabels: [],
                NextShrine: null,
                MechanicPriorityContext: default,
                GroundItemsVisible: true));

            visibleMechanics.HiddenFallbackCalls.Should().Be(0);
            visibleMechanics.VisibleCandidateCalls.Should().Be(1);
            candidates.LostShipment.Should().NotBeNull();
            candidates.LostShipment!.Value.ClickPosition.Should().Be(new Vector2(15f, 25f));
            candidates.SettlersOre.Should().NotBeNull();
            candidates.SettlersOre!.Value.MechanicId.Should().Be(MechanicIds.SettlersVerisium);
            candidates.NextLabel.Should().BeNull();
            candidates.NextLabelMechanicId.Should().BeNull();
        }

        private static CandidateAcquisitionEngine CreateEngine(
            ClickItSettings? settings = null,
            ILabelInteractionPort? labelInteractionPort = null,
            IVisibleMechanicQueryPort? visibleMechanics = null,
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

        private static LostShipmentCandidate CreateLostShipmentCandidate(Vector2 clickPosition)
        {
            object boxed = default(LostShipmentCandidate);
            SetStructMember(boxed, "Entity", null!);
            SetStructMember(boxed, "ClickPosition", clickPosition);
            SetStructMember(boxed, "Distance", 0f);
            return (LostShipmentCandidate)boxed;
        }

        private static SettlersOreCandidate CreateSettlersCandidate(string mechanicId)
        {
            object boxed = default(SettlersOreCandidate);
            SetStructMember(boxed, "Entity", null!);
            SetStructMember(boxed, "ClickPosition", new Vector2(10f, 20f));
            SetStructMember(boxed, "MechanicId", mechanicId);
            SetStructMember(boxed, "EntityPath", MechanicIds.SettlersVerisiumMarker);
            SetStructMember(boxed, "WorldScreenRaw", new Vector2(15f, 25f));
            SetStructMember(boxed, "WorldScreenAbsolute", new Vector2(115f, 225f));
            SetStructMember(boxed, "Distance", 0f);
            return (SettlersOreCandidate)boxed;
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
            LostShipmentCandidate? visibleLostShipment = null,
            SettlersOreCandidate? visibleSettlers = null,
            LostShipmentCandidate? hiddenLostShipment = null,
            SettlersOreCandidate? hiddenSettlers = null) : IVisibleMechanicQueryPort
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
                lostShipmentCandidate = visibleLostShipment;
                settlersOreCandidate = visibleSettlers;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                HiddenFallbackCalls++;
                lostShipmentCandidate = hiddenLostShipment;
                settlersOreCandidate = hiddenSettlers;
            }
        }

        private static void SetStructMember(object instance, string memberName, object value)
        {
            Type? currentType = instance.GetType();
            while (currentType != null)
            {
                FieldInfo? backingField = currentType.GetField($"<{memberName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField($"_{char.ToLowerInvariant(memberName[0])}{memberName[1..]}", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (backingField != null)
                {
                    backingField.SetValue(instance, value);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Unable to set member '{memberName}' on {instance.GetType().FullName}.");
        }
    }
}