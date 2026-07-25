namespace ClickIt.Features.Labels.Application
{
    internal sealed class LabelDebugService(
        ClickItSettings settings,
        ErrorHandler errorHandler,
        GameController? gameController,
        Func<IReadOnlyList<LabelOnGround>?, ClickSettings> createClickSettings,
        Func<ClickSettings, Entity, GameController?, LabelOnGround?, bool> shouldAllowWorldItemByMetadata,
        LabelMechanicResolutionService mechanicResolutionService,
        LabelSelectionDiagnostics diagnostics)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly ErrorHandler _errorHandler = errorHandler;
        private readonly GameController? _gameController = gameController;
        private readonly Func<IReadOnlyList<LabelOnGround>?, ClickSettings> _createClickSettings = createClickSettings;
        private readonly Func<ClickSettings, Entity, GameController?, LabelOnGround?, bool> _shouldAllowWorldItemByMetadata = shouldAllowWorldItemByMetadata;
        private readonly LabelMechanicResolutionService _mechanicResolutionService = mechanicResolutionService;
        private readonly LabelSelectionDiagnostics _diagnostics = diagnostics;

        public LabelDebugSnapshot GetLatestDebug()
            => _diagnostics.GetLatest();

        public IReadOnlyList<string> GetLatestDebugTrail()
            => _diagnostics.GetTrail();

        public (bool LabelsAvailable, int TotalVisibleLabels, int ValidVisibleLabels) GetVisibleLabelCounts()
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

        internal static SelectionDebugSummary BuildSelectionDebugSummary(
            IReadOnlyList<LabelDebugCandidate>? candidates,
            int startIndex,
            int maxCount,
            ClickSettings clickSettings)
        {
            if (candidates == null || candidates.Count == 0)
                return default;

            int start = SystemMath.Max(0, startIndex);
            int end = SystemMath.Min(candidates.Count, start + SystemMath.Max(0, maxCount));
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

                LabelDebugCandidate candidate = candidates[i];
                if (!candidate.HasLabel)
                {
                    nullLabel++;
                    continue;
                }

                if (!candidate.HasItem)
                {
                    nullEntity++;
                    continue;
                }

                bool isSettlersPath = MechanicClassifier.TryGetSettlersOreMechanicId(candidate.Path, out _);
                if (isSettlersPath)
                    settlersPathSeen++;

                if (candidate.Type == EntityType.WorldItem)
                {
                    worldItem++;
                    if (!candidate.AllowWorldItemByMetadata)
                        worldItemMetadataRejected++;
                }

                if (candidate.DistancePlayer > clickSettings.ClickDistance)
                {
                    outOfDistance++;
                    continue;
                }

                if (!candidate.IsTargetable)
                {
                    untargetable++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(candidate.MechanicId))
                {
                    noMechanic++;
                    if (isSettlersPath)
                        settlersMechanicDisabled++;
                    continue;
                }

                if (SettlersMechanicPolicy.IsSettlersMechanicId(candidate.MechanicId))
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

    }

    internal readonly record struct LabelDebugCandidate(
        bool HasLabel,
        bool HasItem,
        string Path,
        EntityType Type,
        float DistancePlayer,
        bool IsTargetable,
        string? MechanicId,
        bool AllowWorldItemByMetadata)
    {
        internal static LabelDebugCandidate NullLabel => new(false, false, string.Empty, default, 0f, false, null, true);

        internal static LabelDebugCandidate NullEntity => new(true, false, string.Empty, default, 0f, false, null, true);
    }
}