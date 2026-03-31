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
    internal readonly record struct ClickRuntimeEngineDependencies(
        ClickItSettings Settings,
        GameController GameController,
        InputHandler InputHandler,
        LabelFilterService LabelFilterService,
        PathfindingService PathfindingService,
        ClickTickContextFactory TickContextFactory,
        VisibleMechanicCoordinator VisibleMechanics,
        LabelSelectionCoordinator LabelSelection,
        ChestLootSettlementTracker ChestLootSettlement,
        OffscreenTraversalEngine OffscreenPathing,
        Func<bool> HasClickableAltars,
        Func<IEnumerator> ProcessAltarClicking,
        PublishClickFlowDebugStageDelegate PublishClickFlowDebugStage,
        Func<bool> ShouldCaptureClickDebug,
        Func<IReadOnlyList<LabelOnGround>?, string> BuildLabelSourceDebugSummary,
        Func<IReadOnlyList<LabelOnGround>?, string> BuildNoLabelDebugSummary,
        ResolveCursorDistanceToEntityDelegate TryGetCursorDistanceSquaredToEntity,
        ResolveLabelClickPositionDelegate TryResolveLabelClickPosition,
        ExecuteVisibleLabelInteractionDelegate ExecuteVisibleLabelInteraction,
        PublishLabelClickDebugDelegate PublishLabelClickDebug,
        Action<string> DebugLog);

    internal readonly record struct ClickCandidates(
        LostShipmentCandidate? LostShipment,
        SettlersOreCandidate? SettlersOre,
        LabelOnGround? NextLabel,
        string? NextLabelMechanicId);

    internal readonly record struct RankingResult(
        bool PreferSettlers,
        bool PreferLostShipment,
        bool PreferShrine,
        bool GroundItemsVisible);

    internal readonly record struct DecisionResult(
        bool TrySettlers,
        bool TryLostShipment,
        bool TryShrine,
        bool GroundItemsVisible);

    internal readonly record struct ExecutionResult(bool ShouldRunPostActions);

    internal sealed class ClickRuntimeEngine
    {
        private readonly ClickRuntimeEngineDependencies _dependencies;
        private readonly CandidateAcquisitionEngine _acquisitionPhase;
        private readonly CandidateRankingEngine _rankingPhase;
        private readonly CandidateGatingPhase _gatingPhase;
        private readonly InteractionExecutionEngine _executionPhase;
        private readonly PostInteractionStateEngine _postActionsPhase;

        internal ClickRuntimeEngineDependencies Dependencies => _dependencies;

        public ClickRuntimeEngine(ClickRuntimeEngineDependencies dependencies)
        {
            _dependencies = dependencies;
            _acquisitionPhase = new CandidateAcquisitionEngine(this);
            _rankingPhase = new CandidateRankingEngine(this);
            _gatingPhase = new CandidateGatingPhase();
            _executionPhase = new InteractionExecutionEngine(this);
            _postActionsPhase = new PostInteractionStateEngine(this);
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
    }
}