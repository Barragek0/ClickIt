namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ChestLootSettlementTrackerTests
    {
        [TestMethod]
        public void StartPostChestLootSettlementWatch_SeedsWatcherState_AndTiming()
        {
            var settings = new ClickItSettings();
            settings.PauseAfterOpeningBasicChests.Value = true;

            var state = new ChestLootSettlementState();
            var tracker = CreateTracker(settings, state);

            tracker.StartPostChestLootSettlementWatch(MechanicIds.BasicChests);

            state.IsWatcherActive.Should().BeTrue();
            state.InitialDelayUntilTimestampMs.Should().BeGreaterThan(0);
            state.NextPollTimestampMs.Should().Be(state.InitialDelayUntilTimestampMs);
            state.LastNewItemTimestampMs.Should().Be(state.InitialDelayUntilTimestampMs);
            state.PollIntervalMs.Should().Be(settings.PauseAfterOpeningBasicChestsPollIntervalMs.Value);
            state.QuietWindowMs.Should().Be(settings.PauseAfterOpeningBasicChestsQuietWindowMs.Value);
            state.KnownGroundItemAddresses.Should().BeEmpty();
        }

        [TestMethod]
        public void StartPostChestLootSettlementWatch_DoesNothing_WhenMechanicIsNotConfiguredToWait()
        {
            var settings = new ClickItSettings();
            settings.PauseAfterOpeningBasicChests.Value = false;

            var state = new ChestLootSettlementState();
            var tracker = CreateTracker(settings, state);

            tracker.StartPostChestLootSettlementWatch(MechanicIds.BasicChests);

            state.IsWatcherActive.Should().BeFalse();
            state.InitialDelayUntilTimestampMs.Should().Be(0);
            state.NextPollTimestampMs.Should().Be(0);
        }

        [TestMethod]
        public void MarkPendingChestOpenConfirmation_SetsPendingState_ForConfiguredBasicChestEvenWithoutLabel()
        {
            var settings = new ClickItSettings();
            settings.PauseAfterOpeningBasicChests.Value = true;

            var state = new ChestLootSettlementState();
            var tracker = CreateTracker(settings, state);

            tracker.MarkPendingChestOpenConfirmation(MechanicIds.BasicChests, chestLabel: null);

            state.PendingOpenConfirmationActive.Should().BeTrue();
            state.PendingOpenMechanicId.Should().Be(MechanicIds.BasicChests);
            state.PendingOpenItemAddress.Should().Be(0);
            state.PendingOpenLabelAddress.Should().Be(0);
            state.SourceGridValid.Should().BeFalse();
        }

        [TestMethod]
        public void ClearPendingChestOpenConfirmation_ResetsPendingState()
        {
            var settings = new ClickItSettings();
            var state = new ChestLootSettlementState
            {
                PendingOpenConfirmationActive = true,
                PendingOpenMechanicId = MechanicIds.BasicChests,
                PendingOpenItemAddress = 11,
                PendingOpenLabelAddress = 22
            };
            var tracker = CreateTracker(settings, state);

            tracker.ClearPendingChestOpenConfirmation();

            state.PendingOpenConfirmationActive.Should().BeFalse();
            state.PendingOpenMechanicId.Should().BeNull();
            state.PendingOpenItemAddress.Should().Be(0);
            state.PendingOpenLabelAddress.Should().Be(0);
        }

        [TestMethod]
        public void IsPostChestLootSettlementBlocking_ReturnsTrue_DuringInitialDelay()
        {
            var settings = new ClickItSettings();
            var state = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                InitialDelayUntilTimestampMs = 300,
                NextPollTimestampMs = 300,
                LastNewItemTimestampMs = 300,
                PollIntervalMs = 50,
                QuietWindowMs = 100
            };
            var tracker = CreateTracker(settings, state);

            bool blocking = tracker.IsPostChestLootSettlementBlocking(now: 200, out string reason);

            blocking.Should().BeTrue();
            reason.Should().Be("waiting 100ms before monitoring chest drops");
            state.IsWatcherActive.Should().BeTrue();
        }

        [TestMethod]
        public void IsPostChestLootSettlementBlocking_ClearsWatcher_WhenQuietWindowHasElapsed()
        {
            var settings = new ClickItSettings();
            var state = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                InitialDelayUntilTimestampMs = 100,
                NextPollTimestampMs = 100,
                LastNewItemTimestampMs = 200,
                PollIntervalMs = 50,
                QuietWindowMs = 100,
                SourceGridValid = true,
                SourceGrid = new Vector2(4f, 5f)
            };
            state.KnownGroundItemAddresses.Add(77);
            var tracker = CreateTracker(settings, state, snapshotProvider: static () => new HashSet<long>());

            bool blocking = tracker.IsPostChestLootSettlementBlocking(now: 400, out string reason);

            blocking.Should().BeFalse();
            reason.Should().BeEmpty();
            state.IsWatcherActive.Should().BeFalse();
            state.KnownGroundItemAddresses.Should().BeEmpty();
            state.SourceGridValid.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowMechanicInteractionDuringPostChestLootSettlement_ReturnsFalse_WhenBypassSettingDisabled()
        {
            var settings = new ClickItSettings();
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value = false;

            var state = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                SourceGridValid = true,
                SourceGrid = new Vector2(5f, 5f)
            };
            var tracker = CreateTracker(settings, state);

            bool allowed = tracker.ShouldAllowMechanicInteractionDuringPostChestLootSettlement("league-chests", entity: null, out string decision);

            allowed.Should().BeFalse();
            decision.Should().Be("setting-disabled");
        }

        [TestMethod]
        public void ShouldAllowMechanicInteractionDuringPostChestLootSettlement_ReturnsFalse_WhenSourceGridUnavailable()
        {
            var settings = new ClickItSettings();
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value = true;

            var state = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                SourceGridValid = false
            };
            var tracker = CreateTracker(settings, state);

            bool allowed = tracker.ShouldAllowMechanicInteractionDuringPostChestLootSettlement("league-chests", entity: null, out string decision);

            allowed.Should().BeFalse();
            decision.Should().Be("source-grid-unavailable");
        }

        [TestMethod]
        public void ShouldAllowMechanicInteractionDuringPostChestLootSettlement_ReturnsFalse_WhenMechanicIsIneligible()
        {
            var settings = new ClickItSettings();
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value = true;

            var state = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                SourceGridValid = true,
                SourceGrid = new Vector2(5f, 5f)
            };
            var tracker = CreateTracker(settings, state);

            bool allowed = tracker.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(null, entity: null, out string decision);

            allowed.Should().BeFalse();
            decision.Should().Be("mechanic-not-eligible");
        }

        [TestMethod]
        public void ShouldAllowMechanicInteractionDuringPostChestLootSettlement_ReturnsFalse_WhenCandidateGridUnavailable()
        {
            var settings = new ClickItSettings();
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value = true;

            var state = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                SourceGridValid = true,
                SourceGrid = new Vector2(5f, 5f)
            };
            var tracker = CreateTracker(settings, state);

            bool allowed = tracker.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(
                MechanicIds.LeagueChests,
                hasCandidateGrid: false,
                entityGridPos: default,
                out string decision);

            allowed.Should().BeFalse();
            decision.Should().Be("candidate-grid-unavailable");
        }

        [TestMethod]
        public void ShouldAllowMechanicInteractionDuringPostChestLootSettlement_ReturnsFalse_WhenWatcherInactive()
        {
            var settings = new ClickItSettings();
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value = true;
            var state = new ChestLootSettlementState();
            var tracker = CreateTracker(settings, state);

            bool allowed = tracker.ShouldAllowMechanicInteractionDuringPostChestLootSettlement("league-chests", entity: null, out string decision);

            allowed.Should().BeFalse();
            decision.Should().Be("watcher-inactive");
        }

        [TestMethod]
        public void ShouldAllowMechanicInteractionDuringPostChestLootSettlement_ReturnsTrue_WhenCandidateGridIsWithinDistance_ThroughBoundedSeam()
        {
            var settings = new ClickItSettings();
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value = true;
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance.Value = 10;

            var state = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                SourceGridValid = true,
                SourceGrid = new Vector2(0f, 0f)
            };
            var tracker = CreateTracker(settings, state);

            bool allowed = tracker.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(
                MechanicIds.LeagueChests,
                hasCandidateGrid: true,
                entityGridPos: new Vector2(6f, 8f),
                out string decision);

            allowed.Should().BeTrue();
            decision.Should().Contain("allowed");
            decision.Should().Contain("dist:10.0");
        }

        [TestMethod]
        public void ShouldAllowMechanicInteractionDuringPostChestLootSettlement_ReturnsFalse_WhenCandidateGridIsOutOfDistance_ThroughBoundedSeam()
        {
            var settings = new ClickItSettings();
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value = true;
            settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance.Value = 10;

            var state = new ChestLootSettlementState
            {
                IsWatcherActive = true,
                SourceGridValid = true,
                SourceGrid = new Vector2(0f, 0f)
            };
            var tracker = CreateTracker(settings, state);

            bool allowed = tracker.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(
                MechanicIds.LeagueChests,
                hasCandidateGrid: true,
                entityGridPos: new Vector2(9f, 12f),
                out string decision);

            allowed.Should().BeFalse();
            decision.Should().Contain("blocked");
            decision.Should().Contain("dist:15.0");
        }

        private static ChestLootSettlementTracker CreateTracker(
            ClickItSettings settings,
            ChestLootSettlementState state,
            Func<IReadOnlySet<long>>? snapshotProvider = null)
        {
            _ = snapshotProvider;
            return new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                Settings: settings,
                State: state,
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(static () => null),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService()));
        }
    }
}