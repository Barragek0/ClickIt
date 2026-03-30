namespace ClickIt.Services
{
    public partial class ClickService
    {
        private ClickTickContextFactory? _tickContextFactory;
        private ClickActionExecutor? _clickActionExecutor;
        private LabelSelectionCoordinator? _labelSelectionCoordinator;
        private ChestLootSettlementTracker? _chestLootSettlementTracker;
        private VisibleMechanicCoordinator? _visibleMechanicCoordinator;
        private OffscreenPathingCoordinator? _offscreenPathingCoordinator;
        private MovementSkillCoordinator? _movementSkillCoordinator;
        private RegularClickCoordinator? _regularClickCoordinator;

        private LabelSelectionCoordinator LabelSelection => _labelSelectionCoordinator ??= new(this);

        private ChestLootSettlementTracker ChestLootSettlement => _chestLootSettlementTracker ??= new(this);

        private VisibleMechanicCoordinator VisibleMechanics => _visibleMechanicCoordinator ??= new(this);

        private OffscreenPathingCoordinator OffscreenPathing => _offscreenPathingCoordinator ??= new(this);

        private MovementSkillCoordinator MovementSkills => _movementSkillCoordinator ??= new(this);

        private RegularClickCoordinator RegularClick => _regularClickCoordinator ??= new(this);

        private ClickTickContextFactory TickContextFactory => _tickContextFactory ??= new(this);

        private ClickActionExecutor ClickActions => _clickActionExecutor ??= new(this);
    }
}