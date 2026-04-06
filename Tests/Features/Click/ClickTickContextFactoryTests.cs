namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ClickTickContextFactoryTests
    {
        [TestMethod]
        public void TryCreateRegularClickContext_ReturnsFalse_WhenUltimatumHandlerConsumesTick()
        {
            var factory = CreateFactory(tryHandleUltimatumPanelUi: static _ => true);

            bool created = factory.TryCreateRegularClickContext(out ClickTickContext context);

            created.Should().BeFalse();
            context.Should().Be(default(ClickTickContext));
        }

        [TestMethod]
        public void TryCreateRegularClickContext_ContinuesAndLogs_WhenUltimatumHandlerThrows()
        {
            var logs = new List<string>();
            var labels = new List<LabelOnGround>
            {
                (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround))
            };
            var factory = CreateFactory(
                getLabelsForRegularSelection: () => labels,
                tryHandleUltimatumPanelUi: static _ => throw new InvalidOperationException("boom"),
                debugLog: logs.Add);

            bool created = factory.TryCreateRegularClickContext(out ClickTickContext context);

            created.Should().BeTrue();
            context.AllLabels.Should().BeSameAs(labels);
            logs.Should().ContainSingle(message => message.Contains("Ultimatum UI handler failed", StringComparison.Ordinal)
                && message.Contains("boom", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TryCreateRegularClickContext_ReturnsFalse_AndPublishesDebug_WhenMovementSkillPostCastBlockActive()
        {
            ClickDebugSnapshot? snapshot = null;
            var runtimeState = new ClickRuntimeState
            {
                MovementSkillPostCastClickBlockUntilTimestampMs = Environment.TickCount64 + 10_000
            };
            var factory = CreateFactory(
                movementSkills: CreateMovementSkillCoordinator(runtimeState),
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: value => snapshot = value));

            bool created = factory.TryCreateRegularClickContext(out ClickTickContext context);

            created.Should().BeFalse();
            context.Should().Be(default(ClickTickContext));
            snapshot.Should().NotBeNull();
            snapshot!.Stage.Should().Be("MovementBlocked");
            snapshot.Notes.Should().Contain("timing window active");
        }

        [TestMethod]
        public void TryCreateRegularClickContext_ReturnsFalse_WhenPendingChestConfirmationConsumesTick()
        {
            var settings = new ClickItSettings();
            settings.PauseAfterOpeningBasicChests.Value = true;

            var chestState = new ChestLootSettlementState
            {
                PendingOpenConfirmationActive = true,
                PendingOpenMechanicId = MechanicIds.BasicChests
            };
            var chestLootSettlement = CreateChestLootSettlementTracker(settings, chestState);
            var factory = CreateFactory(chestLootSettlement: chestLootSettlement);

            bool created = factory.TryCreateRegularClickContext(out ClickTickContext context);

            created.Should().BeFalse();
            context.Should().Be(default(ClickTickContext));
            chestState.PendingOpenConfirmationActive.Should().BeFalse();
            chestState.IsWatcherActive.Should().BeTrue();
        }

        [TestMethod]
        public void TryCreateRegularClickContext_CreatesContext_WithRefreshedMechanicPriorityAndChestBlockState()
        {
            var settings = new ClickItSettings();
            settings.ClickShrines.Value = false;
            settings.MechanicPriorityDistancePenalty.Value = 37;

            var labels = new List<LabelOnGround>
            {
                (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround))
            };
            var snapshotProvider = new FakeMechanicPrioritySnapshotProvider();
            var chestState = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                InitialDelayUntilTimestampMs = Environment.TickCount64 + 1_000
            };
            var factory = CreateFactory(
                settings: settings,
                getWindowRectangle: static () => new RectangleF(10f, 20f, 300f, 200f),
                getCursorAbsolutePosition: static () => new Vector2(40f, 60f),
                getLabelsForRegularSelection: () => labels,
                groundItemsVisible: static () => true,
                chestLootSettlement: CreateChestLootSettlementTracker(settings, chestState),
                mechanicPriorityContextProvider: new MechanicPriorityContextProvider(settings, snapshotProvider));

            bool created = factory.TryCreateRegularClickContext(out ClickTickContext context);

            created.Should().BeTrue();
            context.WindowTopLeft.Should().Be(new Vector2(10f, 20f));
            context.CursorAbsolute.Should().Be(new Vector2(40f, 60f));
            context.AllLabels.Should().BeSameAs(labels);
            context.GroundItemsVisible.Should().BeTrue();
            context.NextShrine.Should().BeNull();
            context.IsPostChestLootSettleBlocking.Should().BeTrue();
            context.ChestLootSettleReason.Should().Contain("waiting ");
            context.MechanicPriorityContext.PriorityDistancePenalty.Should().Be(37);
            context.MechanicPriorityContext.PriorityIndexMap.Should().ContainKey("mechanic-a");
            context.MechanicPriorityContext.IgnoreDistanceSet.Should().Contain("ignore-a");
            context.MechanicPriorityContext.IgnoreDistanceWithinByMechanicId["mechanic-a"].Should().Be(12);
            snapshotProvider.RefreshCalls.Should().Be(1);
        }

        private static ClickTickContextFactory CreateFactory(
            ClickItSettings? settings = null,
            Func<RectangleF>? getWindowRectangle = null,
            Func<Vector2>? getCursorAbsolutePosition = null,
            Func<Vector2, bool>? tryHandleUltimatumPanelUi = null,
            Action<string>? debugLog = null,
            MovementSkillCoordinator? movementSkills = null,
            ChestLootSettlementTracker? chestLootSettlement = null,
            Func<IReadOnlyList<LabelOnGround>?>? getLabelsForRegularSelection = null,
            VisibleMechanicCoordinator? visibleMechanics = null,
            MechanicPriorityContextProvider? mechanicPriorityContextProvider = null,
            Func<bool>? groundItemsVisible = null,
            ClickDebugPublicationService? clickDebugPublisher = null)
        {
            ClickItSettings resolvedSettings = settings ?? new ClickItSettings();
            resolvedSettings.ClickShrines.Value = false;

            return new ClickTickContextFactory(new ClickTickContextFactoryDependencies(
                getWindowRectangle: getWindowRectangle ?? (static () => new RectangleF(0f, 0f, 100f, 100f)),
                getCursorAbsolutePosition: getCursorAbsolutePosition ?? (static () => new Vector2(5f, 6f)),
                tryHandleUltimatumPanelUi: tryHandleUltimatumPanelUi ?? (static _ => false),
                debugLog: debugLog ?? (static _ => { }),
                movementSkills: movementSkills ?? CreateMovementSkillCoordinator(),
                chestLootSettlement: chestLootSettlement ?? CreateChestLootSettlementTracker(resolvedSettings, new ChestLootSettlementState()),
                getLabelsForRegularSelection: getLabelsForRegularSelection ?? (static () => null),
                visibleMechanics: visibleMechanics ?? CreateVisibleMechanicCoordinator(resolvedSettings),
                mechanicPriorityContextProvider: mechanicPriorityContextProvider ?? new MechanicPriorityContextProvider(resolvedSettings, new FakeMechanicPrioritySnapshotProvider()),
                groundItemsVisible: groundItemsVisible ?? (static () => false),
                clickDebugPublisher: clickDebugPublisher ?? ClickTestDebugPublisherFactory.Create()));
        }

        private static MovementSkillCoordinator CreateMovementSkillCoordinator(ClickRuntimeState? runtimeState = null)
        {
            return new MovementSkillCoordinator(new MovementSkillCoordinatorDependencies(
                Settings: new ClickItSettings(),
                GameController: null!,
                RuntimeState: runtimeState ?? new ClickRuntimeState(),
                PerformanceMonitor: null!,
                GetRemainingOffscreenPathNodeCount: static () => 0,
                EnsureCursorInsideGameWindowForClick: static _ => true,
                PointIsInClickableArea: static (_, _) => true,
                DebugLog: static _ => { }));
        }

        private static ChestLootSettlementTracker CreateChestLootSettlementTracker(ClickItSettings settings, ChestLootSettlementState state)
        {
            return new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                Settings: settings,
                State: state,
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(static () => []),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService()));
        }

        private static VisibleMechanicCoordinator CreateVisibleMechanicCoordinator(ClickItSettings settings)
        {
            return new VisibleMechanicCoordinator(new VisibleMechanicCoordinatorDependencies(
                Settings: settings,
                GameController: null!,
                ShrineService: null!,
                LostShipmentTargets: null!,
                SettlersOreTargets: null!,
                PointIsInClickableArea: static (_, _) => false,
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(),
                StickyTargets: null!,
                PathfindingService: null!,
                DebugLog: static _ => { },
                HoldDebugTelemetryAfterSuccess: static _ => { },
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create()));
        }

        private sealed class FakeMechanicPrioritySnapshotProvider : IMechanicPrioritySnapshotProvider
        {
            public int RefreshCalls { get; private set; }

            public MechanicPrioritySnapshot Snapshot { get; private set; } = new(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

            public MechanicPrioritySnapshot Refresh(
                IReadOnlyList<string> mechanicPriorities,
                IReadOnlyCollection<string> ignoreDistance,
                IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
            {
                RefreshCalls++;
                Snapshot = new MechanicPrioritySnapshot(
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mechanic-a"] = 2
                    },
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "ignore-a"
                    },
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mechanic-a"] = 12
                    });
                return Snapshot;
            }
        }
    }
}