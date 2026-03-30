using ClickIt.Definitions;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    internal readonly record struct ChestLootSettlementTrackerDependencies(
        ClickItSettings Settings,
        ChestLootSettlementState State,
        Func<IReadOnlySet<long>> CollectGroundLabelEntityAddresses,
        Action<string, string, string?> PublishClickFlowDebugStage,
        Func<LabelOnGround, string?, Vector2, IReadOnlyList<LabelOnGround>?, (bool Success, Vector2 ClickPos)> TryResolveLabelClickPosition,
        Func<Vector2, LabelOnGround?, bool, bool> PerformLabelClick);

    internal readonly struct ChestLootSettlementTiming
    {
        public ChestLootSettlementTiming(int initialDelayMs, int pollIntervalMs, int quietWindowMs)
        {
            InitialDelayMs = initialDelayMs;
            PollIntervalMs = pollIntervalMs;
            QuietWindowMs = quietWindowMs;
        }

        public int InitialDelayMs { get; }
        public int PollIntervalMs { get; }
        public int QuietWindowMs { get; }
    }

    internal readonly struct ChestLootSettlementTimingOptions
    {
        public ChestLootSettlementTimingOptions(ChestLootSettlementTiming basic, ChestLootSettlementTiming league)
        {
            Basic = basic;
            League = league;
        }

        public ChestLootSettlementTiming Basic { get; }
        public ChestLootSettlementTiming League { get; }
    }

    internal sealed class ChestLootSettlementTracker(ChestLootSettlementTrackerDependencies dependencies)
    {
        private readonly ChestLootSettlementTrackerDependencies _dependencies = dependencies;

        public void StartPostChestLootSettlementWatch(string? mechanicId)
        {
            ClickItSettings settings = _dependencies.Settings;
            ChestLootSettlementState state = _dependencies.State;

            if (!ClickService.ShouldWaitForChestLootSettlement(
                mechanicId,
                settings.PauseAfterOpeningBasicChests?.Value == true,
                settings.PauseAfterOpeningLeagueChests?.Value == true))
            {
                return;
            }

            ChestLootSettlementTiming timing = ClickService.ResolvePostChestLootSettlementTimingSettings(
                mechanicId,
                ResolvePostChestLootSettlementTimingOptions());

            long now = Environment.TickCount64;
            bool hadSourceGrid = state.SourceGridValid;
            Vector2 sourceGrid = state.SourceGrid;
            ClearPendingChestOpenConfirmation();
            ClearPostChestLootSettlementWatch();
            state.IsWatcherActive = true;
            state.InitialDelayUntilTimestampMs = now + timing.InitialDelayMs;
            state.NextPollTimestampMs = state.InitialDelayUntilTimestampMs;
            state.LastNewItemTimestampMs = state.InitialDelayUntilTimestampMs;
            state.PollIntervalMs = timing.PollIntervalMs;
            state.QuietWindowMs = timing.QuietWindowMs;
            state.SourceGridValid = hadSourceGrid;
            state.SourceGrid = sourceGrid;
            ClickService.SeedKnownGroundItemAddresses(state.KnownGroundItemAddresses, _dependencies.CollectGroundLabelEntityAddresses());
        }

        public bool TryHandlePendingChestOpenConfirmation(Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
            ChestLootSettlementState state = _dependencies.State;

            if (!state.PendingOpenConfirmationActive)
                return false;

            LabelOnGround? pendingChestLabel = ClickService.FindPendingChestLabel(allLabels, state.PendingOpenItemAddress, state.PendingOpenLabelAddress);
            bool chestLabelVisible = pendingChestLabel != null;
            if (ClickService.ShouldStartChestLootSettlementAfterClick(state.PendingOpenConfirmationActive, chestLabelVisible))
            {
                _dependencies.PublishClickFlowDebugStage("PostChestOpenDetected", "Chest label disappeared; starting loot settle watch", state.PendingOpenMechanicId);
                StartPostChestLootSettlementWatch(state.PendingOpenMechanicId);
                return true;
            }

            if (!ClickService.ShouldContinueChestOpenRetries(state.PendingOpenConfirmationActive, chestLabelVisible) || pendingChestLabel == null)
                return false;

            (bool resolvedClickPos, Vector2 clickPos) = _dependencies.TryResolveLabelClickPosition(
                pendingChestLabel,
                state.PendingOpenMechanicId,
                windowTopLeft,
                allLabels);
            if (!resolvedClickPos)
            {
                _dependencies.PublishClickFlowDebugStage("PostChestReclickResolveFailed", "Pending chest label visible but click point could not be resolved", state.PendingOpenMechanicId);
                return true;
            }

            bool clicked = _dependencies.PerformLabelClick(
                clickPos,
                pendingChestLabel,
                ClickService.ShouldForceUiHoverVerificationForLabel(pendingChestLabel));
            _dependencies.PublishClickFlowDebugStage(
                clicked ? "PostChestReclick" : "PostChestReclickRejected",
                clicked ? "Chest label still visible; reattempted chest click" : "Chest label still visible; chest reclick was rejected",
                state.PendingOpenMechanicId);
            return true;
        }

        public void MarkPendingChestOpenConfirmation(string? mechanicId, LabelOnGround? chestLabel)
        {
            ClickItSettings settings = _dependencies.Settings;
            ChestLootSettlementState state = _dependencies.State;

            if (!ClickService.ShouldWaitForChestLootSettlement(
                mechanicId,
                settings.PauseAfterOpeningBasicChests?.Value == true,
                settings.PauseAfterOpeningLeagueChests?.Value == true))
            {
                return;
            }

            ClearPendingChestOpenConfirmation();
            state.PendingOpenConfirmationActive = true;
            state.PendingOpenMechanicId = mechanicId;
            state.PendingOpenItemAddress = chestLabel?.ItemOnGround?.Address ?? 0;
            state.PendingOpenLabelAddress = chestLabel?.Label?.Address ?? 0;
            state.SourceGridValid = ClickService.TryGetEntityGridPosition(chestLabel?.ItemOnGround, out state.SourceGrid);
        }

        public void ClearPendingChestOpenConfirmation()
        {
            ChestLootSettlementState state = _dependencies.State;
            state.PendingOpenConfirmationActive = false;
            state.PendingOpenMechanicId = null;
            state.PendingOpenItemAddress = 0;
            state.PendingOpenLabelAddress = 0;
        }

        public bool IsPostChestLootSettlementBlocking(long now, out string reason)
        {
            ChestLootSettlementState state = _dependencies.State;

            reason = string.Empty;
            if (!state.IsWatcherActive)
                return false;

            if (now < state.InitialDelayUntilTimestampMs)
            {
                long initialDelayRemainingMs = state.InitialDelayUntilTimestampMs - now;
                reason = $"waiting {initialDelayRemainingMs}ms before monitoring chest drops";
                return true;
            }

            if (now >= state.NextPollTimestampMs)
            {
                bool hasNewGroundItems = ClickService.MergeNewGroundItemAddresses(
                    state.KnownGroundItemAddresses,
                    _dependencies.CollectGroundLabelEntityAddresses());
                if (hasNewGroundItems)
                {
                    state.LastNewItemTimestampMs = now;
                }

                state.NextPollTimestampMs = now + Math.Max(1, state.PollIntervalMs);
            }

            if (ClickService.IsChestLootSettlementQuietPeriodElapsed(
                now,
                state.LastNewItemTimestampMs,
                state.QuietWindowMs,
                out long quietWindowRemainingMs))
            {
                ClearPostChestLootSettlementWatch();
                return false;
            }

            reason = $"waiting for chest loot to settle ({quietWindowRemainingMs}ms quiet window remaining)";
            return true;
        }

        public void ClearPostChestLootSettlementWatch()
        {
            ChestLootSettlementState state = _dependencies.State;
            state.IsWatcherActive = false;
            state.InitialDelayUntilTimestampMs = 0;
            state.NextPollTimestampMs = 0;
            state.LastNewItemTimestampMs = 0;
            state.PollIntervalMs = 0;
            state.QuietWindowMs = 0;
            state.SourceGridValid = false;
            state.SourceGrid = default;
            state.KnownGroundItemAddresses.Clear();
        }

        public bool ShouldAllowMechanicInteractionDuringPostChestLootSettlement(string? mechanicId, Entity? entity)
            => ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out _);

        public bool ShouldAllowMechanicInteractionDuringPostChestLootSettlement(string? mechanicId, Entity? entity, out string decision)
        {
            ClickItSettings settings = _dependencies.Settings;
            ChestLootSettlementState state = _dependencies.State;

            decision = string.Empty;
            if (!state.IsWatcherActive)
            {
                decision = "watcher-inactive";
                return false;
            }
            if (settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle?.Value != true)
            {
                decision = "setting-disabled";
                return false;
            }
            if (!state.SourceGridValid)
            {
                decision = "source-grid-unavailable";
                return false;
            }
            if (!ClickService.IsMechanicEligibleForNearbyChestLootSettlementBypass(mechanicId))
            {
                decision = "mechanic-not-eligible";
                return false;
            }
            if (!ClickService.TryGetEntityGridPosition(entity, out Vector2 entityGridPos))
            {
                decision = "candidate-grid-unavailable";
                return false;
            }

            int maxDistance = Math.Max(0, settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance?.Value ?? 10);
            float distanceSq = CalculateDistanceSquared(state.SourceGrid, entityGridPos);
            float distance = MathF.Sqrt(distanceSq);
            bool allowed = ClickService.IsWithinNearbyChestLootSettlementBypassDistance(state.SourceGrid, entityGridPos, maxDistance);
            decision = $"{(allowed ? "allowed" : "blocked")}; mechanic:{mechanicId ?? "unknown"}; dist:{distance:0.0}; max:{maxDistance}; source:({state.SourceGrid.X:0.0},{state.SourceGrid.Y:0.0}); candidate:({entityGridPos.X:0.0},{entityGridPos.Y:0.0})";
            return allowed;
        }

        private ChestLootSettlementTimingOptions ResolvePostChestLootSettlementTimingOptions()
            => new(
                new ChestLootSettlementTiming(
                    _dependencies.Settings.PauseAfterOpeningBasicChestsInitialDelayMs?.Value ?? ClickService.PostChestLootSettleDefaultInitialDelayMs,
                    _dependencies.Settings.PauseAfterOpeningBasicChestsPollIntervalMs?.Value ?? ClickService.PostChestLootSettleDefaultPollIntervalMs,
                    _dependencies.Settings.PauseAfterOpeningBasicChestsQuietWindowMs?.Value ?? ClickService.PostChestLootSettleDefaultQuietWindowMs),
                new ChestLootSettlementTiming(
                    _dependencies.Settings.PauseAfterOpeningLeagueChestsInitialDelayMs?.Value ?? ClickService.PostChestLootSettleDefaultInitialDelayMs,
                    _dependencies.Settings.PauseAfterOpeningLeagueChestsPollIntervalMs?.Value ?? ClickService.PostChestLootSettleDefaultPollIntervalMs,
                    _dependencies.Settings.PauseAfterOpeningLeagueChestsQuietWindowMs?.Value ?? ClickService.PostChestLootSettleDefaultQuietWindowMs));

        private static float CalculateDistanceSquared(Vector2 left, Vector2 right)
        {
            float deltaX = left.X - right.X;
            float deltaY = left.Y - right.Y;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }
    }
}