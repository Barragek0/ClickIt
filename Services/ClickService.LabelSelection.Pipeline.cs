using System.Collections;
using System.Collections.Generic;
using ClickIt.Definitions;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        public IEnumerator ProcessRegularClick()
        {
            PublishClickFlowDebugStage("TickStart", "ProcessRegularClick entered");

            if (HasClickableAltars())
            {
                PublishClickFlowDebugStage("AltarBranch", "Clickable altar detected; regular label click path skipped");
                yield return ProcessAltarClicking();
                yield break;
            }

            IEnumerator pipeline = new RegularClickPipeline(this).Run();
            while (pipeline.MoveNext())
            {
                yield return pipeline.Current;
            }
        }

        private readonly record struct ClickContext(
            Vector2 WindowTopLeft,
            Vector2 CursorAbsolute,
            long Now,
            bool IsPostChestLootSettleBlocking,
            string ChestLootSettleReason,
            IReadOnlyList<LabelOnGround>? AllLabels,
            Entity? NextShrine,
            MechanicPriorityContext MechanicPriorityContext,
            bool GroundItemsVisible);

        private readonly record struct ClickCandidates(
            LostShipmentCandidate? LostShipment,
            SettlersOreCandidate? SettlersOre,
            LabelOnGround? NextLabel,
            string? NextLabelMechanicId);

        private readonly record struct DecisionResult(
            bool TrySettlers,
            bool TryLostShipment,
            bool TryShrine,
            bool GroundItemsVisible);

        private readonly record struct ExecutionResult(bool ShouldRunPostActions);

        private interface IClickCandidateProvider
        {
            ClickCandidates Collect(ClickContext context);
        }

        private sealed class RegularClickCandidateProvider(ClickService owner) : IClickCandidateProvider
        {
            public ClickCandidates Collect(ClickContext context)
            {
                LostShipmentCandidate? lostShipment;
                SettlersOreCandidate? settlersOre;

                if (!context.GroundItemsVisible)
                {
                    owner.ResolveHiddenFallbackCandidates(out lostShipment, out settlersOre);
                    owner.PublishClickFlowDebugStage("GroundItemsHidden", "Ground item labels hidden; evaluating non-label fallbacks");
                    return new ClickCandidates(lostShipment, settlersOre, null, null);
                }

                owner.ResolveVisibleMechanicCandidates(out lostShipment, out settlersOre, context.AllLabels);
                if (owner.ShouldCaptureClickDebug())
                {
                    owner.PublishClickFlowDebugStage("LabelSource", owner.BuildLabelSourceDebugSummary(context.AllLabels));
                }

                LabelOnGround? nextLabel = owner.ResolveNextLabelCandidate(context.AllLabels);
                string? nextLabelMechanicId = nextLabel != null
                    ? owner.labelFilterService.GetMechanicIdForLabel(nextLabel)
                    : null;

                nextLabelMechanicId = ClickService.ResolveLabelMechanicIdForVisibleCandidateComparison(
                    nextLabelMechanicId,
                    hasLabel: nextLabel != null,
                    isWorldItemLabel: nextLabel?.ItemOnGround?.Type == ExileCore.Shared.Enums.EntityType.WorldItem,
                    clickItemsEnabled: owner.settings.ClickItems.Value);

                return new ClickCandidates(lostShipment, settlersOre, nextLabel, nextLabelMechanicId);
            }
        }

        private sealed class InteractionPolicyEngine(ClickService owner)
        {
            public DecisionResult Evaluate(ClickContext context, ClickCandidates candidates)
            {
                if (!context.GroundItemsVisible)
                {
                    return new DecisionResult(
                        TrySettlers: ShouldTryHiddenSettlers(context, candidates),
                        TryLostShipment: ShouldTryHiddenLostShipment(context, candidates),
                        TryShrine: ShouldTryHiddenShrine(context),
                        GroundItemsVisible: false);
                }

                return new DecisionResult(
                    TrySettlers: ShouldTryVisibleSettlers(context, candidates),
                    TryLostShipment: ShouldTryVisibleLostShipment(context, candidates),
                    TryShrine: owner.ShouldPreferShrineOverLabel(candidates.NextLabel, context.NextShrine),
                    GroundItemsVisible: true);
            }

            private bool ShouldTryHiddenSettlers(ClickContext context, ClickCandidates candidates)
            {
                if (!candidates.SettlersOre.HasValue)
                    return false;

                return ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                    ClickService.CreateMechanicCandidateSignal(
                        candidates.SettlersOre.Value.MechanicId,
                        candidates.SettlersOre.Value.Distance,
                        GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    MechanicCandidateSignal.None,
                    ClickService.CreateMechanicCandidateSignal(
                        ShrineMechanicId,
                        context.NextShrine?.DistancePlayer,
                        owner.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        LostShipmentMechanicId,
                        candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                        candidates.LostShipment.HasValue ? GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                    context.MechanicPriorityContext);
            }

            private bool ShouldTryHiddenLostShipment(ClickContext context, ClickCandidates candidates)
            {
                if (!candidates.LostShipment.HasValue)
                    return false;

                return ClickService.ShouldPreferLostShipmentOverVisibleCandidates(
                    ClickService.CreateMechanicCandidateSignal(
                        LostShipmentMechanicId,
                        candidates.LostShipment.Value.Distance,
                        GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    MechanicCandidateSignal.None,
                    ClickService.CreateMechanicCandidateSignal(
                        ShrineMechanicId,
                        context.NextShrine?.DistancePlayer,
                        owner.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                    context.MechanicPriorityContext);
            }

            private bool ShouldTryHiddenShrine(ClickContext context)
            {
                return context.NextShrine != null && ClickService.ShouldClickShrineWhenGroundItemsHidden(context.NextShrine);
            }

            private bool ShouldTryVisibleSettlers(ClickContext context, ClickCandidates candidates)
            {
                if (!candidates.SettlersOre.HasValue)
                    return false;

                return ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                    ClickService.CreateMechanicCandidateSignal(
                        candidates.SettlersOre.Value.MechanicId,
                        candidates.SettlersOre.Value.Distance,
                        GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        candidates.NextLabelMechanicId,
                        candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                        TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        ShrineMechanicId,
                        context.NextShrine?.DistancePlayer,
                        owner.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        LostShipmentMechanicId,
                        candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                        candidates.LostShipment.HasValue ? GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                    context.MechanicPriorityContext);
            }

            private bool ShouldTryVisibleLostShipment(ClickContext context, ClickCandidates candidates)
            {
                if (!candidates.LostShipment.HasValue)
                    return false;

                return ClickService.ShouldPreferLostShipmentOverVisibleCandidates(
                    ClickService.CreateMechanicCandidateSignal(
                        LostShipmentMechanicId,
                        candidates.LostShipment.Value.Distance,
                        GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        candidates.NextLabelMechanicId,
                        candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                        TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        ShrineMechanicId,
                        context.NextShrine?.DistancePlayer,
                        owner.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                    context.MechanicPriorityContext);
            }
        }

        private sealed class ClickExecutor(ClickService owner)
        {
            public ExecutionResult Execute(ClickContext context, ClickCandidates candidates, DecisionResult decision)
            {
                return decision.GroundItemsVisible
                    ? ExecuteVisible(context, candidates, decision)
                    : ExecuteHidden(context, candidates, decision);
            }

            private ExecutionResult ExecuteHidden(ClickContext context, ClickCandidates candidates, DecisionResult decision)
            {
                if (decision.TrySettlers && candidates.SettlersOre.HasValue)
                {
                    if (context.IsPostChestLootSettleBlocking
                        && !owner.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(candidates.SettlersOre.Value.MechanicId, candidates.SettlersOre.Value.Entity, out string bypassDecisionSettlersHidden))
                    {
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecisionSettlersHidden}", candidates.SettlersOre.Value.MechanicId);
                    }
                    else if (owner.TryClickSettlersOre(candidates.SettlersOre.Value))
                    {
                        owner.PublishClickFlowDebugStage("HiddenSettlersFallback", "Using hidden settlers candidate", candidates.SettlersOre.Value.MechanicId);
                        return new ExecutionResult(false);
                    }

                    owner.PublishClickFlowDebugStage("HiddenSettlersFallbackSkipped", "Hidden settlers candidate was not targetable/valid at click time", candidates.SettlersOre.Value.MechanicId);
                }

                if (decision.TryLostShipment && candidates.LostShipment.HasValue)
                {
                    if (context.IsPostChestLootSettleBlocking
                        && !owner.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(MechanicIds.LostShipment, candidates.LostShipment.Value.Entity, out string bypassDecisionLostShipmentHidden))
                    {
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecisionLostShipmentHidden}", MechanicIds.LostShipment);
                    }
                    else
                    {
                        owner.TryClickLostShipment(candidates.LostShipment.Value);
                        return new ExecutionResult(false);
                    }
                }

                if (decision.TryShrine && context.NextShrine != null)
                {
                    if (context.IsPostChestLootSettleBlocking
                        && !owner.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(MechanicIds.Shrines, context.NextShrine, out string bypassDecisionShrineHidden))
                    {
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecisionShrineHidden}", MechanicIds.Shrines);
                    }
                    else
                    {
                        owner.TryClickShrine(context.NextShrine);
                        return new ExecutionResult(false);
                    }
                }

                if (context.IsPostChestLootSettleBlocking)
                {
                    string chestLootSettleReason = context.ChestLootSettleReason;
                    owner.DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                    owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason);
                    return new ExecutionResult(false);
                }

                if (owner.settings.WalkTowardOffscreenLabels.Value)
                {
                    owner.TryWalkTowardOffscreenTarget();
                }

                owner.DebugLog(() => "[ProcessRegularClick] Ground items not visible, breaking");
                owner.PublishClickFlowDebugStage("GroundItemsHiddenExit", "No clickable hidden fallback selected");
                return new ExecutionResult(false);
            }

            private ExecutionResult ExecuteVisible(ClickContext context, ClickCandidates candidates, DecisionResult decision)
            {
                if (decision.TrySettlers && candidates.SettlersOre.HasValue)
                {
                    if (context.IsPostChestLootSettleBlocking
                        && !owner.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(candidates.SettlersOre.Value.MechanicId, candidates.SettlersOre.Value.Entity, out string bypassDecisionSettlersVisible))
                    {
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecisionSettlersVisible}", candidates.SettlersOre.Value.MechanicId);
                    }
                    else if (owner.TryClickSettlersOre(candidates.SettlersOre.Value))
                    {
                        return new ExecutionResult(false);
                    }
                }

                if (decision.TryLostShipment && candidates.LostShipment.HasValue)
                {
                    if (context.IsPostChestLootSettleBlocking
                        && !owner.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(MechanicIds.LostShipment, candidates.LostShipment.Value.Entity, out string bypassDecisionLostShipmentVisible))
                    {
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecisionLostShipmentVisible}", MechanicIds.LostShipment);
                    }
                    else
                    {
                        owner.TryClickLostShipment(candidates.LostShipment.Value);
                        return new ExecutionResult(false);
                    }
                }

                if (decision.TryShrine && context.NextShrine != null)
                {
                    if (context.IsPostChestLootSettleBlocking
                        && !owner.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(MechanicIds.Shrines, context.NextShrine, out string bypassDecisionShrineVisible))
                    {
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecisionShrineVisible}", MechanicIds.Shrines);
                    }
                    else
                    {
                        owner.TryClickShrine(context.NextShrine);
                        return new ExecutionResult(false);
                    }
                }

                if (candidates.NextLabel == null)
                {
                    if (context.IsPostChestLootSettleBlocking)
                    {
                        string chestLootSettleReason = context.ChestLootSettleReason;
                        owner.DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason);
                        return new ExecutionResult(false);
                    }

                    owner.labelFilterService.LogSelectionDiagnostics(context.AllLabels, 0, context.AllLabels?.Count ?? 0);
                    if (owner.ShouldCaptureClickDebug())
                    {
                        owner.PublishClickFlowDebugStage("NoLabelCandidate", owner.BuildNoLabelDebugSummary(context.AllLabels));
                    }

                    if (owner.settings.WalkTowardOffscreenLabels.Value && owner.TryHandleStickyOffscreenTarget(context.WindowTopLeft, context.AllLabels))
                    {
                        return new ExecutionResult(false);
                    }

                    if (owner.settings.WalkTowardOffscreenLabels.Value)
                    {
                        owner.TryWalkTowardOffscreenTarget();
                    }

                    owner.DebugLog(() => "[ProcessRegularClick] No label to click found, breaking");
                    owner.PublishClickFlowDebugStage("NoLabelExit", "No label click attempted");
                    return new ExecutionResult(false);
                }

                if (context.IsPostChestLootSettleBlocking
                    && !owner.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(candidates.NextLabelMechanicId, candidates.NextLabel.ItemOnGround, out string bypassDecisionLabel))
                {
                    string chestLootSettleReason = context.ChestLootSettleReason;
                    owner.DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                    owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionLabel}", candidates.NextLabelMechanicId);
                    return new ExecutionResult(false);
                }

                if (owner.ShouldSkipOrHandleSpecialLabel(candidates.NextLabel, context.WindowTopLeft))
                {
                    owner.PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", candidates.NextLabelMechanicId);
                    return new ExecutionResult(false);
                }

                if (!owner.TryResolveLabelClickPosition(
                    candidates.NextLabel,
                    candidates.NextLabelMechanicId,
                    context.WindowTopLeft,
                    context.AllLabels,
                    out Vector2 clickPos))
                {
                    owner.DebugLog(() => "[ProcessRegularClick] Skipping label: no clickable point inside label bounds.");
                    owner.PublishClickFlowDebugStage("ClickPointResolveFailed", "TryCalculateClickPosition returned false", candidates.NextLabelMechanicId);

                    if (candidates.SettlersOre.HasValue
                        && ClickService.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(candidates.NextLabelMechanicId, candidates.SettlersOre.Value.MechanicId))
                    {
                        owner.PublishClickFlowDebugStage("SettlersEntityFallbackAttempt", "Label unresolved; attempting settlers entity click", candidates.SettlersOre.Value.MechanicId);
                        if (context.IsPostChestLootSettleBlocking
                            && !owner.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(candidates.SettlersOre.Value.MechanicId, candidates.SettlersOre.Value.Entity, out string bypassDecisionSettlersFallback))
                        {
                            owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecisionSettlersFallback}", candidates.SettlersOre.Value.MechanicId);
                        }
                        else if (owner.TryClickSettlersOre(candidates.SettlersOre.Value))
                        {
                            owner.PublishClickFlowDebugStage("SettlersEntityFallbackSuccess", "Settlers entity click succeeded after label resolve failure", candidates.SettlersOre.Value.MechanicId);
                            return new ExecutionResult(false);
                        }
                    }

                    bool shouldContinueEntityPathing = ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                        owner.settings.WalkTowardOffscreenLabels.Value,
                        candidates.NextLabel.ItemOnGround != null,
                        candidates.NextLabel.ItemOnGround?.IsHidden == true,
                        candidates.NextLabelMechanicId);
                    if (shouldContinueEntityPathing)
                    {
                        owner.PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", candidates.NextLabelMechanicId);
                        _ = owner.TryWalkTowardOffscreenTarget(candidates.NextLabel.ItemOnGround);
                    }

                    return new ExecutionResult(false);
                }

                owner.PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", candidates.NextLabelMechanicId);

                owner.PublishLabelClickDebug(
                    stage: "LabelCandidate",
                    mechanicId: candidates.NextLabelMechanicId,
                    label: candidates.NextLabel,
                    resolvedClickPos: clickPos,
                    resolved: true,
                    notes: "Settlers label candidate selected from ItemsOnGroundLabelsVisible");

                bool forceUiHoverVerification = ClickService.ShouldForceUiHoverVerificationForLabel(candidates.NextLabel);

                bool clicked = ClickService.ShouldUseHoldClickForSettlersMechanic(candidates.NextLabelMechanicId)
                    ? owner.PerformLabelHoldClick(clickPos, candidates.NextLabel.Label, owner.gameController, holdDurationMs: 0, forceUiHoverVerification)
                    : owner.PerformLabelClick(clickPos, candidates.NextLabel.Label, owner.gameController, forceUiHoverVerification);

                owner.PublishLabelClickDebug(
                    stage: clicked ? "ClickSuccess" : "ClickFailed",
                    mechanicId: candidates.NextLabelMechanicId,
                    label: candidates.NextLabel,
                    resolvedClickPos: clickPos,
                    resolved: clicked,
                    notes: clicked ? "Settlers click completed via label pipeline" : "Settlers click attempt failed via label pipeline");

                owner.PublishClickFlowDebugStage(clicked ? "ClickExecuted" : "ClickRejected", clicked ? "Input click executed" : "Input click rejected", candidates.NextLabelMechanicId);

                if (clicked)
                {
                    if (owner.IsStickyTarget(candidates.NextLabel.ItemOnGround))
                    {
                        owner.ClearStickyOffscreenTarget();
                    }

                    owner.MarkPendingChestOpenConfirmation(candidates.NextLabelMechanicId, candidates.NextLabel);
                    owner.MarkLeverClicked(candidates.NextLabel);
                    if (owner.settings.WalkTowardOffscreenLabels.Value)
                    {
                        owner.pathfindingService.ClearLatestPath();
                    }
                }

                return new ExecutionResult(true);
            }
        }

        private sealed class PostClickHandler(ClickService owner)
        {
            public IEnumerator Run(ExecutionResult executionResult)
            {
                if (!executionResult.ShouldRunPostActions)
                    yield break;

                if (owner.inputHandler.TriggerToggleItems())
                {
                    int blockMs = owner.inputHandler.GetToggleItemsPostClickBlockMs();
                    if (blockMs > 0)
                    {
                        yield return new WaitTime(blockMs);
                    }
                }
            }
        }

        private sealed class RegularClickPipeline(ClickService owner)
        {
            private readonly IClickCandidateProvider candidateProvider = new RegularClickCandidateProvider(owner);
            private readonly InteractionPolicyEngine policyEngine = new(owner);
            private readonly ClickExecutor executor = new(owner);
            private readonly PostClickHandler postClickHandler = new(owner);

            public IEnumerator Run()
            {
                if (!TryBuildContext(out ClickContext context))
                    yield break;

                ClickCandidates candidates = candidateProvider.Collect(context);
                DecisionResult decision = policyEngine.Evaluate(context, candidates);
                ExecutionResult executionResult = executor.Execute(context, candidates, decision);

                IEnumerator postActions = postClickHandler.Run(executionResult);
                while (postActions.MoveNext())
                {
                    yield return postActions.Current;
                }
            }

            private bool TryBuildContext(out ClickContext context)
            {
                RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                Vector2 cursorAbsolute = GetCursorAbsolutePosition();

                try
                {
                    if (owner.TryHandleUltimatumPanelUi(windowTopLeft))
                    {
                        context = default;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    owner.DebugLog(() => $"[ProcessRegularClick] Ultimatum UI handler failed, continuing regular click path: {ex.Message}");
                }

                if (owner.TryGetMovementSkillPostCastBlockState(Environment.TickCount64, out string movementSkillBlockReason))
                {
                    owner.DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while movement skill is still executing ({movementSkillBlockReason}).");
                    owner.PublishClickFlowDebugStage("MovementBlocked", movementSkillBlockReason);
                    context = default;
                    return false;
                }

                long now = Environment.TickCount64;
                bool isPostChestLootSettleBlocking = owner.IsPostChestLootSettlementBlocking(now, out string chestLootSettleReason);
                IReadOnlyList<LabelOnGround>? allLabels = owner.GetLabelsForRegularSelection();
                if (owner.TryHandlePendingChestOpenConfirmation(windowTopLeft, allLabels))
                {
                    context = default;
                    return false;
                }

                Entity? nextShrine = owner.ResolveNextShrineCandidate();
                owner.RefreshMechanicPriorityCaches();
                MechanicPriorityContext mechanicPriorityContext = owner.CreateMechanicPriorityContext();

                context = new ClickContext(
                    windowTopLeft,
                    cursorAbsolute,
                    now,
                    isPostChestLootSettleBlocking,
                    chestLootSettleReason,
                    allLabels,
                    nextShrine,
                    mechanicPriorityContext,
                    owner.groundItemsVisible());

                return true;
            }
        }
    }
}