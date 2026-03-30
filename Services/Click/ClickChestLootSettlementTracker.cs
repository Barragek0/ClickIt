using ClickIt.Definitions;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private sealed class ChestLootSettlementTracker(ClickService owner)
        {
            public void StartPostChestLootSettlementWatch(string? mechanicId)
            {
                if (!ShouldWaitForChestLootSettlement(
                    mechanicId,
                    owner.settings.PauseAfterOpeningBasicChests?.Value == true,
                    owner.settings.PauseAfterOpeningLeagueChests?.Value == true))
                {
                    return;
                }

                ChestLootSettlementTiming timing = ResolvePostChestLootSettlementTimingSettings(
                    mechanicId,
                    ResolvePostChestLootSettlementTimingOptions());

                long now = Environment.TickCount64;
                bool hadSourceGrid = owner._postChestInteractionSourceGridValid;
                Vector2 sourceGrid = owner._postChestInteractionSourceGrid;
                ClearPendingChestOpenConfirmation();
                ClearPostChestLootSettlementWatch();
                owner._postChestLootSettleWatcherActive = true;
                owner._postChestLootSettleInitialDelayUntilTimestampMs = now + timing.InitialDelayMs;
                owner._postChestLootSettleNextPollTimestampMs = owner._postChestLootSettleInitialDelayUntilTimestampMs;
                owner._postChestLootSettleLastNewItemTimestampMs = owner._postChestLootSettleInitialDelayUntilTimestampMs;
                owner._postChestLootSettlePollIntervalMs = timing.PollIntervalMs;
                owner._postChestLootSettleQuietWindowMs = timing.QuietWindowMs;
                owner._postChestInteractionSourceGridValid = hadSourceGrid;
                owner._postChestInteractionSourceGrid = sourceGrid;
                SeedKnownGroundItemAddresses(owner._postChestLootSettleKnownGroundItemAddresses, owner.CollectGroundLabelEntityAddresses());
            }

            public bool TryHandlePendingChestOpenConfirmation(Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
            {
                if (!owner._pendingChestOpenConfirmationActive)
                    return false;

                LabelOnGround? pendingChestLabel = FindPendingChestLabel(allLabels, owner._pendingChestOpenItemAddress, owner._pendingChestOpenLabelAddress);
                bool chestLabelVisible = pendingChestLabel != null;
                if (ShouldStartChestLootSettlementAfterClick(owner._pendingChestOpenConfirmationActive, chestLabelVisible))
                {
                    owner.PublishClickFlowDebugStage("PostChestOpenDetected", "Chest label disappeared; starting loot settle watch", owner._pendingChestOpenMechanicId);
                    StartPostChestLootSettlementWatch(owner._pendingChestOpenMechanicId);
                    return true;
                }

                if (!ShouldContinueChestOpenRetries(owner._pendingChestOpenConfirmationActive, chestLabelVisible) || pendingChestLabel == null)
                    return false;

                if (!owner.TryResolveLabelClickPosition(
                    pendingChestLabel,
                    owner._pendingChestOpenMechanicId,
                    windowTopLeft,
                    allLabels,
                    out Vector2 clickPos))
                {
                    owner.PublishClickFlowDebugStage("PostChestReclickResolveFailed", "Pending chest label visible but click point could not be resolved", owner._pendingChestOpenMechanicId);
                    return true;
                }

                bool clicked = owner.PerformLabelClick(clickPos, pendingChestLabel.Label, owner.gameController, ShouldForceUiHoverVerificationForLabel(pendingChestLabel));
                owner.PublishClickFlowDebugStage(
                    clicked ? "PostChestReclick" : "PostChestReclickRejected",
                    clicked ? "Chest label still visible; reattempted chest click" : "Chest label still visible; chest reclick was rejected",
                    owner._pendingChestOpenMechanicId);
                return true;
            }

            public void MarkPendingChestOpenConfirmation(string? mechanicId, LabelOnGround? chestLabel)
            {
                if (!ShouldWaitForChestLootSettlement(
                    mechanicId,
                    owner.settings.PauseAfterOpeningBasicChests?.Value == true,
                    owner.settings.PauseAfterOpeningLeagueChests?.Value == true))
                {
                    return;
                }

                ClearPendingChestOpenConfirmation();
                owner._pendingChestOpenConfirmationActive = true;
                owner._pendingChestOpenMechanicId = mechanicId;
                owner._pendingChestOpenItemAddress = chestLabel?.ItemOnGround?.Address ?? 0;
                owner._pendingChestOpenLabelAddress = chestLabel?.Label?.Address ?? 0;
                owner._postChestInteractionSourceGridValid = TryGetEntityGridPosition(chestLabel?.ItemOnGround, out owner._postChestInteractionSourceGrid);
            }

            public void ClearPendingChestOpenConfirmation()
            {
                owner._pendingChestOpenConfirmationActive = false;
                owner._pendingChestOpenMechanicId = null;
                owner._pendingChestOpenItemAddress = 0;
                owner._pendingChestOpenLabelAddress = 0;
            }

            public bool IsPostChestLootSettlementBlocking(long now, out string reason)
            {
                reason = string.Empty;
                if (!owner._postChestLootSettleWatcherActive)
                    return false;

                if (now < owner._postChestLootSettleInitialDelayUntilTimestampMs)
                {
                    long initialDelayRemainingMs = owner._postChestLootSettleInitialDelayUntilTimestampMs - now;
                    reason = $"waiting {initialDelayRemainingMs}ms before monitoring chest drops";
                    return true;
                }

                if (now >= owner._postChestLootSettleNextPollTimestampMs)
                {
                    bool hasNewGroundItems = MergeNewGroundItemAddresses(
                        owner._postChestLootSettleKnownGroundItemAddresses,
                        owner.CollectGroundLabelEntityAddresses());
                    if (hasNewGroundItems)
                    {
                        owner._postChestLootSettleLastNewItemTimestampMs = now;
                    }

                    owner._postChestLootSettleNextPollTimestampMs = now + Math.Max(1, owner._postChestLootSettlePollIntervalMs);
                }

                if (IsChestLootSettlementQuietPeriodElapsed(
                    now,
                    owner._postChestLootSettleLastNewItemTimestampMs,
                    owner._postChestLootSettleQuietWindowMs,
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
                owner._postChestLootSettleWatcherActive = false;
                owner._postChestLootSettleInitialDelayUntilTimestampMs = 0;
                owner._postChestLootSettleNextPollTimestampMs = 0;
                owner._postChestLootSettleLastNewItemTimestampMs = 0;
                owner._postChestLootSettlePollIntervalMs = 0;
                owner._postChestLootSettleQuietWindowMs = 0;
                owner._postChestInteractionSourceGridValid = false;
                owner._postChestInteractionSourceGrid = default;
                owner._postChestLootSettleKnownGroundItemAddresses.Clear();
            }

            public bool ShouldAllowMechanicInteractionDuringPostChestLootSettlement(string? mechanicId, Entity? entity)
                => ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out _);

            public bool ShouldAllowMechanicInteractionDuringPostChestLootSettlement(string? mechanicId, Entity? entity, out string decision)
            {
                decision = string.Empty;
                if (!owner._postChestLootSettleWatcherActive)
                {
                    decision = "watcher-inactive";
                    return false;
                }
                if (owner.settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle?.Value != true)
                {
                    decision = "setting-disabled";
                    return false;
                }
                if (!owner._postChestInteractionSourceGridValid)
                {
                    decision = "source-grid-unavailable";
                    return false;
                }
                if (!IsMechanicEligibleForNearbyChestLootSettlementBypass(mechanicId))
                {
                    decision = "mechanic-not-eligible";
                    return false;
                }
                if (!TryGetEntityGridPosition(entity, out Vector2 entityGridPos))
                {
                    decision = "candidate-grid-unavailable";
                    return false;
                }

                int maxDistance = Math.Max(0, owner.settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance?.Value ?? 10);
                float distanceSq = GetDistanceSquared(owner._postChestInteractionSourceGrid, entityGridPos);
                float distance = MathF.Sqrt(distanceSq);
                bool allowed = IsWithinNearbyChestLootSettlementBypassDistance(owner._postChestInteractionSourceGrid, entityGridPos, maxDistance);
                decision = $"{(allowed ? "allowed" : "blocked")}; mechanic:{mechanicId ?? "unknown"}; dist:{distance:0.0}; max:{maxDistance}; source:({owner._postChestInteractionSourceGrid.X:0.0},{owner._postChestInteractionSourceGrid.Y:0.0}); candidate:({entityGridPos.X:0.0},{entityGridPos.Y:0.0})";
                return allowed;
            }

            private ChestLootSettlementTimingOptions ResolvePostChestLootSettlementTimingOptions()
                => new(
                    new ChestLootSettlementTiming(
                        owner.settings.PauseAfterOpeningBasicChestsInitialDelayMs?.Value ?? PostChestLootSettleDefaultInitialDelayMs,
                        owner.settings.PauseAfterOpeningBasicChestsPollIntervalMs?.Value ?? PostChestLootSettleDefaultPollIntervalMs,
                        owner.settings.PauseAfterOpeningBasicChestsQuietWindowMs?.Value ?? PostChestLootSettleDefaultQuietWindowMs),
                    new ChestLootSettlementTiming(
                        owner.settings.PauseAfterOpeningLeagueChestsInitialDelayMs?.Value ?? PostChestLootSettleDefaultInitialDelayMs,
                        owner.settings.PauseAfterOpeningLeagueChestsPollIntervalMs?.Value ?? PostChestLootSettleDefaultPollIntervalMs,
                        owner.settings.PauseAfterOpeningLeagueChestsQuietWindowMs?.Value ?? PostChestLootSettleDefaultQuietWindowMs));
        }
    }
}