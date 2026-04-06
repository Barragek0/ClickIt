namespace ClickIt.Features.Click.Core
{
    internal readonly record struct MovementSkillPostCastBlockState(bool IsBlocking, string Reason);

    internal readonly record struct ChestLootSettlementBlockState(bool IsBlocking, string Reason);

    internal sealed class ClickTickContextFactoryDependencies(
        Func<RectangleF> getWindowRectangle,
        Func<Vector2> getCursorAbsolutePosition,
        Func<Vector2, bool> tryHandleUltimatumPanelUi,
        Action<string> debugLog,
        MovementSkillCoordinator movementSkills,
        ChestLootSettlementTracker chestLootSettlement,
        Func<IReadOnlyList<LabelOnGround>?> getLabelsForRegularSelection,
        IVisibleMechanicSelectionSource visibleMechanics,
        MechanicPriorityContextProvider mechanicPriorityContextProvider,
        Func<bool> groundItemsVisible,
        ClickDebugPublicationService clickDebugPublisher)
    {
        public Func<RectangleF> GetWindowRectangle { get; } = getWindowRectangle;
        public Func<Vector2> GetCursorAbsolutePosition { get; } = getCursorAbsolutePosition;
        public Func<Vector2, bool> TryHandleUltimatumPanelUi { get; } = tryHandleUltimatumPanelUi;
        public Action<string> DebugLog { get; } = debugLog;
        public MovementSkillCoordinator MovementSkills { get; } = movementSkills;
        public ChestLootSettlementTracker ChestLootSettlement { get; } = chestLootSettlement;
        public Func<IReadOnlyList<LabelOnGround>?> GetLabelsForRegularSelection { get; } = getLabelsForRegularSelection;
        public IVisibleMechanicSelectionSource VisibleMechanics { get; } = visibleMechanics;
        public MechanicPriorityContextProvider MechanicPriorityContextProvider { get; } = mechanicPriorityContextProvider;
        public Func<bool> GroundItemsVisible { get; } = groundItemsVisible;
        public ClickDebugPublicationService ClickDebugPublisher { get; } = clickDebugPublisher;
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

            long now = Environment.TickCount64;
            MovementSkillPostCastBlockState movementSkillBlockState = CreateMovementSkillPostCastBlockState(now);
            if (movementSkillBlockState.IsBlocking)
            {
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping click attempt while movement skill is still executing ({movementSkillBlockState.Reason}).");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("MovementBlocked", movementSkillBlockState.Reason, null);
                context = default;
                return false;
            }

            ChestLootSettlementBlockState chestLootSettlementBlockState = CreateChestLootSettlementBlockState(now);
            IReadOnlyList<LabelOnGround>? allLabels = _dependencies.GetLabelsForRegularSelection();
            if (_dependencies.ChestLootSettlement.TryHandlePendingChestOpenConfirmation(windowTopLeft, allLabels))
            {
                context = default;
                return false;
            }

            Entity? nextShrine = _dependencies.VisibleMechanics.ResolveNextShrineCandidate();
            _dependencies.MechanicPriorityContextProvider.Refresh();
            MechanicPriorityContext mechanicPriorityContext = _dependencies.MechanicPriorityContextProvider.CreateContext();

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

        private MovementSkillPostCastBlockState CreateMovementSkillPostCastBlockState(long now)
        {
            return _dependencies.MovementSkills.TryGetMovementSkillPostCastBlockState(now, out string reason)
                ? new MovementSkillPostCastBlockState(true, reason)
                : new MovementSkillPostCastBlockState(false, string.Empty);
        }

        private ChestLootSettlementBlockState CreateChestLootSettlementBlockState(long now)
        {
            bool isBlocking = _dependencies.ChestLootSettlement.IsPostChestLootSettlementBlocking(now, out string reason);
            return new ChestLootSettlementBlockState(isBlocking, reason);
        }
    }
}