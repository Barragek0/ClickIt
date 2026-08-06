namespace ClickIt.Features.Labels
{
    public sealed partial class LabelFilterPort : ILabelInteractionPort
    {
        private readonly ClickItSettings _settings;
        private readonly EssenceService _essenceService;
        private readonly ErrorHandler _errorHandler;
        private readonly GameController? _gameController;
        private readonly IWorldItemMetadataPolicy _worldItemMetadataPolicy = new WorldItemMetadataPolicy();
        private readonly IMechanicPrioritySnapshotProvider _mechanicPrioritySnapshotService = new MechanicPrioritySnapshotService();
        private readonly LabelSelectionDiagnostics _labelSelectionDiagnostics = new(24);
        private InventoryDomainServices? _inventoryDomainServices;

        internal LabelFilterPort(ClickItSettings settings, EssenceService essenceService, ErrorHandler errorHandler, GameController? gameController)
        {
            _settings = settings;
            _essenceService = essenceService;
            _errorHandler = errorHandler;
            _gameController = gameController;
        }

        internal (bool LabelsAvailable, int TotalVisibleLabels, int ValidVisibleLabels) GetVisibleLabelCounts()
            => LabelDebugService.GetVisibleLabelCounts();

        public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => LabelDebugService.GetSelectionDebugSummary(allLabels, startIndex, maxCount);

        public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => LabelDebugService.LogSelectionDiagnostics(allLabels, startIndex, maxCount);

        public string? GetMechanicIdForLabel(LabelOnGround? label)
            => LabelSelectionService.GetMechanicIdForLabel(label);

        public bool ShouldCorruptEssence(LabelOnGround label)
            => _essenceService.ShouldCorruptEssence(label.Label);

        private bool ShouldCaptureLabelDebug()
            => _settings.DebugMode.Value && _settings.DebugShowLabels.Value;
    }
}