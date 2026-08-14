namespace ClickIt.Features.Click.Core
{
    internal sealed class InteractionExecutionEngine(InteractionExecutionEngineDependencies dependencies)
    {
        private readonly InteractionExecutionEngineDependencies _dependencies = dependencies;

        // Let the game register the hover on the blight menu button before the click lands — clicking too soon after the cursor arrives can miss the element (the UIHover read is unreliable for a freshly-moved cursor).
        private const int BlightMenuClickSettleMs = 60;

        private void RecordStage(int stageIndex, long bytes, double ms)
            => _dependencies.RecordBreakdownStage?.Invoke(stageIndex, bytes, ms);

        public ExecutionResult Execute(ClickTickContext context, ClickCandidates candidates, DecisionResult decision)
        {
            // Ordered decision chain (mechanics → harvest → blight → label → walk).
            if (TryHandleBlightBlocking())
                return StopExecution(didActionableWork: true);

            if (TryExecuteMechanicSelections(context, candidates, decision, hiddenFallback: !decision.GroundItemsVisible))
                return StopExecution(didActionableWork: true);

            if (decision.GroundItemsVisible && TryClickChosenHarvestLabel(context))
                return StopExecution(didActionableWork: true);

            if (TryProgressBlightBuilding())
                return StopExecution(didActionableWork: true);

            return decision.GroundItemsVisible
                ? ExecuteVisibleRemainder(context, candidates)
                : ExecuteHiddenRemainder(context, candidates);
        }

        private bool TryHandleBlightBlocking()
        {
            if (!_dependencies.Settings.BlightBlockOtherInteractions.Value
                || _dependencies.TryProgressBlightBuilding == null
                || (_dependencies.IsBlightEncounterActive?.Invoke() != true))
                return false;

            if (!TryProgressBlightBuilding())
            {
                Entity? blightTarget = _dependencies.GetBlightPathfindTarget?.Invoke();
                if (blightTarget != null)
                    WalkTowardEntity(blightTarget, MechanicIds.Blight);
            }
            return true;
        }

        private bool IsBlockedByPostChestLootSettlement(ClickTickContext context, string? mechanicId, Entity? entity)
        {
            if (!context.IsPostChestLootSettleBlocking)
                return false;

            if (_dependencies.ChestLootSettlement.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out string bypassDecision))
                return false;

            if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
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

        private static ExecutionResult StopExecution(bool didActionableWork = false)
        {
            return new ExecutionResult(false, didActionableWork);
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
            if (!_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                return false;

            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                "WalkTowardEntity", $"Pathfinding toward entity", mechanicId ?? string.Empty);
            return _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget(entity);
        }

        // Unified click-vs-walk decision for a selected label, shared by the visible and hidden ground-item paths: click the label in place when its click point resolves into a clickable area; otherwise pathfind toward its entity.
        private bool TryPathfindToLabelInsteadOfClick(ClickTickContext context, LabelOnGround label, string? mechanicId, Entity? entity)
        {
            if (!_dependencies.Settings.WalkTowardOffscreenLabels.Value || entity == null)
                return false;

            // Spec 11: a label beyond ClickDistance is walked to even when its click point resolves. Eligibility normally filters these out, but the hover preference and hidden path can surface a far label here - never click it; walk toward it (or do nothing when the walk cannot resolve) and consume the tick so a far label is never clicked.
            if (DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out float entityDistance)
                && entityDistance > _dependencies.Settings.ClickDistance.Value)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                    "WalkTowardLabel", $"Label beyond ClickDistance ({entityDistance:0.0} > {_dependencies.Settings.ClickDistance.Value}); pathfinding toward entity", mechanicId);
                _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget(entity);
                return true;
            }

            (bool resolved, Vector2 clickPos) = _dependencies.LabelInteraction.TryResolveLabelClickPositionResult(
                label, mechanicId, default, context.AllLabels);
            if (resolved && _dependencies.PointIsInClickableArea(clickPos, mechanicId ?? string.Empty))
                return false;

            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                "WalkTowardLabel", "Label not clickable in place; pathfinding toward entity", mechanicId);
            return _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget(entity);
        }

        private bool TryPublishPostChestLootSettleBlock(ClickTickContext context)
        {
            if (!context.IsPostChestLootSettleBlocking)
                return false;

            string chestLootSettleReason = context.ChestLootSettleReason;
            _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason, null);
            return true;
        }

        private ExecutionResult ExecuteHiddenRemainder(ClickTickContext context, ClickCandidates candidates)
        {
            if (TryPublishPostChestLootSettleBlock(context))
                return StopExecution();

            if (candidates.NextLabel != null)
            {
                _dependencies.DebugLog("[ExecuteHidden] NextLabel exists, checking clickability");
                if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelCheck",
                        $"label has entity={(TryGetLabelItemOnGround(candidates.NextLabel) != null)} mechanic={candidates.NextLabelMechanicId}", candidates.NextLabelMechanicId);
                Entity? labelEntity = TryGetLabelItemOnGround(candidates.NextLabel);
                if (labelEntity != null && TryPathfindToLabelInsteadOfClick(context, candidates.NextLabel, candidates.NextLabelMechanicId, labelEntity))
                    return StopExecution(didActionableWork: true);
            }
            else
            {
                _dependencies.DebugLog("[ExecuteHidden] No NextLabel in candidates");
                if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenLabelNull",
                        $"labelsInContext={context.AllLabels?.Count ?? 0} settlers={candidates.SettlersOre.HasValue} lostShipment={candidates.LostShipment.HasValue}", null);
            }

            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
            {
                _dependencies.DebugLog($"[ExecuteHidden] Calling TryWalkTowardOffscreenTarget (no target)");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenFallbackWalk",
                    "Calling TryWalkTowardOffscreenTarget with no preferred target", null);
                _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget();
            }
            else
            {
                _dependencies.DebugLog($"[ExecuteHidden] WalkTowardOffscreenLabels is OFF");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HiddenWalkDisabled",
                    "WalkTowardOffscreenLabels setting is OFF", null);
            }

            _dependencies.DebugLog("[ProcessRegularClick] Ground items not visible, breaking");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("GroundItemsHiddenExit", "No clickable hidden fallback selected", null);
            return StopExecution();
        }

        private ExecutionResult ExecuteVisibleRemainder(ClickTickContext context, ClickCandidates candidates)
        {
            if (candidates.NextLabel == null)
                return HandleNoVisibleLabel(context);

            // Unified clickability check: if the selected label's click position is not in a clickable area, pathfind toward its entity instead of attempting a click that would fail.
            if (TryPathfindToLabelInsteadOfClick(context, candidates.NextLabel, candidates.NextLabelMechanicId,
                TryGetLabelItemOnGround(candidates.NextLabel)))
                return StopExecution(didActionableWork: true);


            return HandleVisibleLabel(context, candidates);
        }

        private bool TryClickChosenHarvestLabel(ClickTickContext context)
        {
            if (_dependencies.GetHarvestLabelToClick == null)
                return false;

            LabelOnGround? chosen = _dependencies.GetHarvestLabelToClick();
            if (chosen == null)
                return false;

            (bool resolved, Vector2 clickPos) = _dependencies.LabelInteraction.TryResolveLabelClickPositionResult(
                chosen, MechanicIds.Harvest, context.WindowTopLeft, context.AllLabels);
            if (!resolved)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                    "HarvestResolveFailed", "Could not resolve harvest plot click position", MechanicIds.Harvest);
                return false;
            }

            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                "HarvestClickDirect", $"Direct harvest click ({clickPos.X:0.0},{clickPos.Y:0.0})", MechanicIds.Harvest);

            return _dependencies.LabelInteraction.PerformResolvedLabelInteraction(
                clickPos, chosen, MechanicIds.Harvest);
        }

        private bool TryProgressBlightBuilding()
        {
            if (_dependencies.TryProgressBlightBuilding == null)
                return false;

            BlightBuildAction action = _dependencies.TryProgressBlightBuilding();
            Vector2 clickPos = action.ClickPosition;

            switch (action.Kind)
            {
                case BlightBuildActionKind.ClickPosition:
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                        "BlightBuildClick", $"{action.DebugMessage} ({clickPos.X:0.0},{clickPos.Y:0.0})", MechanicIds.Blight);
                    if (action.IsMenuClick)
                    {
                        // Let the game register the hover on the menu button before the click lands — clicking too soon after the cursor arrives can miss the element entirely.
                        ClickPipelineTiming.Sleep(BlightMenuClickSettleMs);
                    }
                    _dependencies.LabelInteraction.PerformMechanicClick(clickPos);
                    return true;

                case BlightBuildActionKind.WalkToTarget:
                    {
                        _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                            "BlightBuildWalk", action.DebugMessage ?? "Walking toward foundation", MechanicIds.Blight);
                        Entity? walkTarget = _dependencies.GetBlightPathfindTarget?.Invoke();
                        if (walkTarget != null)
                        {
                            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                                "BlightBuildWalk", "walkTarget found, calling WalkTowardEntity", MechanicIds.Blight);
                            WalkTowardEntity(walkTarget, MechanicIds.Blight);
                        }
                        else
                        {
                            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                                "BlightBuildWalk", "walkTarget=null - GetPathfindingTargetEntity returned null (entity on screen or no target)", MechanicIds.Blight);
                        }
                        // The walk was already performed; report handled so the caller's fallback does not issue a second redundant walk click on the same target.
                        return true;
                    }

                case BlightBuildActionKind.WalkToPosition:
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                        "BlightBuildWalk", action.DebugMessage ?? "Walking toward foundation position", MechanicIds.Blight);
                    _dependencies.OffscreenPathing.TryWalkTowardGridPosition(action.GridPosition);
                    return true;

                case BlightBuildActionKind.Complete:
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                        "BlightBuildComplete", action.DebugMessage ?? "Build step completed", MechanicIds.Blight);
                    return true;

                case BlightBuildActionKind.Error:
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                        "BlightBuildError", action.DebugMessage ?? "Unknown error", MechanicIds.Blight);
                    return false;

                case BlightBuildActionKind.None:
                default:
                    return false;
            }
        }

        private ExecutionResult HandleNoVisibleLabel(ClickTickContext context)
        {
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("HandleNoVisibleLabel",
                $"labelsInContext={context.AllLabels?.Count ?? 0} walkSetting={_dependencies.Settings.WalkTowardOffscreenLabels.Value} {ClickLabelSelectionMath.DescribeCursorPosition()}", null);

            if (TryPublishPostChestLootSettleBlock(context))
                return StopExecution();

            _dependencies.LabelInteractionPort.LogSelectionDiagnostics(context.AllLabels, 0, context.AllLabels?.Count ?? 0);
            if (_dependencies.ShouldCaptureClickDebug())
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("NoLabelCandidate", _dependencies.LabelInteraction.BuildNoLabelDebugSummary(context.AllLabels), null);


            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value
                && _dependencies.OffscreenPathing.TryHandleStickyOffscreenTarget(context.WindowTopLeft, context.AllLabels))
                return StopExecution(didActionableWork: true);


            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
            {
                // Pickup-to-next-pathfinding latency: how long after the last successful click the walk path
                // was reached (the delay the user sees between picking up an item and walking to the next).
                long now = Environment.TickCount64;
                long lastClickAtMs = _dependencies.ClickSuccessAnchor?.Value ?? 0;
                if (lastClickAtMs > 0 && now - lastClickAtMs < 5000)
                {
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("PickupToWalkLatency",
                        $"{now - lastClickAtMs}ms since last successful click | labels={context.AllLabels?.Count ?? 0}", null);
                }
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleFallbackWalk",
                    $"Calling TryWalkTowardOffscreenTarget (no target) with {context.AllLabels?.Count ?? 0} labels in context", null);
                _dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget();
            }
            else
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("VisibleWalkDisabled",
                    "WalkTowardOffscreenLabels is OFF in HandleNoVisibleLabel", null);
            }

            Entity? blightTarget = _dependencies.GetBlightPathfindTarget?.Invoke();
            if (_dependencies.Settings.BlightPathfindToBuild.Value && blightTarget != null)
                WalkTowardEntity(blightTarget, MechanicIds.Blight);

            _dependencies.DebugLog("[ProcessRegularClick] No label to click found, breaking");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("NoLabelExit", "No label click attempted", null);
            return StopExecution();
        }

        private ExecutionResult HandleVisibleLabel(ClickTickContext context, ClickCandidates candidates)
        {
            LabelOnGround nextLabel = candidates.NextLabel!;
            Entity? nextLabelItem = TryGetLabelItemOnGround(nextLabel);

            // A locked strongbox (the strongbox overlay's red frame) cannot be opened, so it is never clicked even if a stale selection cache ranked it; fall through to the no-visible-label path so the plugin still walks toward other targets.
            if (nextLabelItem != null && MechanicClassifier.IsLockedStrongbox(nextLabelItem))
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("LockedChestSkipped",
                    $"Strongbox is locked - not clickable | {ClickLabelSelectionMath.DescribeLabel(nextLabel)} {ClickLabelSelectionMath.DescribeCursorPosition()}", candidates.NextLabelMechanicId);
                return HandleNoVisibleLabel(context);
            }

            if (IsBlockedByPostChestLootSettlement(context, candidates.NextLabelMechanicId, nextLabelItem))
            {
                string chestLootSettleReason = context.ChestLootSettleReason;
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                return StopExecution();
            }

            if (_dependencies.SpecialLabelInteraction.TryHandle(nextLabel, context.WindowTopLeft))
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", candidates.NextLabelMechanicId);
                return StopExecution(didActionableWork: true);
            }

            long resolveStart = GC.GetAllocatedBytesForCurrentThread();
            long resolveTimestamp = Stopwatch.GetTimestamp();
            double resolveSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            (bool resolved, Vector2 clickPos) = _dependencies.LabelInteraction.TryResolveLabelClickPositionResult(
                nextLabel,
                candidates.NextLabelMechanicId,
                context.WindowTopLeft,
                context.AllLabels);
            long resolveBytes = GC.GetAllocatedBytesForCurrentThread() - resolveStart;
            double resolveMs = GetElapsedMs(resolveTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - resolveSleepBefore);
            RecordStage(PerformanceMonitor.ClickResolveStageIndex, resolveBytes, resolveMs);
            if (!resolved)
                return HandleVisibleLabelResolveFailure(context, candidates, nextLabel);


            if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", candidates.NextLabelMechanicId);

            if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
                _dependencies.ClickDebugPublisher.PublishLabelClickDebug(
                    "LabelCandidate",
                    candidates.NextLabelMechanicId,
                    nextLabel,
                    clickPos,
                    true,
                    $"Label candidate selected (mechanic: {candidates.NextLabelMechanicId ?? "none"})");

            long inputStart = GC.GetAllocatedBytesForCurrentThread();
            long inputTimestamp = Stopwatch.GetTimestamp();
            double inputSleepBefore = ClickPipelineTiming.ReadSleepTimeMs();
            bool clicked = _dependencies.LabelInteraction.PerformResolvedLabelInteraction(clickPos, nextLabel, candidates.NextLabelMechanicId);
            long inputBytes = GC.GetAllocatedBytesForCurrentThread() - inputStart;
            // Exclude the intentional hover/post-click settle sleeps inside the click dispatch so Input shows
            // the true processing cost (the host already reports those waits as the separate Sleep row).
            double inputMs = GetElapsedMs(inputTimestamp) - (ClickPipelineTiming.ReadSleepTimeMs() - inputSleepBefore);
            RecordStage(PerformanceMonitor.ClickInputStageIndex, inputBytes, inputMs);

            if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
            {
                _dependencies.ClickDebugPublisher.PublishLabelClickDebug(
                    clicked ? "ClickSuccess" : "ClickFailed",
                    candidates.NextLabelMechanicId,
                    nextLabel,
                    clickPos,
                    clicked,
                    clicked ? $"Click executed ({candidates.NextLabelMechanicId ?? "visible-label"})" : $"Click rejected ({candidates.NextLabelMechanicId ?? "visible-label"})");

                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(clicked ? "ClickExecuted" : "ClickRejected", clicked ? "Input click executed" : "Input click rejected", candidates.NextLabelMechanicId);
            }

            if (clicked)
            {
                _dependencies.ClickSuccessAnchor?.Mark();
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

            return new ExecutionResult(true, DidActionableWork: true);
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
                    return StopExecution(didActionableWork: true);
                }
            }

            bool shouldContinueEntityPathing = OffscreenPathingMath.ShouldPathfindToEntityAfterClickPointResolveFailure(
                _dependencies.Settings.WalkTowardOffscreenLabels.Value,
                nextLabelItem != null,
                candidates.NextLabelMechanicId);
            if (shouldContinueEntityPathing && nextLabelItem != null)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", candidates.NextLabelMechanicId);
                WalkTowardEntity(nextLabelItem, candidates.NextLabelMechanicId);
            }

            return StopExecution(didActionableWork: shouldContinueEntityPathing && nextLabelItem != null);
        }

        private static Entity? TryGetLabelItemOnGround(LabelOnGround? label)
        {
            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                && rawItem is Entity item
                ? item
                : null;
        }

        private static double GetElapsedMs(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}
