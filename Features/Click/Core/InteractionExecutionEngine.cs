namespace ClickIt.Features.Click.Core
{
    internal sealed class InteractionExecutionEngine(ClickRuntimeEngineDependencies dependencies)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = dependencies;

        // Let the game register the hover on the blight menu button before the click lands — clicking too soon after the cursor arrives can miss the element (the UIHover read is unreliable for a freshly-moved cursor).
        private const int BlightMenuClickSettleMs = 60;

        private void RecordStage(int stageIndex, long bytes, double ms)
            => _dependencies.Telemetry.RecordBreakdownStage?.Invoke(stageIndex, bytes, ms);

        public ExecutionResult Execute(ClickTickContext context, ClickCandidates candidates, DecisionResult decision)
        {
            // Ordered decision chain (mechanics → harvest → blight → label → walk).
            if (TryHandleBlightBlocking())
                return StopExecution();

            if (TryExecuteMechanicSelections(context, candidates, decision, hiddenFallback: !decision.GroundItemsVisible))
                return StopExecution();

            if (decision.GroundItemsVisible && TryClickChosenHarvestLabel(context))
                return StopExecution();

            if (TryProgressBlightBuilding())
                return StopExecution();

            return decision.GroundItemsVisible
                ? ExecuteVisibleRemainder(context, candidates)
                : ExecuteHiddenRemainder(context, candidates);
        }

        private bool TryHandleBlightBlocking()
        {
            if (!_dependencies.Policy.Settings.BlightBlockOtherInteractions.Value
                || _dependencies.Mechanics.TryProgressBlightBuilding == null
                || (_dependencies.Mechanics.IsBlightEncounterActive?.Invoke() != true))
                return false;

            if (!TryProgressBlightBuilding())
            {
                Entity? blightTarget = _dependencies.Mechanics.GetBlightPathfindTarget?.Invoke();
                if (blightTarget != null)
                    WalkTowardEntity(blightTarget, MechanicIds.Blight);
            }
            return true;
        }

        private bool IsBlockedByPostChestLootSettlement(ClickTickContext context, string? mechanicId, Entity? entity)
        {
            if (!context.IsPostChestLootSettleBlocking)
                return false;

            if (_dependencies.Selection.ChestLootSettlement.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out string bypassDecision))
                return false;

            if (_dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug())
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
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
                && _dependencies.Selection.VisibleMechanics.TryClickSettlersOre(candidate);
            if (!hiddenFallback)
                return clicked;

            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                clicked ? "HiddenSettlersFallback" : "HiddenSettlersFallbackSkipped",
                clicked ? "Using hidden settlers candidate" : "Hidden settlers candidate was not targetable/valid at click time",
                candidate.MechanicId);
            return clicked;
        }

        private bool TryExecuteLostShipment(ClickTickContext context, LostShipmentCandidate candidate)
        {
            if (IsBlockedByPostChestLootSettlement(context, MechanicIds.LostShipment, candidate.Entity))
                return false;

            return _dependencies.Selection.VisibleMechanics.TryClickLostShipmentInteraction(candidate);
        }

        private bool TryExecuteShrine(ClickTickContext context)
        {
            if (context.NextShrine == null)
                return false;
            if (IsBlockedByPostChestLootSettlement(context, MechanicIds.Shrines, context.NextShrine))
                return false;

            return _dependencies.Selection.VisibleMechanics.TryClickShrineInteraction(context.NextShrine);
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

        private bool WalkTowardEntity(Entity entity, string? mechanicId)
        {
            if (!_dependencies.Policy.Settings.WalkTowardOffscreenLabels.Value)
                return false;

            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                "WalkTowardEntity", $"Pathfinding toward entity", mechanicId ?? string.Empty);
            return _dependencies.Pathing.OffscreenPathing.TryWalkTowardOffscreenTarget(entity);
        }

        // Unified click-vs-walk decision for a selected label, shared by the visible and hidden ground-item paths: click the label in place when its click point resolves into a clickable area; otherwise pathfind toward its entity. When it resolves clickable, the client-space click point is also returned so the caller does not resolve it a second time.
        private bool TryPathfindToLabelInsteadOfClick(
            ClickTickContext context,
            LabelOnGround label,
            string? mechanicId,
            Entity? entity,
            out bool hasClickableClickPos,
            out Vector2 clientClickPos,
            out bool settleBlocked)
        {
            hasClickableClickPos = false;
            clientClickPos = default;
            settleBlocked = false;

            if (!_dependencies.Policy.Settings.WalkTowardOffscreenLabels.Value || entity == null)
                return false;

            // During the post-chest-loot-settle wait a label is only clicked or walked when it is within the nearby-mechanics bypass distance; a farther target consumes the tick waiting for drops to settle instead of pathfinding off-screen.
            if (IsBlockedByPostChestLootSettlement(context, mechanicId, entity))
            {
                settleBlocked = true;
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                    "PostChestLootSettleBlocked", context.ChestLootSettleReason, mechanicId);
                return true;
            }

            // Spec 11: a label beyond ClickDistance is walked to even when its click point resolves. Eligibility normally filters these out, but the hover preference and hidden path can surface a far label here - never click it; walk toward it (or do nothing when the walk cannot resolve) and consume the tick so a far label is never clicked.
            if (DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out float entityDistance)
                && entityDistance > _dependencies.Policy.Settings.ClickDistance.Value)
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                    "WalkTowardLabel", $"Label beyond ClickDistance ({entityDistance:0.0} > {_dependencies.Policy.Settings.ClickDistance.Value}); pathfinding toward entity", mechanicId);
                _dependencies.Pathing.OffscreenPathing.TryWalkTowardOffscreenTarget(entity);
                return true;
            }

            (bool resolved, Vector2 clickPos) = _dependencies.Selection.LabelInteraction.TryResolveLabelClickPositionResult(
                label, mechanicId, default, context.AllLabels);
            if (resolved && _dependencies.Policy.PointIsInClickableArea(clickPos, mechanicId ?? string.Empty))
            {
                hasClickableClickPos = true;
                clientClickPos = clickPos;
                return false;
            }

            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                "WalkTowardLabel", "Label not clickable in place; pathfinding toward entity", mechanicId);
            return _dependencies.Pathing.OffscreenPathing.TryWalkTowardOffscreenTarget(entity);
        }

        private bool TryPublishPostChestLootSettleBlock(ClickTickContext context)
        {
            if (!context.IsPostChestLootSettleBlocking)
                return false;

            string chestLootSettleReason = context.ChestLootSettleReason;
            _dependencies.Telemetry.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason, null);
            return true;
        }

        private ExecutionResult ExecuteHiddenRemainder(ClickTickContext context, ClickCandidates candidates)
        {
            if (TryPublishPostChestLootSettleBlock(context))
                return StopExecution();

            if (candidates.NextLabel != null)
            {
                _dependencies.Telemetry.DebugLog("[ExecuteHidden] NextLabel exists, checking clickability");
                if (_dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug())
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelCheck",
                        $"label has entity={DynamicAccess.TryGetLabelItemOnGround(candidates.NextLabel, out _)} mechanic={candidates.NextLabelMechanicId}", candidates.NextLabelMechanicId);
                DynamicAccess.TryGetLabelItemOnGround(candidates.NextLabel, out Entity? labelEntity);
                if (labelEntity != null && TryPathfindToLabelInsteadOfClick(context, candidates.NextLabel, candidates.NextLabelMechanicId, labelEntity,
                    out _, out _, out _))
                    return StopExecution();
            }
            else
            {
                _dependencies.Telemetry.DebugLog("[ExecuteHidden] No NextLabel in candidates");
                if (_dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug())
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelNull",
                        $"labelsInContext={context.AllLabels?.Count ?? 0} settlers={candidates.SettlersOre.HasValue} lostShipment={candidates.LostShipment.HasValue}", null);
            }

            if (_dependencies.Policy.Settings.WalkTowardOffscreenLabels.Value)
            {
                _dependencies.Telemetry.DebugLog($"[ExecuteHidden] Calling TryWalkTowardOffscreenTarget (no target)");
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenFallbackWalk",
                    "Calling TryWalkTowardOffscreenTarget with no preferred target", null);
                _dependencies.Pathing.OffscreenPathing.TryWalkTowardOffscreenTarget();
            }
            else
            {
                _dependencies.Telemetry.DebugLog($"[ExecuteHidden] WalkTowardOffscreenLabels is OFF");
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenWalkDisabled",
                    "WalkTowardOffscreenLabels setting is OFF", null);
            }

            _dependencies.Telemetry.DebugLog("[ProcessRegularClick] Ground items not visible, breaking");
            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHiddenExit", "No clickable hidden fallback selected", null);
            return StopExecution();
        }

        private ExecutionResult ExecuteVisibleRemainder(ClickTickContext context, ClickCandidates candidates)
        {
            if (candidates.NextLabel == null)
                return HandleNoVisibleLabel(context);

            // Unified clickability check: if the selected label's click position is not in a clickable area, pathfind toward its entity instead of attempting a click that would fail.
            DynamicAccess.TryGetLabelItemOnGround(candidates.NextLabel, out Entity? nextLabelItem);
            if (TryPathfindToLabelInsteadOfClick(context, candidates.NextLabel, candidates.NextLabelMechanicId, nextLabelItem,
                out bool hasClickableClickPos, out Vector2 clientClickPos, out _))
                return StopExecution();

            return HandleVisibleLabel(context, candidates, nextLabelItem, hasClickableClickPos, clientClickPos);
        }

        private bool TryClickChosenHarvestLabel(ClickTickContext context)
        {
            if (_dependencies.Mechanics.GetHarvestLabelToClick == null)
                return false;

            LabelOnGround? chosen = _dependencies.Mechanics.GetHarvestLabelToClick();
            if (chosen == null)
                return false;

            (bool resolved, Vector2 clickPos) = _dependencies.Selection.LabelInteraction.TryResolveLabelClickPositionResult(
                chosen, MechanicIds.Harvest, context.WindowTopLeft, context.AllLabels);
            if (!resolved)
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                    "HarvestResolveFailed", "Could not resolve harvest plot click position", MechanicIds.Harvest);
                return false;
            }

            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                "HarvestClickDirect", $"Direct harvest click ({clickPos.X:0.0},{clickPos.Y:0.0})", MechanicIds.Harvest);

            return _dependencies.Selection.LabelInteraction.PerformResolvedLabelInteraction(
                clickPos, chosen, MechanicIds.Harvest);
        }

        private bool TryProgressBlightBuilding()
        {
            if (_dependencies.Mechanics.TryProgressBlightBuilding == null)
                return false;

            BlightBuildAction action = _dependencies.Mechanics.TryProgressBlightBuilding();
            Vector2 clickPos = action.ClickPosition;

            switch (action.Kind)
            {
                case BlightBuildActionKind.ClickPosition:
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                        "BlightBuildClick", $"{action.DebugMessage} ({clickPos.X:0.0},{clickPos.Y:0.0})", MechanicIds.Blight);
                    if (action.IsMenuClick)
                    {
                        // Let the game register the hover on the menu button before the click lands — clicking too soon after the cursor arrives can miss the element entirely.
                        ClickPipelineTiming.Sleep(BlightMenuClickSettleMs);
                    }
                    _dependencies.Selection.LabelInteraction.PerformMechanicClick(clickPos);
                    return true;

                case BlightBuildActionKind.WalkToTarget:
                    {
                        _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                            "BlightBuildWalk", action.DebugMessage ?? "Walking toward foundation", MechanicIds.Blight);
                        Entity? walkTarget = _dependencies.Mechanics.GetBlightPathfindTarget?.Invoke();
                        if (walkTarget != null)
                        {
                            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                                "BlightBuildWalk", "walkTarget found, calling WalkTowardEntity", MechanicIds.Blight);
                            WalkTowardEntity(walkTarget, MechanicIds.Blight);
                        }
                        else
                        {
                            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                                "BlightBuildWalk", "walkTarget=null - GetPathfindingTargetEntity returned null (entity on screen or no target)", MechanicIds.Blight);
                        }
                        // The walk was already performed; report handled so the caller's fallback does not issue a second redundant walk click on the same target.
                        return true;
                    }

                case BlightBuildActionKind.WalkToPosition:
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                        "BlightBuildWalk", action.DebugMessage ?? "Walking toward foundation position", MechanicIds.Blight);
                    _dependencies.Pathing.OffscreenPathing.TryWalkTowardGridPosition(action.GridPosition);
                    return true;

                case BlightBuildActionKind.Complete:
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                        "BlightBuildComplete", action.DebugMessage ?? "Build step completed", MechanicIds.Blight);
                    return true;

                case BlightBuildActionKind.Error:
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(
                        "BlightBuildError", action.DebugMessage ?? "Unknown error", MechanicIds.Blight);
                    return false;

                case BlightBuildActionKind.None:
                default:
                    return false;
            }
        }

        private ExecutionResult HandleNoVisibleLabel(ClickTickContext context)
        {
            if (_dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug())
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("HandleNoVisibleLabel",
                    $"labelsInContext={context.AllLabels?.Count ?? 0} walkSetting={_dependencies.Policy.Settings.WalkTowardOffscreenLabels.Value} {ClickLabelSelectionMath.DescribeCursorPosition()}", null);

            if (TryPublishPostChestLootSettleBlock(context))
                return StopExecution();

            _dependencies.Selection.LabelInteractionPort.LogSelectionDiagnostics(context.AllLabels, 0, context.AllLabels?.Count ?? 0);
            if (_dependencies.Telemetry.ShouldCaptureClickDebug())
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("NoLabelCandidate", _dependencies.Selection.LabelInteraction.BuildNoLabelDebugSummary(context.AllLabels), null);


            if (_dependencies.Policy.Settings.WalkTowardOffscreenLabels.Value
                && _dependencies.Pathing.OffscreenPathing.TryHandleStickyOffscreenTarget(context.WindowTopLeft, context.AllLabels))
                return StopExecution();


            if (_dependencies.Policy.Settings.WalkTowardOffscreenLabels.Value)
            {
                // Pickup-to-next-pathfinding latency: how long after the last successful click the walk path was reached (the delay the user sees between picking up an item and walking to the next).
                long now = Environment.TickCount64;
                long lastClickAtMs = _dependencies.Policy.ClickSuccessAnchor?.Value ?? 0;
                if (lastClickAtMs > 0 && now - lastClickAtMs < 5000)
                {
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("PickupToWalkLatency",
                        $"{now - lastClickAtMs}ms since last successful click | labels={context.AllLabels?.Count ?? 0}", null);
                }
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleFallbackWalk",
                    $"Calling TryWalkTowardOffscreenTarget (no target) with {context.AllLabels?.Count ?? 0} labels in context", null);
                _dependencies.Pathing.OffscreenPathing.TryWalkTowardOffscreenTarget();
            }
            else
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleWalkDisabled",
                    "WalkTowardOffscreenLabels is OFF in HandleNoVisibleLabel", null);
            }

            Entity? blightTarget = _dependencies.Mechanics.GetBlightPathfindTarget?.Invoke();
            if (_dependencies.Policy.Settings.BlightPathfindToBuild.Value && blightTarget != null)
                WalkTowardEntity(blightTarget, MechanicIds.Blight);

            _dependencies.Telemetry.DebugLog("[ProcessRegularClick] No label to click found, breaking");
            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("NoLabelExit", "No label click attempted", null);
            return StopExecution();
        }

        private ExecutionResult HandleVisibleLabel(ClickTickContext context, ClickCandidates candidates, Entity? nextLabelItem, bool hasClickableClickPos, Vector2 clientClickPos)
        {
            LabelOnGround nextLabel = candidates.NextLabel!;

            // A locked strongbox (the strongbox overlay's red frame) cannot be opened, so it is never clicked even if a stale selection cache ranked it; fall through to the no-visible-label path so the plugin still walks toward other targets.
            if (nextLabelItem != null && MechanicClassifier.IsLockedStrongbox(nextLabelItem))
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("LockedChestSkipped",
                    $"Strongbox is locked - not clickable | {ClickLabelSelectionMath.DescribeLabel(nextLabel)} {ClickLabelSelectionMath.DescribeCursorPosition()}", candidates.NextLabelMechanicId);
                return HandleNoVisibleLabel(context);
            }

            if (IsBlockedByPostChestLootSettlement(context, candidates.NextLabelMechanicId, nextLabelItem))
            {
                string chestLootSettleReason = context.ChestLootSettleReason;
                _dependencies.Telemetry.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                return StopExecution();
            }

            if (_dependencies.Selection.SpecialLabelInteraction.TryHandle(nextLabel, context.WindowTopLeft))
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", candidates.NextLabelMechanicId);
                return StopExecution();
            }

            long resolveStart = GC.GetAllocatedBytesForCurrentThread();
            long resolveTimestamp = Stopwatch.GetTimestamp();
            double resolveSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            // Reuse the client-space click point already resolved by the click-vs-walk check (adding the window top-left) instead of resolving the label a second time.
            (bool resolved, Vector2 clickPos) = hasClickableClickPos
                ? (true, clientClickPos + context.WindowTopLeft)
                : _dependencies.Selection.LabelInteraction.TryResolveLabelClickPositionResult(
                    nextLabel,
                    candidates.NextLabelMechanicId,
                    context.WindowTopLeft,
                    context.AllLabels);
            long resolveBytes = GC.GetAllocatedBytesForCurrentThread() - resolveStart;
            double resolveMs = GetElapsedMs(resolveTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - resolveSleepBefore);
            RecordStage(PerformanceMonitor.ClickResolveStageIndex, resolveBytes, resolveMs);
            if (!resolved)
                return HandleVisibleLabelResolveFailure(context, candidates, nextLabelItem);


            if (_dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug())
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", candidates.NextLabelMechanicId);

            if (_dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug())
                _dependencies.Telemetry.ClickDebugPublisher.PublishLabelClickDebug(
                    "LabelCandidate",
                    candidates.NextLabelMechanicId,
                    nextLabel,
                    clickPos,
                    true,
                    $"Label candidate selected (mechanic: {candidates.NextLabelMechanicId ?? "none"})");

            long inputStart = GC.GetAllocatedBytesForCurrentThread();
            long inputTimestamp = Stopwatch.GetTimestamp();
            double inputSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            bool clicked = _dependencies.Selection.LabelInteraction.PerformResolvedLabelInteraction(clickPos, nextLabel, candidates.NextLabelMechanicId);
            long inputBytes = GC.GetAllocatedBytesForCurrentThread() - inputStart;
            // Exclude the intentional hover/post-click settle sleeps inside the click dispatch so Input shows the true processing cost (the host already reports those waits as the separate Sleep row).
            double inputMs = GetElapsedMs(inputTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - inputSleepBefore);
            RecordStage(PerformanceMonitor.ClickInputStageIndex, inputBytes, inputMs);

            if (_dependencies.Telemetry.ClickDebugPublisher.ShouldCaptureClickDebug())
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishLabelClickDebug(
                    clicked ? "ClickSuccess" : "ClickFailed",
                    candidates.NextLabelMechanicId,
                    nextLabel,
                    clickPos,
                    clicked,
                    clicked ? $"Click executed ({candidates.NextLabelMechanicId ?? "visible-label"})" : $"Click rejected ({candidates.NextLabelMechanicId ?? "visible-label"})");

                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage(clicked ? "ClickExecuted" : "ClickRejected", clicked ? "Input click executed" : "Input click rejected", candidates.NextLabelMechanicId);
            }

            if (clicked)
            {
                _dependencies.Policy.ClickSuccessAnchor?.Mark();
                string mechanicDisplay = string.IsNullOrWhiteSpace(candidates.NextLabelMechanicId)
                    ? "visible-label-click"
                    : candidates.NextLabelMechanicId;
                SuccessfulInteractionAftermathApplier.Apply(
                    new SuccessfulInteractionAftermath(
                        Reason: $"Successful automated click: {mechanicDisplay}",
                        ShouldClearStickyTarget: _dependencies.Pathing.OffscreenPathing.IsStickyTarget(nextLabelItem),
                        ShouldClearPath: _dependencies.Policy.Settings.WalkTowardOffscreenLabels.Value,
                        PendingChestMechanicId: candidates.NextLabelMechanicId,
                        PendingChestLabel: nextLabel,
                        ShouldRecordLeverClick: true),
                    _dependencies.Telemetry.HoldDebugTelemetryAfterSuccess,
                        clearStickyTarget: _dependencies.Pathing.OffscreenPathing.ClearStickyOffscreenTarget,
                        clearPath: _dependencies.Pathing.PathfindingService.ClearLatestPath,
                        markPendingChestOpenConfirmation: _dependencies.Selection.ChestLootSettlement.MarkPendingChestOpenConfirmation,
                        recordLeverClick: _dependencies.Pathing.PathfindingLabelSuppression.RecordLeverClick);
            }

            return new ExecutionResult(true);
        }

        private ExecutionResult HandleVisibleLabelResolveFailure(ClickTickContext context, ClickCandidates candidates, Entity? nextLabelItem)
        {
            _dependencies.Telemetry.DebugLog("[ProcessRegularClick] Skipping label: no clickable point inside label bounds.");
            _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("ClickPointResolveFailed", "TryCalculateClickPosition returned false", candidates.NextLabelMechanicId);

            if (candidates.SettlersOre.HasValue
                && OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(candidates.NextLabelMechanicId, candidates.SettlersOre.Value.MechanicId))
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("SettlersEntityFallbackAttempt", "Label unresolved; attempting settlers entity click", candidates.SettlersOre.Value.MechanicId);
                if (!IsBlockedByPostChestLootSettlement(context, candidates.SettlersOre.Value.MechanicId, candidates.SettlersOre.Value.Entity)
                    && _dependencies.Selection.VisibleMechanics.TryClickSettlersOre(candidates.SettlersOre.Value))
                {
                    _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("SettlersEntityFallbackSuccess", "Settlers entity click succeeded after label resolve failure", candidates.SettlersOre.Value.MechanicId);
                    return StopExecution();
                }
            }

            bool shouldContinueEntityPathing = OffscreenPathingMath.ShouldPathfindToEntityAfterClickPointResolveFailure(
                _dependencies.Policy.Settings.WalkTowardOffscreenLabels.Value,
                nextLabelItem != null,
                candidates.NextLabelMechanicId);
            if (shouldContinueEntityPathing && nextLabelItem != null)
            {
                _dependencies.Telemetry.ClickDebugPublisher.PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", candidates.NextLabelMechanicId);
                WalkTowardEntity(nextLabelItem, candidates.NextLabelMechanicId);
            }

            return StopExecution();
        }

        private static double GetElapsedMs(long startTimestamp)
            => StopwatchMath.ElapsedMs(startTimestamp);
    }
}
