using System.Collections;
using ClickIt.Definitions;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using SharpDX;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private sealed class RegularClickCoordinator(ClickService owner)
        {
            private readonly IClickCandidateProvider _acquisitionPhase = new RegularClickCandidateProvider(owner);
            private readonly CandidateRankingPhase _rankingPhase = new(owner);
            private readonly CandidateGatingPhase _gatingPhase = new();
            private readonly ClickExecutor _executionPhase = new(owner);
            private readonly PostClickHandler _postActionsPhase = new(owner);

            public IEnumerator Run()
            {
                owner.PublishClickFlowDebugStage("TickStart", "ProcessRegularClick entered");

                if (owner.HasClickableAltars())
                {
                    owner.PublishClickFlowDebugStage("AltarBranch", "Clickable altar detected; regular label click path skipped");
                    return owner.ProcessAltarClicking();
                }

                return RunCore();
            }

            private IEnumerator RunCore()
            {
                if (!owner.TickContextFactory.TryCreateRegularClickContext(out ClickTickContext context))
                    yield break;

                ClickCandidates candidates = _acquisitionPhase.Collect(context);
                RankingResult ranking = _rankingPhase.Rank(context, candidates);
                DecisionResult decision = _gatingPhase.Gate(candidates, ranking);
                ExecutionResult executionResult = _executionPhase.Execute(context, candidates, decision);

                IEnumerator postActions = _postActionsPhase.Run(executionResult);
                while (postActions.MoveNext())
                {
                    yield return postActions.Current;
                }
            }

            private readonly record struct ClickCandidates(
                LostShipmentCandidate? LostShipment,
                SettlersOreCandidate? SettlersOre,
                LabelOnGround? NextLabel,
                string? NextLabelMechanicId);

            private readonly record struct RankingResult(
                bool PreferSettlers,
                bool PreferLostShipment,
                bool PreferShrine,
                bool GroundItemsVisible);

            private readonly record struct DecisionResult(
                bool TrySettlers,
                bool TryLostShipment,
                bool TryShrine,
                bool GroundItemsVisible);

            private readonly record struct ExecutionResult(bool ShouldRunPostActions);

            private interface IClickCandidateProvider
            {
                ClickCandidates Collect(ClickTickContext context);
            }

            private sealed class RegularClickCandidateProvider(ClickService owner) : IClickCandidateProvider
            {
                public ClickCandidates Collect(ClickTickContext context)
                {
                    LostShipmentCandidate? lostShipment;
                    SettlersOreCandidate? settlersOre;

                    if (!context.GroundItemsVisible)
                    {
                        owner.VisibleMechanics.ResolveHiddenFallbackCandidates(out lostShipment, out settlersOre);
                        owner.PublishClickFlowDebugStage("GroundItemsHidden", "Ground item labels hidden; evaluating non-label fallbacks");
                        return new ClickCandidates(lostShipment, settlersOre, null, null);
                    }

                    owner.VisibleMechanics.ResolveVisibleMechanicCandidates(out lostShipment, out settlersOre, context.AllLabels);
                    if (owner.ShouldCaptureClickDebug())
                    {
                        owner.PublishClickFlowDebugStage("LabelSource", owner.BuildLabelSourceDebugSummary(context.AllLabels));
                    }

                    LabelOnGround? nextLabel = owner.LabelSelection.ResolveNextLabelCandidate(context.AllLabels);
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

            private sealed class CandidateRankingPhase(ClickService owner)
            {
                public RankingResult Rank(ClickTickContext context, ClickCandidates candidates)
                {
                    if (!context.GroundItemsVisible)
                    {
                        return new RankingResult(
                            PreferSettlers: ShouldTryHiddenSettlers(context, candidates),
                            PreferLostShipment: ShouldTryHiddenLostShipment(context, candidates),
                            PreferShrine: ShouldTryHiddenShrine(context),
                            GroundItemsVisible: false);
                    }

                    return new RankingResult(
                        PreferSettlers: ShouldTryVisibleSettlers(context, candidates),
                        PreferLostShipment: ShouldTryVisibleLostShipment(context, candidates),
                        PreferShrine: owner.LabelSelection.ShouldPreferShrineOverLabel(candidates.NextLabel, context.NextShrine),
                        GroundItemsVisible: true);
                }

                private bool ShouldTryHiddenSettlers(ClickTickContext context, ClickCandidates candidates)
                {
                    if (!candidates.SettlersOre.HasValue)
                        return false;

                    return ShouldPreferSettlersWithSharedRankingEngine(
                        ClickService.CreateMechanicCandidateSignal(
                            candidates.SettlersOre.Value.MechanicId,
                            candidates.SettlersOre.Value.Distance,
                            ClickService.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                        MechanicCandidateSignal.None,
                        ClickService.CreateMechanicCandidateSignal(
                            ShrineMechanicId,
                            context.NextShrine?.DistancePlayer,
                            owner.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                        ClickService.CreateMechanicCandidateSignal(
                            LostShipmentMechanicId,
                            candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                            candidates.LostShipment.HasValue ? ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                        context.MechanicPriorityContext);
                }

                private bool ShouldTryHiddenLostShipment(ClickTickContext context, ClickCandidates candidates)
                {
                    if (!candidates.LostShipment.HasValue)
                        return false;

                    return ShouldPreferLostShipmentWithSharedRankingEngine(
                        ClickService.CreateMechanicCandidateSignal(
                            LostShipmentMechanicId,
                            candidates.LostShipment.Value.Distance,
                            ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                        MechanicCandidateSignal.None,
                        ClickService.CreateMechanicCandidateSignal(
                            ShrineMechanicId,
                            context.NextShrine?.DistancePlayer,
                            owner.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                        context.MechanicPriorityContext);
                }

                private static bool ShouldTryHiddenShrine(ClickTickContext context)
                {
                    return context.NextShrine != null && ClickService.ShouldClickShrineWhenGroundItemsHidden(context.NextShrine);
                }

                private bool ShouldTryVisibleSettlers(ClickTickContext context, ClickCandidates candidates)
                {
                    if (!candidates.SettlersOre.HasValue)
                        return false;

                    return ShouldPreferSettlersWithSharedRankingEngine(
                        ClickService.CreateMechanicCandidateSignal(
                            candidates.SettlersOre.Value.MechanicId,
                            candidates.SettlersOre.Value.Distance,
                            ClickService.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                        ClickService.CreateMechanicCandidateSignal(
                            candidates.NextLabelMechanicId,
                            candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                            ClickService.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                        ClickService.CreateMechanicCandidateSignal(
                            ShrineMechanicId,
                            context.NextShrine?.DistancePlayer,
                            owner.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                        ClickService.CreateMechanicCandidateSignal(
                            LostShipmentMechanicId,
                            candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                            candidates.LostShipment.HasValue ? ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                        context.MechanicPriorityContext);
                }

                private bool ShouldTryVisibleLostShipment(ClickTickContext context, ClickCandidates candidates)
                {
                    if (!candidates.LostShipment.HasValue)
                        return false;

                    return ShouldPreferLostShipmentWithSharedRankingEngine(
                        ClickService.CreateMechanicCandidateSignal(
                            LostShipmentMechanicId,
                            candidates.LostShipment.Value.Distance,
                            ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                        ClickService.CreateMechanicCandidateSignal(
                            candidates.NextLabelMechanicId,
                            candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                            ClickService.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                        ClickService.CreateMechanicCandidateSignal(
                            ShrineMechanicId,
                            context.NextShrine?.DistancePlayer,
                            owner.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                        context.MechanicPriorityContext);
                }
            }

            private sealed class CandidateGatingPhase
            {
                public DecisionResult Gate(ClickCandidates candidates, RankingResult ranking)
                {
                    return new DecisionResult(
                        TrySettlers: ranking.PreferSettlers && candidates.SettlersOre.HasValue,
                        TryLostShipment: ranking.PreferLostShipment && candidates.LostShipment.HasValue,
                        TryShrine: ranking.PreferShrine,
                        GroundItemsVisible: ranking.GroundItemsVisible);
                }
            }

            private sealed class ClickExecutor(ClickService owner)
            {
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

                    if (owner.ChestLootSettlement.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out string bypassDecision))
                        return false;

                    owner.PublishClickFlowDebugStage(
                        "PostChestLootSettleBlocked",
                        $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecision}",
                        mechanicId);
                    return true;
                }

                private bool TryExecuteSettlersHidden(ClickTickContext context, SettlersOreCandidate candidate)
                {
                    if (!IsBlockedByPostChestLootSettlement(context, candidate.MechanicId, candidate.Entity)
                        && owner.VisibleMechanics.TryClickSettlersOre(candidate))
                    {
                        owner.PublishClickFlowDebugStage("HiddenSettlersFallback", "Using hidden settlers candidate", candidate.MechanicId);
                        return true;
                    }

                    owner.PublishClickFlowDebugStage("HiddenSettlersFallbackSkipped", "Hidden settlers candidate was not targetable/valid at click time", candidate.MechanicId);
                    return false;
                }

                private bool TryExecuteSettlersVisible(ClickTickContext context, SettlersOreCandidate candidate)
                {
                    return !IsBlockedByPostChestLootSettlement(context, candidate.MechanicId, candidate.Entity)
                        && owner.VisibleMechanics.TryClickSettlersOre(candidate);
                }

                private bool TryExecuteLostShipment(ClickTickContext context, LostShipmentCandidate candidate)
                {
                    if (IsBlockedByPostChestLootSettlement(context, MechanicIds.LostShipment, candidate.Entity))
                        return false;

                    owner.VisibleMechanics.TryClickLostShipment(candidate);
                    return true;
                }

                private bool TryExecuteShrine(ClickTickContext context)
                {
                    if (context.NextShrine == null)
                        return false;
                    if (IsBlockedByPostChestLootSettlement(context, MechanicIds.Shrines, context.NextShrine))
                        return false;

                    owner.VisibleMechanics.TryClickShrine(context.NextShrine);
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
                        owner.DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason);
                        return StopExecution();
                    }

                    if (owner.settings.WalkTowardOffscreenLabels.Value)
                    {
                        owner.OffscreenPathing.TryWalkTowardOffscreenTarget();
                    }

                    owner.DebugLog(() => "[ProcessRegularClick] Ground items not visible, breaking");
                    owner.PublishClickFlowDebugStage("GroundItemsHiddenExit", "No clickable hidden fallback selected");
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
                        owner.DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                        owner.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason);
                        return StopExecution();
                    }

                    owner.labelFilterService.LogSelectionDiagnostics(context.AllLabels, 0, context.AllLabels?.Count ?? 0);
                    if (owner.ShouldCaptureClickDebug())
                    {
                        owner.PublishClickFlowDebugStage("NoLabelCandidate", owner.BuildNoLabelDebugSummary(context.AllLabels));
                    }

                    if (owner.settings.WalkTowardOffscreenLabels.Value && owner.OffscreenPathing.TryHandleStickyOffscreenTarget(context.WindowTopLeft, context.AllLabels))
                    {
                        return StopExecution();
                    }

                    if (owner.settings.WalkTowardOffscreenLabels.Value)
                    {
                        owner.OffscreenPathing.TryWalkTowardOffscreenTarget();
                    }

                    owner.DebugLog(() => "[ProcessRegularClick] No label to click found, breaking");
                    owner.PublishClickFlowDebugStage("NoLabelExit", "No label click attempted");
                    return StopExecution();
                }

                private ExecutionResult HandleVisibleLabel(ClickTickContext context, ClickCandidates candidates)
                {
                    LabelOnGround nextLabel = candidates.NextLabel!;

                    if (IsBlockedByPostChestLootSettlement(context, candidates.NextLabelMechanicId, nextLabel.ItemOnGround))
                    {
                        string chestLootSettleReason = context.ChestLootSettleReason;
                        owner.DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                        return StopExecution();
                    }

                    if (owner.LabelSelection.ShouldSkipOrHandleSpecialLabel(nextLabel, context.WindowTopLeft))
                    {
                        owner.PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", candidates.NextLabelMechanicId);
                        return StopExecution();
                    }

                    if (!owner.TryResolveLabelClickPosition(
                        nextLabel,
                        candidates.NextLabelMechanicId,
                        context.WindowTopLeft,
                        context.AllLabels,
                        out Vector2 clickPos))
                    {
                        return HandleVisibleLabelResolveFailure(context, candidates, nextLabel);
                    }

                    owner.PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", candidates.NextLabelMechanicId);

                    owner.PublishLabelClickDebug(
                        stage: "LabelCandidate",
                        mechanicId: candidates.NextLabelMechanicId,
                        label: nextLabel,
                        resolvedClickPos: clickPos,
                        resolved: true,
                        notes: "Settlers label candidate selected from ItemsOnGroundLabelsVisible");

                    bool forceUiHoverVerification = ClickService.ShouldForceUiHoverVerificationForLabel(nextLabel);

                    bool clicked = owner.ClickActions.ExecuteLabelInteraction(
                        clickPos,
                        nextLabel.Label,
                        owner.gameController,
                        useHoldClick: ClickService.ShouldUseHoldClickForSettlersMechanic(candidates.NextLabelMechanicId),
                        holdDurationMs: 0,
                        forceUiHoverVerification: forceUiHoverVerification);

                    owner.PublishLabelClickDebug(
                        stage: clicked ? "ClickSuccess" : "ClickFailed",
                        mechanicId: candidates.NextLabelMechanicId,
                        label: nextLabel,
                        resolvedClickPos: clickPos,
                        resolved: clicked,
                        notes: clicked ? "Settlers click completed via label pipeline" : "Settlers click attempt failed via label pipeline");

                    owner.PublishClickFlowDebugStage(clicked ? "ClickExecuted" : "ClickRejected", clicked ? "Input click executed" : "Input click rejected", candidates.NextLabelMechanicId);

                    if (clicked)
                    {
                        if (owner.OffscreenPathing.IsStickyTarget(nextLabel.ItemOnGround))
                        {
                            owner.OffscreenPathing.ClearStickyOffscreenTarget();
                        }

                        owner.ChestLootSettlement.MarkPendingChestOpenConfirmation(candidates.NextLabelMechanicId, nextLabel);
                        owner.LabelSelection.MarkLeverClicked(nextLabel);
                        if (owner.settings.WalkTowardOffscreenLabels.Value)
                        {
                            owner.pathfindingService.ClearLatestPath();
                        }
                    }

                    return new ExecutionResult(true);
                }

                private ExecutionResult HandleVisibleLabelResolveFailure(ClickTickContext context, ClickCandidates candidates, LabelOnGround nextLabel)
                {
                    owner.DebugLog(() => "[ProcessRegularClick] Skipping label: no clickable point inside label bounds.");
                    owner.PublishClickFlowDebugStage("ClickPointResolveFailed", "TryCalculateClickPosition returned false", candidates.NextLabelMechanicId);

                    if (candidates.SettlersOre.HasValue
                        && ClickService.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(candidates.NextLabelMechanicId, candidates.SettlersOre.Value.MechanicId))
                    {
                        owner.PublishClickFlowDebugStage("SettlersEntityFallbackAttempt", "Label unresolved; attempting settlers entity click", candidates.SettlersOre.Value.MechanicId);
                        if (!IsBlockedByPostChestLootSettlement(context, candidates.SettlersOre.Value.MechanicId, candidates.SettlersOre.Value.Entity)
                            && owner.VisibleMechanics.TryClickSettlersOre(candidates.SettlersOre.Value))
                        {
                            owner.PublishClickFlowDebugStage("SettlersEntityFallbackSuccess", "Settlers entity click succeeded after label resolve failure", candidates.SettlersOre.Value.MechanicId);
                            return StopExecution();
                        }
                    }

                    bool shouldContinueEntityPathing = ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                        owner.settings.WalkTowardOffscreenLabels.Value,
                        nextLabel.ItemOnGround != null,
                        nextLabel.ItemOnGround?.IsHidden == true,
                        candidates.NextLabelMechanicId);
                    if (shouldContinueEntityPathing)
                    {
                        owner.PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", candidates.NextLabelMechanicId);
                        _ = owner.OffscreenPathing.TryWalkTowardOffscreenTarget(nextLabel.ItemOnGround);
                    }

                    return StopExecution();
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
        }
    }
}