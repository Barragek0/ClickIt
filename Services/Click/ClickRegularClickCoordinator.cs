using System.Collections;
using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using SharpDX;

namespace ClickIt.Services
{
    internal readonly record struct RegularClickCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        InputHandler InputHandler,
        LabelFilterService LabelFilterService,
        PathfindingService PathfindingService,
        ClickTickContextFactory TickContextFactory,
        VisibleMechanicCoordinator VisibleMechanics,
        LabelSelectionCoordinator LabelSelection,
        ChestLootSettlementTracker ChestLootSettlement,
        OffscreenPathingCoordinator OffscreenPathing,
        Func<bool> HasClickableAltars,
        Func<IEnumerator> ProcessAltarClicking,
        Action<string, string, string?> PublishClickFlowDebugStage,
        Func<bool> ShouldCaptureClickDebug,
        Func<IReadOnlyList<LabelOnGround>?, string> BuildLabelSourceDebugSummary,
        Func<IReadOnlyList<LabelOnGround>?, string> BuildNoLabelDebugSummary,
        Func<Entity?, Vector2, Vector2, float?> TryGetCursorDistanceSquaredToEntity,
        Func<LabelOnGround, string?, Vector2, IReadOnlyList<LabelOnGround>?, (bool Success, Vector2 ClickPos)> TryResolveLabelClickPosition,
        Func<Vector2, LabelOnGround, string?, bool> ExecuteVisibleLabelInteraction,
        Action<string, string?, LabelOnGround, Vector2, bool, string> PublishLabelClickDebug,
        Action<string> DebugLog);

    internal sealed class RegularClickCoordinator
    {
        private readonly RegularClickCoordinatorDependencies _dependencies;
        private readonly IClickCandidateProvider _acquisitionPhase;
        private readonly CandidateRankingPhase _rankingPhase;
        private readonly CandidateGatingPhase _gatingPhase;
        private readonly ClickExecutor _executionPhase;
        private readonly PostClickHandler _postActionsPhase;

        public RegularClickCoordinator(RegularClickCoordinatorDependencies dependencies)
        {
            _dependencies = dependencies;
            _acquisitionPhase = new RegularClickCandidateProvider(this);
            _rankingPhase = new CandidateRankingPhase(this);
            _gatingPhase = new CandidateGatingPhase();
            _executionPhase = new ClickExecutor(this);
            _postActionsPhase = new PostClickHandler(this);
        }

        public IEnumerator Run()
        {
            _dependencies.PublishClickFlowDebugStage("TickStart", "ProcessRegularClick entered", null);

            if (_dependencies.HasClickableAltars())
            {
                _dependencies.PublishClickFlowDebugStage("AltarBranch", "Clickable altar detected; regular label click path skipped", null);
                return _dependencies.ProcessAltarClicking();
            }

            return RunCore();
        }

        private IEnumerator RunCore()
        {
            if (!_dependencies.TickContextFactory.TryCreateRegularClickContext(out ClickTickContext context))
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

        private sealed class RegularClickCandidateProvider(RegularClickCoordinator owner) : IClickCandidateProvider
        {
            public ClickCandidates Collect(ClickTickContext context)
            {
                LostShipmentCandidate? lostShipment;
                SettlersOreCandidate? settlersOre;

                if (!context.GroundItemsVisible)
                {
                    owner._dependencies.VisibleMechanics.ResolveHiddenFallbackCandidates(out lostShipment, out settlersOre);
                    owner._dependencies.PublishClickFlowDebugStage("GroundItemsHidden", "Ground item labels hidden; evaluating non-label fallbacks", null);
                    return new ClickCandidates(lostShipment, settlersOre, null, null);
                }

                owner._dependencies.VisibleMechanics.ResolveVisibleMechanicCandidates(out lostShipment, out settlersOre, context.AllLabels);
                if (owner._dependencies.ShouldCaptureClickDebug())
                {
                    owner._dependencies.PublishClickFlowDebugStage("LabelSource", owner._dependencies.BuildLabelSourceDebugSummary(context.AllLabels), null);
                }

                LabelOnGround? nextLabel = owner._dependencies.LabelSelection.ResolveNextLabelCandidate(context.AllLabels);
                string? nextLabelMechanicId = nextLabel != null
                    ? owner._dependencies.LabelFilterService.GetMechanicIdForLabel(nextLabel)
                    : null;

                nextLabelMechanicId = ClickService.ResolveLabelMechanicIdForVisibleCandidateComparison(
                    nextLabelMechanicId,
                    hasLabel: nextLabel != null,
                    isWorldItemLabel: nextLabel?.ItemOnGround?.Type == ExileCore.Shared.Enums.EntityType.WorldItem,
                    clickItemsEnabled: owner._dependencies.Settings.ClickItems.Value);

                return new ClickCandidates(lostShipment, settlersOre, nextLabel, nextLabelMechanicId);
            }
        }

        private sealed class CandidateRankingPhase(RegularClickCoordinator owner)
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
                    PreferShrine: owner._dependencies.LabelSelection.ShouldPreferShrineOverLabel(candidates.NextLabel, context.NextShrine),
                    GroundItemsVisible: true);
            }

            private bool ShouldTryHiddenSettlers(ClickTickContext context, ClickCandidates candidates)
            {
                if (!candidates.SettlersOre.HasValue)
                    return false;

                return ClickService.ShouldPreferSettlersWithSharedRankingEngine(
                    ClickService.CreateMechanicCandidateSignal(
                        candidates.SettlersOre.Value.MechanicId,
                        candidates.SettlersOre.Value.Distance,
                        ClickService.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    MechanicCandidateSignal.None,
                    ClickService.CreateMechanicCandidateSignal(
                        ClickService.ShrineMechanicId,
                        context.NextShrine?.DistancePlayer,
                        owner._dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        ClickService.LostShipmentMechanicId,
                        candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                        candidates.LostShipment.HasValue ? ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                    context.MechanicPriorityContext);
            }

            private bool ShouldTryHiddenLostShipment(ClickTickContext context, ClickCandidates candidates)
            {
                if (!candidates.LostShipment.HasValue)
                    return false;

                return ClickService.ShouldPreferLostShipmentWithSharedRankingEngine(
                    ClickService.CreateMechanicCandidateSignal(
                        ClickService.LostShipmentMechanicId,
                        candidates.LostShipment.Value.Distance,
                        ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    MechanicCandidateSignal.None,
                    ClickService.CreateMechanicCandidateSignal(
                        ClickService.ShrineMechanicId,
                        context.NextShrine?.DistancePlayer,
                        owner._dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
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

                return ClickService.ShouldPreferSettlersWithSharedRankingEngine(
                    ClickService.CreateMechanicCandidateSignal(
                        candidates.SettlersOre.Value.MechanicId,
                        candidates.SettlersOre.Value.Distance,
                        ClickService.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        candidates.NextLabelMechanicId,
                        candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                        ClickService.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        ClickService.ShrineMechanicId,
                        context.NextShrine?.DistancePlayer,
                        owner._dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        ClickService.LostShipmentMechanicId,
                        candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                        candidates.LostShipment.HasValue ? ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                    context.MechanicPriorityContext);
            }

            private bool ShouldTryVisibleLostShipment(ClickTickContext context, ClickCandidates candidates)
            {
                if (!candidates.LostShipment.HasValue)
                    return false;

                return ClickService.ShouldPreferLostShipmentWithSharedRankingEngine(
                    ClickService.CreateMechanicCandidateSignal(
                        ClickService.LostShipmentMechanicId,
                        candidates.LostShipment.Value.Distance,
                        ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        candidates.NextLabelMechanicId,
                        candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                        ClickService.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                    ClickService.CreateMechanicCandidateSignal(
                        ClickService.ShrineMechanicId,
                        context.NextShrine?.DistancePlayer,
                        owner._dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
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

        private sealed class ClickExecutor(RegularClickCoordinator owner)
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

                if (owner._dependencies.ChestLootSettlement.ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out string bypassDecision))
                    return false;

                owner._dependencies.PublishClickFlowDebugStage(
                    "PostChestLootSettleBlocked",
                    $"{context.ChestLootSettleReason} | nearby-bypass:{bypassDecision}",
                    mechanicId);
                return true;
            }

            private bool TryExecuteSettlersHidden(ClickTickContext context, SettlersOreCandidate candidate)
            {
                if (!IsBlockedByPostChestLootSettlement(context, candidate.MechanicId, candidate.Entity)
                    && owner._dependencies.VisibleMechanics.TryClickSettlersOre(candidate))
                {
                    owner._dependencies.PublishClickFlowDebugStage("HiddenSettlersFallback", "Using hidden settlers candidate", candidate.MechanicId);
                    return true;
                }

                owner._dependencies.PublishClickFlowDebugStage("HiddenSettlersFallbackSkipped", "Hidden settlers candidate was not targetable/valid at click time", candidate.MechanicId);
                return false;
            }

            private bool TryExecuteSettlersVisible(ClickTickContext context, SettlersOreCandidate candidate)
            {
                return !IsBlockedByPostChestLootSettlement(context, candidate.MechanicId, candidate.Entity)
                    && owner._dependencies.VisibleMechanics.TryClickSettlersOre(candidate);
            }

            private bool TryExecuteLostShipment(ClickTickContext context, LostShipmentCandidate candidate)
            {
                if (IsBlockedByPostChestLootSettlement(context, MechanicIds.LostShipment, candidate.Entity))
                    return false;

                owner._dependencies.VisibleMechanics.TryClickLostShipment(candidate);
                return true;
            }

            private bool TryExecuteShrine(ClickTickContext context)
            {
                if (context.NextShrine == null)
                    return false;
                if (IsBlockedByPostChestLootSettlement(context, MechanicIds.Shrines, context.NextShrine))
                    return false;

                owner._dependencies.VisibleMechanics.TryClickShrine(context.NextShrine);
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
                    owner._dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                    owner._dependencies.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason, null);
                    return StopExecution();
                }

                if (owner._dependencies.Settings.WalkTowardOffscreenLabels.Value)
                {
                    owner._dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget();
                }

                owner._dependencies.DebugLog("[ProcessRegularClick] Ground items not visible, breaking");
                owner._dependencies.PublishClickFlowDebugStage("GroundItemsHiddenExit", "No clickable hidden fallback selected", null);
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
                    owner._dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                    owner._dependencies.PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason, null);
                    return StopExecution();
                }

                owner._dependencies.LabelFilterService.LogSelectionDiagnostics(context.AllLabels, 0, context.AllLabels?.Count ?? 0);
                if (owner._dependencies.ShouldCaptureClickDebug())
                {
                    owner._dependencies.PublishClickFlowDebugStage("NoLabelCandidate", owner._dependencies.BuildNoLabelDebugSummary(context.AllLabels), null);
                }

                if (owner._dependencies.Settings.WalkTowardOffscreenLabels.Value
                    && owner._dependencies.OffscreenPathing.TryHandleStickyOffscreenTarget(context.WindowTopLeft, context.AllLabels))
                {
                    return StopExecution();
                }

                if (owner._dependencies.Settings.WalkTowardOffscreenLabels.Value)
                {
                    owner._dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget();
                }

                owner._dependencies.DebugLog("[ProcessRegularClick] No label to click found, breaking");
                owner._dependencies.PublishClickFlowDebugStage("NoLabelExit", "No label click attempted", null);
                return StopExecution();
            }

            private ExecutionResult HandleVisibleLabel(ClickTickContext context, ClickCandidates candidates)
            {
                LabelOnGround nextLabel = candidates.NextLabel!;

                if (IsBlockedByPostChestLootSettlement(context, candidates.NextLabelMechanicId, nextLabel.ItemOnGround))
                {
                    string chestLootSettleReason = context.ChestLootSettleReason;
                    owner._dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                    return StopExecution();
                }

                if (owner._dependencies.LabelSelection.ShouldSkipOrHandleSpecialLabel(nextLabel, context.WindowTopLeft))
                {
                    owner._dependencies.PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", candidates.NextLabelMechanicId);
                    return StopExecution();
                }

                (bool resolved, Vector2 clickPos) = owner._dependencies.TryResolveLabelClickPosition(
                    nextLabel,
                    candidates.NextLabelMechanicId,
                    context.WindowTopLeft,
                    context.AllLabels);
                if (!resolved)
                {
                    return HandleVisibleLabelResolveFailure(context, candidates, nextLabel);
                }

                owner._dependencies.PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", candidates.NextLabelMechanicId);

                owner._dependencies.PublishLabelClickDebug(
                    "LabelCandidate",
                    candidates.NextLabelMechanicId,
                    nextLabel,
                    clickPos,
                    true,
                    "Settlers label candidate selected from ItemsOnGroundLabelsVisible");

                bool clicked = owner._dependencies.ExecuteVisibleLabelInteraction(clickPos, nextLabel, candidates.NextLabelMechanicId);

                owner._dependencies.PublishLabelClickDebug(
                    clicked ? "ClickSuccess" : "ClickFailed",
                    candidates.NextLabelMechanicId,
                    nextLabel,
                    clickPos,
                    clicked,
                    clicked ? "Settlers click completed via label pipeline" : "Settlers click attempt failed via label pipeline");

                owner._dependencies.PublishClickFlowDebugStage(clicked ? "ClickExecuted" : "ClickRejected", clicked ? "Input click executed" : "Input click rejected", candidates.NextLabelMechanicId);

                if (clicked)
                {
                    if (owner._dependencies.OffscreenPathing.IsStickyTarget(nextLabel.ItemOnGround))
                    {
                        owner._dependencies.OffscreenPathing.ClearStickyOffscreenTarget();
                    }

                    owner._dependencies.ChestLootSettlement.MarkPendingChestOpenConfirmation(candidates.NextLabelMechanicId, nextLabel);
                    owner._dependencies.LabelSelection.MarkLeverClicked(nextLabel);
                    if (owner._dependencies.Settings.WalkTowardOffscreenLabels.Value)
                    {
                        owner._dependencies.PathfindingService.ClearLatestPath();
                    }
                }

                return new ExecutionResult(true);
            }

            private ExecutionResult HandleVisibleLabelResolveFailure(ClickTickContext context, ClickCandidates candidates, LabelOnGround nextLabel)
            {
                owner._dependencies.DebugLog("[ProcessRegularClick] Skipping label: no clickable point inside label bounds.");
                owner._dependencies.PublishClickFlowDebugStage("ClickPointResolveFailed", "TryCalculateClickPosition returned false", candidates.NextLabelMechanicId);

                if (candidates.SettlersOre.HasValue
                    && ClickService.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(candidates.NextLabelMechanicId, candidates.SettlersOre.Value.MechanicId))
                {
                    owner._dependencies.PublishClickFlowDebugStage("SettlersEntityFallbackAttempt", "Label unresolved; attempting settlers entity click", candidates.SettlersOre.Value.MechanicId);
                    if (!IsBlockedByPostChestLootSettlement(context, candidates.SettlersOre.Value.MechanicId, candidates.SettlersOre.Value.Entity)
                        && owner._dependencies.VisibleMechanics.TryClickSettlersOre(candidates.SettlersOre.Value))
                    {
                        owner._dependencies.PublishClickFlowDebugStage("SettlersEntityFallbackSuccess", "Settlers entity click succeeded after label resolve failure", candidates.SettlersOre.Value.MechanicId);
                        return StopExecution();
                    }
                }

                bool shouldContinueEntityPathing = ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                    owner._dependencies.Settings.WalkTowardOffscreenLabels.Value,
                    nextLabel.ItemOnGround != null,
                    nextLabel.ItemOnGround?.IsHidden == true,
                    candidates.NextLabelMechanicId);
                if (shouldContinueEntityPathing)
                {
                    owner._dependencies.PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", candidates.NextLabelMechanicId);
                    _ = owner._dependencies.OffscreenPathing.TryWalkTowardOffscreenTarget(nextLabel.ItemOnGround);
                }

                return StopExecution();
            }
        }

        private sealed class PostClickHandler(RegularClickCoordinator owner)
        {
            public IEnumerator Run(ExecutionResult executionResult)
            {
                if (!executionResult.ShouldRunPostActions)
                    yield break;

                if (owner._dependencies.InputHandler.TriggerToggleItems())
                {
                    int blockMs = owner._dependencies.InputHandler.GetToggleItemsPostClickBlockMs();
                    if (blockMs > 0)
                    {
                        yield return new WaitTime(blockMs);
                    }
                }
            }
        }
    }
}