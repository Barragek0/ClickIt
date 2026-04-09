namespace ClickIt.Features.Click.Core
{
    internal sealed class InteractionExecutionEngine(InteractionExecutionEngineDependencies dependencies)
    {
        private readonly InteractionExecutionEngineDependencies _dependencies = dependencies;

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

            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                "PostChestLootSettleBlocked",
                $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecision}",
                mechanicId);
            return true;
        }

        private bool TryExecuteSettlersHidden(ClickTickContext context, SettlersOreCandidate candidate)
        {
            return TryExecuteSettlers(context, candidate, hiddenFallback: true);
        }

        private bool TryExecuteSettlersVisible(ClickTickContext context, SettlersOreCandidate candidate)
            => TryExecuteSettlers(context, candidate, hiddenFallback: false);

        private bool TryExecuteSettlers(ClickTickContext context, SettlersOreCandidate candidate, bool hiddenFallback)
        {
            bool clicked = !IsBlockedByPostChestLootSettlement(context, candidate.MechanicId, candidate.Entity)
                && _dependencies.VisibleMechanics.TryClickSettlersOre(candidate);
            if (!hiddenFallback)
                return clicked;

            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                clicked ? "HiddenSettlersFallback" : "HiddenSettlersFallbackSkipped",
                clicked ? "Using hidden settlers candidate" : "Hidden settlers candidate was not targetable/valid at click time",
                candidate.MechanicId);
            return clicked;
        }

        private bool TryExecuteLostShipment(ClickTickContext context, LostShipmentCandidate candidate)
        {
            if (IsBlockedByPostChestLootSettlement(context, MechanicIds.LostShipment, candidate.Entity))
                return false;

            return _dependencies.VisibleMechanics.TryClickLostShipmentInteraction(candidate);
        }

        private bool TryExecuteShrine(ClickTickContext context)
        {
            if (context.NextShrine == null)
                return false;
            if (IsBlockedByPostChestLootSettlement(context, MechanicIds.Shrines, context.NextShrine))
                return false;

            return _dependencies.VisibleMechanics.TryClickShrineInteraction(context.NextShrine);
        }

        private static ExecutionResult StopExecution()
        {
            return new ExecutionResult(false);
        }

        private bool TryExecuteMechanicSelections(ClickTickContext context, ClickCandidates candidates, DecisionResult decision, bool hiddenFallback)
        {
            if (decision.TrySettlers && candidates.SettlersOre.HasValue)
            {
                bool clicked = hiddenFallback
                    ? TryExecuteSettlersHidden(context, candidates.SettlersOre.Value)
                    : TryExecuteSettlersVisible(context, candidates.SettlersOre.Value);
                if (clicked)
                    return true;
            }

            if (decision.TryLostShipment && candidates.LostShipment.HasValue)
                if (TryExecuteLostShipment(context, candidates.LostShipment.Value))
                    return true;


            if (decision.TryShrine && context.NextShrine != null)
                if (TryExecuteShrine(context))
                    return true;


            return false;
        }

        private ExecutionResult ExecuteHidden(ClickTickContext context, ClickCandidates candidates, DecisionResult decision)
        {
            if (TryExecuteMechanicSelections(context, candidates, decision, hiddenFallback: true))
                return StopExecution();

            if (context.IsPostChestLootSettleBlocking)
            {
                string chestLootSettleReason = context.ChestLootSettleReason;
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason, null);
                return StopExecution();
            }

            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget();


            _dependencies.DebugLog("[ProcessRegularClick] Ground items not visible, breaking");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHiddenExit", "No clickable hidden fallback selected", null);
            return StopExecution();
        }

        private ExecutionResult ExecuteVisible(ClickTickContext context, ClickCandidates candidates, DecisionResult decision)
        {
            if (TryExecuteMechanicSelections(context, candidates, decision, hiddenFallback: false))
                return StopExecution();

            if (candidates.NextLabel == null)
                return HandleNoVisibleLabel(context);


            return HandleVisibleLabel(context, candidates);
        }

        private ExecutionResult HandleNoVisibleLabel(ClickTickContext context)
        {
            if (context.IsPostChestLootSettleBlocking)
            {
                string chestLootSettleReason = context.ChestLootSettleReason;
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason, null);
                return StopExecution();
            }

            _dependencies.LabelInteractionPort.LogSelectionDiagnostics(context.AllLabels, 0, context.AllLabels?.Count ?? 0);
            if (_dependencies.ShouldCaptureClickDebug())
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("NoLabelCandidate", _dependencies.LabelInteraction.BuildNoLabelDebugSummary(context.AllLabels), null);


            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value
                && _dependencies.OffscreenPathing.TryHandleStickyOffscreenTarget(context.WindowTopLeft, context.AllLabels))
                return StopExecution();


            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget();


            _dependencies.DebugLog("[ProcessRegularClick] No label to click found, breaking");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("NoLabelExit", "No label click attempted", null);
            return StopExecution();
        }

        private ExecutionResult HandleVisibleLabel(ClickTickContext context, ClickCandidates candidates)
        {
            LabelOnGround nextLabel = candidates.NextLabel!;
            Entity? nextLabelItem = TryGetLabelItemOnGround(nextLabel);

            if (IsBlockedByPostChestLootSettlement(context, candidates.NextLabelMechanicId, nextLabelItem))
            {
                string chestLootSettleReason = context.ChestLootSettleReason;
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                return StopExecution();
            }

            if (_dependencies.LabelSelection.ShouldSkipOrHandleSpecialLabel(nextLabel, context.WindowTopLeft))
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", candidates.NextLabelMechanicId);
                return StopExecution();
            }

            (bool resolved, Vector2 clickPos) = _dependencies.LabelInteraction.TryResolveLabelClickPositionResult(
                nextLabel,
                candidates.NextLabelMechanicId,
                context.WindowTopLeft,
                context.AllLabels);
            if (!resolved)
                return HandleVisibleLabelResolveFailure(context, candidates, nextLabel);


            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", candidates.NextLabelMechanicId);

            _dependencies.ClickDebugPublisher.PublishLabelClickDebug(
                "LabelCandidate",
                candidates.NextLabelMechanicId,
                nextLabel,
                clickPos,
                true,
                "Settlers label candidate selected from ItemsOnGroundLabelsVisible");

            bool clicked = _dependencies.LabelInteraction.PerformResolvedLabelInteraction(clickPos, nextLabel, candidates.NextLabelMechanicId);

            _dependencies.ClickDebugPublisher.PublishLabelClickDebug(
                clicked ? "ClickSuccess" : "ClickFailed",
                candidates.NextLabelMechanicId,
                nextLabel,
                clickPos,
                clicked,
                clicked ? "Settlers click completed via label pipeline" : "Settlers click attempt failed via label pipeline");

            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(clicked ? "ClickExecuted" : "ClickRejected", clicked ? "Input click executed" : "Input click rejected", candidates.NextLabelMechanicId);

            if (clicked)
            {
                string mechanicDisplay = string.IsNullOrWhiteSpace(candidates.NextLabelMechanicId)
                    ? "visible-label-click"
                    : candidates.NextLabelMechanicId;
                SuccessfulInteractionAftermathApplier.Apply(
                    new SuccessfulInteractionAftermath(
                        Reason: $"Successful automated click: {mechanicDisplay}",
                        ShouldClearStickyTarget: _dependencies.OffscreenPathing.IsStickyTarget(nextLabelItem),
                        ShouldClearPath: _dependencies.Settings.WalkTowardOffscreenLabels.Value,
                        PendingChestMechanicId: candidates.NextLabelMechanicId,
                        PendingChestLabel: nextLabel,
                        ShouldRecordLeverClick: true),
                    _dependencies.HoldDebugTelemetryAfterSuccess,
                        clearStickyTarget: _dependencies.OffscreenPathing.ClearStickyOffscreenTarget,
                        clearPath: _dependencies.PathfindingService.ClearLatestPath,
                        markPendingChestOpenConfirmation: _dependencies.ChestLootSettlement.MarkPendingChestOpenConfirmation,
                        recordLeverClick: _dependencies.PathfindingLabelSuppression.RecordLeverClick);
            }

            return new ExecutionResult(true);
        }

        private ExecutionResult HandleVisibleLabelResolveFailure(ClickTickContext context, ClickCandidates candidates, LabelOnGround nextLabel)
        {
            Entity? nextLabelItem = TryGetLabelItemOnGround(nextLabel);

            _dependencies.DebugLog("[ProcessRegularClick] Skipping label: no clickable point inside label bounds.");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ClickPointResolveFailed", "TryCalculateClickPosition returned false", candidates.NextLabelMechanicId);

            if (candidates.SettlersOre.HasValue
                && OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(candidates.NextLabelMechanicId, candidates.SettlersOre.Value.MechanicId))
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("SettlersEntityFallbackAttempt", "Label unresolved; attempting settlers entity click", candidates.SettlersOre.Value.MechanicId);
                if (!IsBlockedByPostChestLootSettlement(context, candidates.SettlersOre.Value.MechanicId, candidates.SettlersOre.Value.Entity)
                    && _dependencies.VisibleMechanics.TryClickSettlersOre(candidates.SettlersOre.Value))
                {
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("SettlersEntityFallbackSuccess", "Settlers entity click succeeded after label resolve failure", candidates.SettlersOre.Value.MechanicId);
                    return StopExecution();
                }
            }

            bool shouldContinueEntityPathing = OffscreenPathingMath.ShouldPathfindToEntityAfterClickPointResolveFailure(
                _dependencies.Settings.WalkTowardOffscreenLabels.Value,
                nextLabelItem != null,
                candidates.NextLabelMechanicId);
            if (shouldContinueEntityPathing)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", candidates.NextLabelMechanicId);
                _ = _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget(nextLabelItem);
            }

            return StopExecution();
        }

        private static Entity? TryGetLabelItemOnGround(LabelOnGround? label)
        {
            return DynamicAccess.TryGetDynamicValue(label, static l => l.ItemOnGround, out object? rawItem)
                && rawItem is Entity item
                ? item
                : null;
        }
    }
}