namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ClickRuntimeEngineTests
    {
        [TestMethod]
        public void Run_StopsAfterTickStart_WhenUltimatumHandlerConsumesTick()
        {
            var snapshots = new List<ClickDebugSnapshot>();
            var visibleMechanics = new StubVisibleMechanicInteractionPort();
            var engine = CreateEngine(
                visibleMechanics: visibleMechanics,
                tryHandleUltimatumPanelUi: static _ => true,
                setLatestClickDebug: snapshots.Add);

            List<object?> yielded = Drain(engine.Run());

            yielded.Should().BeEmpty();
            visibleMechanics.HiddenFallbackCalls.Should().Be(0);
            visibleMechanics.SettlersClicks.Should().Be(0);
            snapshots.Select(static snapshot => snapshot.Stage).Should().Equal("TickStart");
        }

        private static ClickRuntimeEngine CreateEngine(
            ClickItSettings? settings = null,
            StubVisibleMechanicInteractionPort? visibleMechanics = null,
            Func<Vector2, bool>? tryHandleUltimatumPanelUi = null,
            MechanicPrioritySnapshot? prioritySnapshot = null,
            Action<ClickDebugSnapshot>? setLatestClickDebug = null)
        {
            ClickItSettings resolvedSettings = settings ?? new ClickItSettings();
            resolvedSettings.WalkTowardOffscreenLabels.Value = false;

            var debugPublisher = ClickTestDebugPublisherFactory.Create(
                shouldCaptureClickDebug: static () => true,
                setLatestClickDebug: setLatestClickDebug ?? (static _ => { }));
            StubVisibleMechanicInteractionPort resolvedVisibleMechanics = visibleMechanics ?? new StubVisibleMechanicInteractionPort();
            var tickContextFactory = new ClickTickContextFactory(new ClickTickContextFactoryDependencies(
                getWindowRectangle: static () => new RectangleF(0f, 0f, 100f, 100f),
                getCursorAbsolutePosition: static () => Vector2.Zero,
                tryHandleUltimatumPanelUi: tryHandleUltimatumPanelUi ?? (static _ => false),
                debugLog: static _ => { },
                movementSkills: CreateMovementSkillCoordinator(),
                chestLootSettlement: CreateChestLootSettlementTracker(),
                getLabelsForRegularSelection: static () => null,
                visibleMechanics: resolvedVisibleMechanics,
                mechanicPriorityContextProvider: new MechanicPriorityContextProvider(
                    resolvedSettings,
                    new FixedMechanicPrioritySnapshotProvider(prioritySnapshot ?? new MechanicPrioritySnapshot(
                        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)))),
                groundItemsVisible: static () => false,
                clickDebugPublisher: debugPublisher));

            return new ClickRuntimeEngine(new ClickRuntimeEngineDependencies(
                TickContextFactory: tickContextFactory,
                AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(resolvedSettings),
                ClickDebugPublisher: debugPublisher,
                Settings: resolvedSettings,
                LabelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                VisibleMechanics: resolvedVisibleMechanics,
                LabelSelection: null!,
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(),
                ShouldCaptureClickDebug: static () => true,
                PathfindingService: null!,
                PathfindingLabelSuppression: null!,
                ChestLootSettlement: CreateChestLootSettlementTracker(),
                OffscreenPathing: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { },
                DebugLog: static _ => { },
                InputHandler: new InputHandler(resolvedSettings)));
        }

        private static MovementSkillCoordinator CreateMovementSkillCoordinator()
        {
            return new MovementSkillCoordinator(new MovementSkillCoordinatorDependencies(
                Settings: new ClickItSettings(),
                GameController: null!,
                RuntimeState: new ClickRuntimeState(),
                PerformanceMonitor: null!,
                GetRemainingOffscreenPathNodeCount: static () => 0,
                EnsureCursorInsideGameWindowForClick: static _ => true,
                PointIsInClickableArea: static (_, _) => true,
                DebugLog: static _ => { }));
        }

        private static ChestLootSettlementTracker CreateChestLootSettlementTracker()
        {
            return new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                Settings: new ClickItSettings(),
                State: new ChestLootSettlementState(),
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(static () => []),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService()));
        }

        private static List<object?> Drain(IEnumerator enumerator)
        {
            var yielded = new List<object?>();
            while (enumerator.MoveNext())
            {
                yielded.Add(enumerator.Current);
            }

            return yielded;
        }

        private sealed class FixedMechanicPrioritySnapshotProvider(MechanicPrioritySnapshot snapshot) : IMechanicPrioritySnapshotProvider
        {
            public MechanicPrioritySnapshot Snapshot { get; private set; } = snapshot;

            public MechanicPrioritySnapshot Refresh(
                IReadOnlyList<string> mechanicPriorities,
                IReadOnlyCollection<string> ignoreDistance,
                IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
            {
                return Snapshot;
            }
        }

        private sealed class StubVisibleMechanicInteractionPort(
            LostShipmentCandidate? hiddenLostShipment = null,
            SettlersOreCandidate? hiddenSettlers = null) : IVisibleMechanicManualInteractionPort
        {
            public int HiddenFallbackCalls { get; private set; }
            public int SettlersClicks { get; private set; }
            public int LostShipmentClicks { get; private set; }

            public Entity? ResolveNextShrineCandidate()
                => null;

            public bool HasClickableShrine()
                => false;

            public void ResolveVisibleMechanicCandidates(
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate,
                IReadOnlyList<LabelOnGround>? labelsOverride = null)
            {
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
                => (hiddenLostShipment.HasValue, hiddenSettlers.HasValue);

            public bool TryClickSettlersOre(SettlersOreCandidate candidate)
            {
                SettlersClicks++;
                return true;
            }

            public void TryClickLostShipment(LostShipmentCandidate candidate)
            {
                LostShipmentClicks++;
            }

            public void TryClickShrine(Entity shrine)
            {
            }

            public void HandleSuccessfulMechanicEntityClick(Entity? entity)
            {
            }

            public void HandleSuccessfulShrineClick(Entity? shrine)
            {
            }
        }
    }
}