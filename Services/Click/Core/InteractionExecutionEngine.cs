using ClickIt.Definitions;
using ClickIt.Services.Click.Runtime;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    internal sealed class InteractionExecutionEngine(ClickRuntimeEngine owner)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = owner.Dependencies;

        public ExecutionResult Execute(ClickTickContext context, ClickCandidates candidates, DecisionResult decision)
        {
            return decision.GroundItemsVisible
                ? ExecuteVisible(context, candidates, decision)
                : ExecuteHidden(context, candidates, decision);
        }

        private bool IsBlockedByPostChestLootSettlement(ClickTickContext context, string? mechanicId, Entity? entity)
        {
            if (!context.IsPostChestLootSettleBlocking)
                return false;

            if (_dependencies.ChestLootSettlement.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out string bypassDecision))
                return false;

            _dependencies.PublishClickFlowDebugStage(
                "PostChestLootSettleBlocked",
                $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecision}",
                mechanicId);
            return true;
        }

        private bool TryExecuteSettlersHidden(ClickTickContext context, SettlersOreCandidate candidate)
        {
            if (!IsBlockedByPostChestLootSettlement(context, candidate.MechanicId, candidate.Entity)
                && _dependencies.VisibleMechanics.TryClickSettlersOre(candidate))
            {
                _dependencies.PublishClickFlowDebugStage("HiddenSettlersFallback", "Using hidden settlers candidate", candidate.MechanicId);
                return true;
            }

            _dependencies.PublishClickFlowDebugStage("HiddenSettlersFallbackSkipped", "Hidden settlers candidate was not targetable/valid at click time", candidate.MechanicId);
            return false;
        }

        private bool TryExecuteSettlersVisible(ClickTickContext context, SettlersOreCandidate candidate)
        {
            return !IsBlockedByPostChestLootSettlement(context, candidate.MechanicId, candidate.Entity)
                && _dependencies.VisibleMechanics.TryClickSettlersOre(candidate);
        }

        private bool TryExecuteLostShipment(ClickTickContext context, LostShipmentCandidate candidate)
        {
            if (IsBlockedByPostChestLootSettlement(context, MechanicIds.LostShipment, candidate.Entity))
                return false;

            _dependencies.VisibleMechanics.TryClickLostShipment(candidate);
            return true;
        }

        private bool TryExecuteShrine(ClickTickContext context)
        {
            if (context.NextShrine == null)
                return false;
            if (IsBlockedByPostChestLootSettlement(context, MechanicIds.Shrines, context.NextShrine))
                return false;

            _dependencies.VisibleMechanics.TryClickShrine(context.NextShrine);
            return true;
        }

        private static ExecutionResult StopExecution()
        {
            return new ExecutionResult(false);
        }

        private ExecutionResult ExecuteHidden(ClickTickContext context, ClickCandidates candidates, DecisionResult decision)
        {
            if (decision.TrySettlers && candidates.SettlersOre.HasValue)
            {
                if (TryExecuteSettlersHidden(context, candidates.SettlersOre.Value))
                    return StopExecution();
            }

            if (decision.TryLostShipment && candidates.LostShipment.HasValue)
            {
                if (TryExecuteLostShipment(context, candidates.LostShipment.Value))
                    return StopExecution();
            }

            if (decision.TryShrine && context.NextShrine != null)
            {
                if (TryExecuteShrine(context))
                    return StopExecution();
            }

            if (context.IsPostChestLootSettleBlocking)
            {
                string chestLootSettleReason = context.ChestLootSettleReason;
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                _dependencies.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason, null);
                return StopExecution();
            }

            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
            {
                _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget();
            }

            _dependencies.DebugLog("[ProcessRegularClick] Ground items not visible, breaking");
            _dependencies.PublishClickFlowDebugStage("GroundItemsHiddenExit", "No clickable hidden fallback selected", null);
            return StopExecution();
        }

        private ExecutionResult ExecuteVisible(ClickTickContext context, ClickCandidates candidates, DecisionResult decision)
        {
            if (decision.TrySettlers && candidates.SettlersOre.HasValue)
            {
                if (TryExecuteSettlersVisible(context, candidates.SettlersOre.Value))
                    return StopExecution();
            }

            if (decision.TryLostShipment && candidates.LostShipment.HasValue)
            {
                if (TryExecuteLostShipment(context, candidates.LostShipment.Value))
                    return StopExecution();
            }

            if (decision.TryShrine && context.NextShrine != null)
            {
                if (TryExecuteShrine(context))
                    return StopExecution();
            }

            if (candidates.NextLabel == null)
            {
                return HandleNoVisibleLabel(context);
            }

            return HandleVisibleLabel(context, candidates);
        }

        private ExecutionResult HandleNoVisibleLabel(ClickTickContext context)
        {
            if (context.IsPostChestLootSettleBlocking)
            {
                string chestLootSettleReason = context.ChestLootSettleReason;
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                _dependencies.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason, null);
                return StopExecution();
            }

            _dependencies.LabelFilterService.LogSelectionDiagnostics(context.AllLabels, 0, context.AllLabels?.Count ?? 0);
            if (_dependencies.ShouldCaptureClickDebug())
            {
                _dependencies.PublishClickFlowDebugStage("NoLabelCandidate", _dependencies.BuildNoLabelDebugSummary(context.AllLabels), null);
            }

            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value
                && _dependencies.OffscreenPathing.TryHandleStickyOffscreenTarget(context.WindowTopLeft, context.AllLabels))
            {
                return StopExecution();
            }

            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
            {
                _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget();
            }

            _dependencies.DebugLog("[ProcessRegularClick] No label to click found, breaking");
            _dependencies.PublishClickFlowDebugStage("NoLabelExit", "No label click attempted", null);
            return StopExecution();
        }

        private ExecutionResult HandleVisibleLabel(ClickTickContext context, ClickCandidates candidates)
        {
            LabelOnGround nextLabel = candidates.NextLabel!;

            if (IsBlockedByPostChestLootSettlement(context, candidates.NextLabelMechanicId, nextLabel.ItemOnGround))
            {
                string chestLootSettleReason = context.ChestLootSettleReason;
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                return StopExecution();
            }

            if (_dependencies.LabelSelection.ShouldSkipOrHandleSpecialLabel(nextLabel, context.WindowTopLeft))
            {
                _dependencies.PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", candidates.NextLabelMechanicId);
                return StopExecution();
            }

            (bool resolved, Vector2 clickPos) = _dependencies.TryResolveLabelClickPosition(
                nextLabel,
                candidates.NextLabelMechanicId,
                context.WindowTopLeft,
                context.AllLabels);
            if (!resolved)
            {
                return HandleVisibleLabelResolveFailure(context, candidates, nextLabel);
            }

            _dependencies.PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", candidates.NextLabelMechanicId);

            _dependencies.PublishLabelClickDebug(
                "LabelCandidate",
                candidates.NextLabelMechanicId,
                nextLabel,
                clickPos,
                true,
                "Settlers label candidate selected from ItemsOnGroundLabelsVisible");

            bool clicked = _dependencies.ExecuteVisibleLabelInteraction(clickPos, nextLabel, candidates.NextLabelMechanicId);

            _dependencies.PublishLabelClickDebug(
                clicked ? "ClickSuccess" : "ClickFailed",
                candidates.NextLabelMechanicId,
                nextLabel,
                clickPos,
                clicked,
                clicked ? "Settlers click completed via label pipeline" : "Settlers click attempt failed via label pipeline");

            _dependencies.PublishClickFlowDebugStage(clicked ? "ClickExecuted" : "ClickRejected", clicked ? "Input click executed" : "Input click rejected", candidates.NextLabelMechanicId);

            if (clicked)
            {
                string mechanicDisplay = string.IsNullOrWhiteSpace(candidates.NextLabelMechanicId)
                    ? "visible-label-click"
                    : candidates.NextLabelMechanicId;
                _dependencies.HoldDebugTelemetryAfterSuccess($"Successful automated click: {mechanicDisplay}");

                if (_dependencies.OffscreenPathing.IsStickyTarget(nextLabel.ItemOnGround))
                {
                    _dependencies.OffscreenPathing.ClearStickyOffscreenTarget();
                }

                _dependencies.ChestLootSettlement.MarkPendingChestOpenConfirmation(candidates.NextLabelMechanicId, nextLabel);
                _dependencies.LabelSelection.MarkLeverClicked(nextLabel);
                if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                {
                    _dependencies.PathfindingService.ClearLatestPath();
                }
            }

            return new ExecutionResult(true);
        }

        private ExecutionResult HandleVisibleLabelResolveFailure(ClickTickContext context, ClickCandidates candidates, LabelOnGround nextLabel)
        {
            _dependencies.DebugLog("[ProcessRegularClick] Skipping label: no clickable point inside label bounds.");
            _dependencies.PublishClickFlowDebugStage("ClickPointResolveFailed", "TryCalculateClickPosition returned false", candidates.NextLabelMechanicId);

            if (candidates.SettlersOre.HasValue
                && OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(candidates.NextLabelMechanicId, candidates.SettlersOre.Value.MechanicId))
            {
                _dependencies.PublishClickFlowDebugStage("SettlersEntityFallbackAttempt", "Label unresolved; attempting settlers entity click", candidates.SettlersOre.Value.MechanicId);
                if (!IsBlockedByPostChestLootSettlement(context, candidates.SettlersOre.Value.MechanicId, candidates.SettlersOre.Value.Entity)
                    && _dependencies.VisibleMechanics.TryClickSettlersOre(candidates.SettlersOre.Value))
                {
                    _dependencies.PublishClickFlowDebugStage("SettlersEntityFallbackSuccess", "Settlers entity click succeeded after label resolve failure", candidates.SettlersOre.Value.MechanicId);
                    return StopExecution();
                }
            }

            bool shouldContinueEntityPathing = OffscreenPathingMath.ShouldPathfindToEntityAfterClickPointResolveFailure(
                _dependencies.Settings.WalkTowardOffscreenLabels.Value,
                nextLabel.ItemOnGround != null,
                nextLabel.ItemOnGround?.IsHidden == true,
                candidates.NextLabelMechanicId);
            if (shouldContinueEntityPathing)
            {
                _dependencies.PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", candidates.NextLabelMechanicId);
                _ = _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget(nextLabel.ItemOnGround);
            }

            return StopExecution();
        }
    }
}