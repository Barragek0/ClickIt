using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Features.Click.Core
{
    internal readonly record struct MovementSkillPostCastBlockState(bool IsBlocking, string Reason);

    internal readonly record struct ChestLootSettlementBlockState(bool IsBlocking, string Reason);

    internal sealed class ClickTickContextFactoryDependencies(
        Func<RectangleF> getWindowRectangle,
        Func<Vector2> getCursorAbsolutePosition,
        Func<Vector2, bool> tryHandleUltimatumPanelUi,
        Action<string> debugLog,
        Func<long, MovementSkillPostCastBlockState> getMovementSkillPostCastBlockState,
        Func<long, ChestLootSettlementBlockState> getChestLootSettlementBlockState,
        Func<IReadOnlyList<LabelOnGround>?> getLabelsForRegularSelection,
        Func<Vector2, IReadOnlyList<LabelOnGround>?, bool> tryHandlePendingChestOpenConfirmation,
        Func<Entity?> resolveNextShrineCandidate,
        Action refreshMechanicPriorityCaches,
        Func<MechanicPriorityContext> createMechanicPriorityContext,
        Func<bool> groundItemsVisible,
        Action<string, string, string?> publishClickFlowDebugStage)
    {
        public Func<RectangleF> GetWindowRectangle { get; } = getWindowRectangle;
        public Func<Vector2> GetCursorAbsolutePosition { get; } = getCursorAbsolutePosition;
        public Func<Vector2, bool> TryHandleUltimatumPanelUi { get; } = tryHandleUltimatumPanelUi;
        public Action<string> DebugLog { get; } = debugLog;
        public Func<long, MovementSkillPostCastBlockState> GetMovementSkillPostCastBlockState { get; } = getMovementSkillPostCastBlockState;
        public Func<long, ChestLootSettlementBlockState> GetChestLootSettlementBlockState { get; } = getChestLootSettlementBlockState;
        public Func<IReadOnlyList<LabelOnGround>?> GetLabelsForRegularSelection { get; } = getLabelsForRegularSelection;
        public Func<Vector2, IReadOnlyList<LabelOnGround>?, bool> TryHandlePendingChestOpenConfirmation { get; } = tryHandlePendingChestOpenConfirmation;
        public Func<Entity?> ResolveNextShrineCandidate { get; } = resolveNextShrineCandidate;
        public Action RefreshMechanicPriorityCaches { get; } = refreshMechanicPriorityCaches;
        public Func<MechanicPriorityContext> CreateMechanicPriorityContext { get; } = createMechanicPriorityContext;
        public Func<bool> GroundItemsVisible { get; } = groundItemsVisible;
        public Action<string, string, string?> PublishClickFlowDebugStage { get; } = publishClickFlowDebugStage;
    }

    internal sealed class ClickTickContextFactory(ClickTickContextFactoryDependencies dependencies)
    {
        private readonly ClickTickContextFactoryDependencies _dependencies = dependencies;

        public bool TryCreateRegularClickContext(out ClickTickContext context)
        {
            RectangleF windowArea = _dependencies.GetWindowRectangle();
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 cursorAbsolute = _dependencies.GetCursorAbsolutePosition();

            try
            {
                if (_dependencies.TryHandleUltimatumPanelUi(windowTopLeft))
                {
                    context = default;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _dependencies.DebugLog($"[ProcessRegularClick] Ultimatum UI handler failed, continuing regular click path: {ex.Message}");
            }

            MovementSkillPostCastBlockState movementSkillBlockState = _dependencies.GetMovementSkillPostCastBlockState(Environment.TickCount64);
            if (movementSkillBlockState.IsBlocking)
            {
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while movement skill is still executing ({movementSkillBlockState.Reason}).");
                _dependencies.PublishClickFlowDebugStage("MovementBlocked", movementSkillBlockState.Reason, null);
                context = default;
                return false;
            }

            long now = Environment.TickCount64;
            ChestLootSettlementBlockState chestLootSettlementBlockState = _dependencies.GetChestLootSettlementBlockState(now);
            IReadOnlyList<LabelOnGround>? allLabels = _dependencies.GetLabelsForRegularSelection();
            if (_dependencies.TryHandlePendingChestOpenConfirmation(windowTopLeft, allLabels))
            {
                context = default;
                return false;
            }

            Entity? nextShrine = _dependencies.ResolveNextShrineCandidate();
            _dependencies.RefreshMechanicPriorityCaches();
            MechanicPriorityContext mechanicPriorityContext = _dependencies.CreateMechanicPriorityContext();

            context = new ClickTickContext(
                windowTopLeft,
                cursorAbsolute,
                now,
                chestLootSettlementBlockState.IsBlocking,
                chestLootSettlementBlockState.Reason,
                allLabels,
                nextShrine,
                mechanicPriorityContext,
                _dependencies.GroundItemsVisible());

            return true;
        }
    }
}