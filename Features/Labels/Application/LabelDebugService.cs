namespace ClickIt.Features.Labels.Application
{
    internal sealed class LabelDebugService
    {
        private readonly ClickItSettings _settings;
        private readonly ErrorHandler _errorHandler;
        private readonly GameController? _gameController;
        private readonly Func<IReadOnlyList<LabelOnGround>?, ClickSettings> _createClickSettings;
        private readonly Func<ClickSettings, Entity, GameController?, LabelOnGround?, bool> _shouldAllowWorldItemByMetadata;
        private readonly LabelMechanicResolutionService _mechanicResolutionService;
        private readonly LabelSelectionDiagnostics _diagnostics;

        // The debug telemetry rebuilds this every frame while the debug window is open; reading the ground-label list on every frame is a major allocation source, so the counts are cached.
        private readonly TimeCache<(bool LabelsAvailable, int TotalVisibleLabels, int ValidVisibleLabels)> _visibleLabelCounts;

        public LabelDebugService(
            ClickItSettings settings,
            ErrorHandler errorHandler,
            GameController? gameController,
            Func<IReadOnlyList<LabelOnGround>?, ClickSettings> createClickSettings,
            Func<ClickSettings, Entity, GameController?, LabelOnGround?, bool> shouldAllowWorldItemByMetadata,
            LabelMechanicResolutionService mechanicResolutionService,
            LabelSelectionDiagnostics diagnostics)
        {
            _settings = settings;
            _errorHandler = errorHandler;
            _gameController = gameController;
            _createClickSettings = createClickSettings;
            _shouldAllowWorldItemByMetadata = shouldAllowWorldItemByMetadata;
            _mechanicResolutionService = mechanicResolutionService;
            _diagnostics = diagnostics;
            _visibleLabelCounts = new TimeCache<(bool LabelsAvailable, int TotalVisibleLabels, int ValidVisibleLabels)>(ReadVisibleLabelCounts, 100);
        }

        public LabelDebugSnapshot GetLatestDebug()
            => _diagnostics.GetLatest();

        public IReadOnlyList<string> GetLatestDebugTrail()
            => _diagnostics.GetTrail();

        public (bool LabelsAvailable, int TotalVisibleLabels, int ValidVisibleLabels) GetVisibleLabelCounts()
            => _visibleLabelCounts.Value;

        private (bool LabelsAvailable, int TotalVisibleLabels, int ValidVisibleLabels) ReadVisibleLabelCounts()
        {
            IList<LabelOnGround>? labels = _gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null)
                return (false, 0, 0);

            int validVisibleLabels = 0;
            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i]?.ItemOnGround?.Path != null)
                    validVisibleLabels++;
            }

            return (true, labels.Count, validVisibleLabels);
        }

        public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
        {
            if (allLabels == null || allLabels.Count == 0)
                return default;

            ClickSettings clickSettings = _createClickSettings(allLabels);
            int start = SystemMath.Max(0, startIndex);
            int end = SystemMath.Min(allLabels.Count, start + SystemMath.Max(0, maxCount));
            if (start >= end)
                return default;

            int total = 0;
            int nullLabel = 0;
            int nullEntity = 0;
            int outOfDistance = 0;
            int untargetable = 0;
            int noMechanic = 0;
            int worldItem = 0;
            int worldItemMetadataRejected = 0;
            int settlersPathSeen = 0;
            int settlersMechanicMatched = 0;
            int settlersMechanicDisabled = 0;

            for (int i = start; i < end; i++)
            {
                total++;

                LabelOnGround? label = allLabels[i];
                if (label == null)
                {
                    nullLabel++;
                    continue;
                }

                Entity? item = label.ItemOnGround;
                if (item == null)
                {
                    nullEntity++;
                    continue;
                }

                string path = item.Path ?? string.Empty;
                bool isSettlersPath = MechanicClassifier.TryGetSettlersOreMechanicId(path, out _);
                if (isSettlersPath)
                    settlersPathSeen++;

                if (item.Type == EntityType.WorldItem)
                {
                    worldItem++;
                    if (!_shouldAllowWorldItemByMetadata(clickSettings, item, _gameController, label))
                        worldItemMetadataRejected++;
                }

                if (item.DistancePlayer > clickSettings.ClickDistance)
                {
                    outOfDistance++;
                    continue;
                }

                if (!LabelTargetabilityPolicy.IsEntityTargetableForClick(label, item))
                {
                    untargetable++;
                    continue;
                }

                string? mechanicId = _mechanicResolutionService.ResolveMechanicId(label, item, clickSettings);
                if (string.IsNullOrWhiteSpace(mechanicId))
                {
                    noMechanic++;
                    if (isSettlersPath)
                        settlersMechanicDisabled++;
                    continue;
                }

                if (SettlersMechanicPolicy.IsSettlersMechanicId(mechanicId))
                    settlersMechanicMatched++;
            }

            return new SelectionDebugSummary(
                start,
                end,
                total,
                nullLabel,
                nullEntity,
                outOfDistance,
                untargetable,
                noMechanic,
                worldItem,
                worldItemMetadataRejected,
                settlersPathSeen,
                settlersMechanicMatched,
                settlersMechanicDisabled);
        }

        public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
        {
            if (_settings?.DebugMode?.Value != true || _settings?.LogMessages?.Value != true)
                return;

            if (allLabels == null || allLabels.Count == 0)
            {
                _errorHandler.LogMessage(true, true, "[LabelFilterDiag] none", 5);
                return;
            }

            int start = SystemMath.Max(0, startIndex);
            int end = SystemMath.Min(allLabels.Count, start + SystemMath.Max(0, maxCount));
            if (start >= end)
            {
                _errorHandler.LogMessage(true, true, $"[LabelFilterDiag] bad-range s:{start} e:{end} c:{allLabels.Count}", 5);
                return;
            }

            ClickSettings clickSettings = _createClickSettings(allLabels);
            SelectionDebugSummary summary = GetSelectionDebugSummary(allLabels, start, end - start);
            string msg =
                $"[LabelFilterDiag] {summary.ToCompactString()} " +
                $"sv:{(clickSettings.ClickSettlersVerisium ? 1 : 0)} sp:{(clickSettings.ClickSettlersPetrifiedWood ? 1 : 0)}";

            _errorHandler.LogMessage(true, true, msg, 5);
        }

    }

}
