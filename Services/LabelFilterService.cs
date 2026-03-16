using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using System.Diagnostics.CodeAnalysis;

namespace ClickIt.Services
{
    public partial class LabelFilterService(ClickItSettings settings, EssenceService essenceService, ErrorHandler errorHandler, ExileCore.GameController? gameController)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly EssenceService _essenceService = essenceService;
        private readonly ErrorHandler _errorHandler = errorHandler;
        private readonly ExileCore.GameController? _gameController = gameController;

        private IReadOnlyList<string>? _cachedMechanicPriorityOrder;
        private IReadOnlyCollection<string>? _cachedMechanicIgnoreDistanceIds;
        private IReadOnlyDictionary<string, int>? _cachedMechanicIgnoreDistanceWithinById;
        private IReadOnlyDictionary<string, int> _cachedMechanicPriorityIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlySet<string> _cachedMechanicIgnoreDistanceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, int> _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public bool HasLazyModeRestrictedItemsOnScreen(IReadOnlyList<LabelOnGround>? allLabels)
            => LazyModeRestrictedChecker(this, allLabels);

        private bool HasLazyModeRestrictedItemsOnScreenImpl(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null)
                return false;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                Entity? item = label.ItemOnGround;
                if (item == null || item.DistancePlayer > _settings.ClickDistance.Value)
                    continue;

                string path = item.Path ?? string.Empty;
                if (path.Length == 0)
                    continue;

                Chest? chest = item.GetComponent<Chest>();
                if (chest?.IsLocked == true && !chest.IsStrongbox)
                {
                    _errorHandler.LogMessage(true, true, $"Lazy mode: restricted item detected - Path: {path}", 5);
                    return true;
                }
            }

            return false;
        }

        public static List<LabelOnGround> FilterHarvestLabels(IReadOnlyList<LabelOnGround>? allLabels, Func<Vector2, bool> isInClickableArea)
        {
            List<LabelOnGround> result = [];
            if (allLabels == null)
                return result;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                if (!TryGetClickableLabelRectCenter(label, out Vector2 center))
                    continue;
                if (!isInClickableArea(center))
                    continue;

                string path = label.ItemOnGround?.Path ?? string.Empty;
                if (path.Contains("Harvest/Irrigator", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("Harvest/Extractor", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(label);
                }
            }

            if (result.Count > 1)
                result.Sort(static (a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));

            return result;
        }

        private static bool TryGetClickableLabelRectCenter(LabelOnGround? label, out Vector2 center)
        {
            center = default;
            var element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            RectangleF rect = element.GetClientRect();
            center = rect.Center;
            return rect.Width > 0f && rect.Height > 0f;
        }

        public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
        {
            bool captureDebug = ShouldCaptureLabelDebug();

            if (allLabels == null || allLabels.Count == 0)
            {
                if (captureDebug)
                    PublishSelectionLifecycleDebug("NoLabels", allLabels, 0, 0, "GetNextLabelToClick received an empty label collection");
                return null;
            }

            int start = Math.Max(0, startIndex);
            int end = Math.Min(allLabels.Count, startIndex + Math.Max(0, maxCount));
            ClickSettings clickSettings = CreateClickSettings(allLabels);

            if (captureDebug)
                PublishSelectionLifecycleDebug("SelectionRequested", allLabels, start, end, $"start={startIndex} maxCount={maxCount}");

            LabelOnGround? selected = SelectNextLabelByPriority(allLabels, start, end, clickSettings);
            if (captureDebug)
            {
                if (selected == null)
                {
                    PublishSelectionLifecycleDebug("SelectionReturnedNone", allLabels, start, end, "No label selected");
                }
                else
                {
                    Entity? selectedItem = selected.ItemOnGround;
                    string? selectedMechanic = selectedItem != null
                        ? GetClickableMechanicId(selected, selectedItem, clickSettings, _gameController)
                        : null;

                    PublishLabelDebugStage(
                        stage: "SelectionReturned",
                        startIndex: start,
                        endExclusive: end,
                        totalLabels: allLabels.Count,
                        consideredCandidates: 0,
                        nullOrDistanceRejected: 0,
                        untargetableRejected: 0,
                        noMechanicRejected: 0,
                        ignoredByDistanceCandidates: 0,
                        selectedMechanicId: selectedMechanic,
                        selectedEntityPath: selectedItem?.Path,
                        selectedDistance: selectedItem?.DistancePlayer ?? 0f,
                        notes: "Selected label returned to click service");
                }
            }

            return selected;
        }

        private void PublishSelectionLifecycleDebug(string stage, IReadOnlyList<LabelOnGround>? allLabels, int start, int end, string notes)
        {
            PublishLabelDebugStage(
                stage: stage,
                startIndex: start,
                endExclusive: end,
                totalLabels: allLabels?.Count ?? 0,
                consideredCandidates: 0,
                nullOrDistanceRejected: 0,
                untargetableRejected: 0,
                noMechanicRejected: 0,
                ignoredByDistanceCandidates: 0,
                selectedMechanicId: string.Empty,
                selectedEntityPath: string.Empty,
                selectedDistance: 0f,
                notes: notes);
        }

        public readonly struct SelectionDebugSummary(
            int Start,
            int End,
            int Total,
            int NullLabel,
            int NullEntity,
            int OutOfDistance,
            int Untargetable,
            int NoMechanic,
            int WorldItem,
            int WorldItemMetadataRejected,
            int SettlersPathSeen,
            int SettlersMechanicMatched,
            int SettlersMechanicDisabled)
        {
            public string ToCompactString()
            {
                return $"r:{Start}-{End} t:{Total} nl:{NullLabel} ne:{NullEntity} d:{OutOfDistance} u:{Untargetable} nm:{NoMechanic} wi:{WorldItem}/{WorldItemMetadataRejected} sp:{SettlersPathSeen} sm:{SettlersMechanicMatched} sd:{SettlersMechanicDisabled}";
            }
        }

        public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
        {
            if (allLabels == null || allLabels.Count == 0)
                return default;

            ClickSettings clickSettings = CreateClickSettings(allLabels);
            int start = Math.Max(0, startIndex);
            int end = Math.Min(allLabels.Count, start + Math.Max(0, maxCount));
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
                bool isSettlersPath = TryGetSettlersOreMechanicId(path, out _);
                if (isSettlersPath)
                    settlersPathSeen++;

                if (item.Type == ExileCore.Shared.Enums.EntityType.WorldItem)
                {
                    worldItem++;
                    if (!ShouldAllowWorldItemByMetadata(clickSettings, item, _gameController))
                        worldItemMetadataRejected++;
                }

                if (item.DistancePlayer > clickSettings.ClickDistance)
                {
                    outOfDistance++;
                    continue;
                }

                if (!IsEntityTargetableForClick(label, item))
                {
                    untargetable++;
                    continue;
                }

                string? mechanicId = GetClickableMechanicId(label, item, clickSettings, _gameController);
                if (string.IsNullOrWhiteSpace(mechanicId))
                {
                    noMechanic++;
                    if (isSettlersPath)
                        settlersMechanicDisabled++;
                    continue;
                }

                if (mechanicId.StartsWith("settlers-", StringComparison.OrdinalIgnoreCase))
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

            int start = Math.Max(0, startIndex);
            int end = Math.Min(allLabels.Count, start + Math.Max(0, maxCount));
            if (start >= end)
            {
                _errorHandler.LogMessage(true, true, $"[LabelFilterDiag] bad-range s:{start} e:{end} c:{allLabels.Count}", 5);
                return;
            }

            ClickSettings clickSettings = CreateClickSettings(allLabels);
            SelectionDebugSummary summary = GetSelectionDebugSummary(allLabels, start, end - start);
            string msg =
                $"[LabelFilterDiag] {summary.ToCompactString()} " +
                $"sv:{(clickSettings.ClickSettlersVerisium ? 1 : 0)} sp:{(clickSettings.ClickSettlersPetrifiedWood ? 1 : 0)}";

            _errorHandler.LogMessage(true, true, msg, 5);
        }

        public string? GetMechanicIdForLabel(LabelOnGround? label)
        {
            Entity? item = label?.ItemOnGround;
            if (item == null)
                return null;
            if (!IsEntityTargetableForClick(label!, item))
                return null;

            ClickSettings clickSettings = CreateClickSettings(null);
            return GetClickableMechanicId(label!, item, clickSettings, _gameController);
        }

        private LabelOnGround? SelectNextLabelByPriority(IReadOnlyList<LabelOnGround> allLabels, int startIndex, int endExclusive, ClickSettings clickSettings)
        {
            if (allLabels.Count == 0)
                return null;

            int start = Math.Max(0, startIndex);
            int end = Math.Min(allLabels.Count, endExclusive);
            if (start >= end)
                return null;

            var stats = new SelectionStats();

            LabelOnGround? bestNonIgnored = null;
            string? bestNonIgnoredMechanicId = null;
            float bestNonIgnoredDistance = float.MaxValue;
            float bestNonIgnoredWeightedScore = float.MaxValue;

            LabelOnGround? bestIgnored = null;
            int bestIgnoredPriority = int.MaxValue;

            for (int i = start; i < end; i++)
            {
                LabelOnGround label = allLabels[i];
                stats.ConsideredCandidates++;

                if (!TryBuildLabelCandidate(label, clickSettings, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason))
                {
                    stats.AddReject(rejectReason);
                    continue;
                }

                if (TryPromoteIgnoredCandidate(label, mechanicId!, item!.DistancePlayer, clickSettings, ref bestIgnored, ref bestIgnoredPriority))
                {
                    stats.IgnoredByDistanceCandidates++;
                    continue;
                }

                PromoteNonIgnoredCandidate(
                    label,
                    mechanicId!,
                    item.DistancePlayer,
                    clickSettings,
                    ref bestNonIgnored,
                    ref bestNonIgnoredMechanicId,
                    ref bestNonIgnoredDistance,
                    ref bestNonIgnoredWeightedScore);
            }

            LabelOnGround? selected = ResolveWinningCandidate(bestNonIgnored, bestNonIgnoredMechanicId, bestIgnored, bestIgnoredPriority, clickSettings);
            if (ShouldCaptureLabelDebug())
            {
                Entity? selectedEntity = selected?.ItemOnGround;
                string? selectedMechanicId = selectedEntity != null
                    ? GetClickableMechanicId(selected!, selectedEntity, clickSettings, _gameController)
                    : string.Empty;

                PublishLabelDebugStage(
                    stage: selected == null ? "SelectionScanNone" : "SelectionScanSelected",
                    startIndex: start,
                    endExclusive: end,
                    totalLabels: allLabels.Count,
                    consideredCandidates: stats.ConsideredCandidates,
                    nullOrDistanceRejected: stats.NullOrDistanceRejected,
                    untargetableRejected: stats.UntargetableRejected,
                    noMechanicRejected: stats.NoMechanicRejected,
                    ignoredByDistanceCandidates: stats.IgnoredByDistanceCandidates,
                    selectedMechanicId: selectedMechanicId,
                    selectedEntityPath: selectedEntity?.Path,
                    selectedDistance: selectedEntity?.DistancePlayer ?? 0f,
                    notes: $"c:{stats.ConsideredCandidates} nd:{stats.NullOrDistanceRejected} u:{stats.UntargetableRejected} nm:{stats.NoMechanicRejected} ig:{stats.IgnoredByDistanceCandidates}");
            }

            return selected;
        }

        private sealed class SelectionStats
        {
            public int ConsideredCandidates;
            public int NullOrDistanceRejected;
            public int UntargetableRejected;
            public int NoMechanicRejected;
            public int IgnoredByDistanceCandidates;

            public void AddReject(LabelCandidateRejectReason rejectReason)
            {
                if (rejectReason == LabelCandidateRejectReason.NullItemOrOutOfDistance)
                    NullOrDistanceRejected++;
                else if (rejectReason == LabelCandidateRejectReason.Untargetable)
                    UntargetableRejected++;
                else if (rejectReason == LabelCandidateRejectReason.NoMechanic)
                    NoMechanicRejected++;
            }
        }

        private enum LabelCandidateRejectReason
        {
            None = 0,
            NullItemOrOutOfDistance = 1,
            Untargetable = 2,
            NoMechanic = 3
        }

        private bool TryBuildLabelCandidate(
            LabelOnGround label,
            ClickSettings clickSettings,
            [NotNullWhen(true)] out Entity? item,
            [NotNullWhen(true)] out string? mechanicId,
            out LabelCandidateRejectReason rejectReason)
        {
            item = label.ItemOnGround;
            mechanicId = null;
            rejectReason = LabelCandidateRejectReason.None;

            if (item == null || item.DistancePlayer > clickSettings.ClickDistance)
            {
                rejectReason = LabelCandidateRejectReason.NullItemOrOutOfDistance;
                return false;
            }

            if (!IsEntityTargetableForClick(label, item))
            {
                rejectReason = LabelCandidateRejectReason.Untargetable;
                return false;
            }

            mechanicId = GetClickableMechanicId(label, item, clickSettings, _gameController);
            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                rejectReason = LabelCandidateRejectReason.NoMechanic;
                return false;
            }

            return true;
        }

        private static bool IsEntityTargetableForClick(LabelOnGround label, Entity item)
        {
            string path = item.Path ?? string.Empty;
            if (!RequiresTargetabilityGate(path))
                return true;
            if (!ShouldApplyPetrifiedWoodEntityTargetabilityGate(path))
                return true;

            ResolveLabelEntityTargetableForClick(label, out bool hasLabelEntityTargetable, out bool labelEntityTargetable);
            return ShouldAllowPetrifiedWoodTargetability(hasLabelEntityTargetable, labelEntityTargetable);
        }

        private static bool RequiresTargetabilityGate(string path)
            => !string.IsNullOrEmpty(path) && IsSettlersOrePath(path);

        internal static bool ShouldApplyPetrifiedWoodEntityTargetabilityGate(string? path)
            => !string.IsNullOrWhiteSpace(path) && IsSettlersPetrifiedWoodPath(path);

        internal static bool ShouldAllowPetrifiedWoodTargetability(bool hasLabelEntityTargetable, bool labelEntityTargetable)
            => !hasLabelEntityTargetable || labelEntityTargetable;

        internal static void ResolveLabelEntityTargetableForClick(LabelOnGround label, out bool hasLabelEntityTargetable, out bool labelEntityTargetable)
        {
            hasLabelEntityTargetable = false;
            labelEntityTargetable = true;

            Entity? item = label.ItemOnGround;
            if (item != null)
            {
                Targetable? targetable = item.GetComponent<Targetable>();
                if (targetable != null)
                {
                    hasLabelEntityTargetable = true;
                    labelEntityTargetable = targetable.isTargetable;
                    return;
                }
            }

            if (TryGetDynamicValue(label, l => l.Entity, out object? rawLabelEntity)
                && TryResolveLabelEntityTargetableFromRaw(rawLabelEntity, out bool directTargetable, out bool directHasTargetable)
                && directHasTargetable)
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = directTargetable;
                return;
            }

            if (TryGetDynamicValue(label, l => l.Label, out object? rawLabelElement)
                && TryGetDynamicValue(rawLabelElement, l => l.Entity, out object? rawElementEntity)
                && TryResolveLabelEntityTargetableFromRaw(rawElementEntity, out bool elementTargetable, out bool elementHasTargetable)
                && elementHasTargetable)
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = elementTargetable;
            }
        }

        internal static void ResolveLabelEntityTargetableFromRaw(object? rawLabelEntity, out bool hasLabelEntityTargetable, out bool labelEntityTargetable)
        {
            bool resolved = TryResolveLabelEntityTargetableFromRaw(rawLabelEntity, out labelEntityTargetable, out hasLabelEntityTargetable);
            if (!resolved)
            {
                hasLabelEntityTargetable = false;
                labelEntityTargetable = true;
            }
        }

        private static bool TryResolveLabelEntityTargetableFromRaw(object? rawLabelEntity, out bool labelEntityTargetable, out bool hasLabelEntityTargetable)
        {
            hasLabelEntityTargetable = false;
            labelEntityTargetable = true;

            if (rawLabelEntity == null)
                return false;

            if (rawLabelEntity is Entity labelEntity)
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = labelEntity.IsTargetable;
                return true;
            }

            if (TryReadBool(rawLabelEntity, out bool dynamicTargetable, e => e.IsTargetable))
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = dynamicTargetable;
                return true;
            }

            return false;
        }

        internal static bool ShouldSkipUntargetableEntity(bool hasLabelEntityTargetable, bool labelEntityTargetable, bool itemIsTargetable, bool allowNullEntityFallback = false)
        {
            if (hasLabelEntityTargetable && !labelEntityTargetable)
                return true;

            if (!hasLabelEntityTargetable)
                return !allowNullEntityFallback && !itemIsTargetable;

            return !itemIsTargetable;
        }

        private static bool TryPromoteIgnoredCandidate(
            LabelOnGround label,
            string mechanicId,
            float distance,
            ClickSettings clickSettings,
            ref LabelOnGround? bestIgnoredByPriority,
            ref int bestIgnoredPriority)
        {
            if (!IsIgnoreDistanceActiveForMechanic(mechanicId, distance, clickSettings.IgnoreDistanceMechanicIds, clickSettings.IgnoreDistanceWithinByMechanicId))
                return false;

            int candidatePriority = GetMechanicPriorityIndex(clickSettings.MechanicPriorityIndexMap, mechanicId);
            if (candidatePriority < bestIgnoredPriority)
            {
                bestIgnoredPriority = candidatePriority;
                bestIgnoredByPriority = label;
            }

            return true;
        }

        private static bool IsIgnoreDistanceActiveForMechanic(
            string? mechanicId,
            float distance,
            IReadOnlySet<string> ignoreDistanceMechanicIds,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId) || !ignoreDistanceMechanicIds.Contains(mechanicId))
                return false;

            int maxDistance = ignoreDistanceWithinByMechanicId.TryGetValue(mechanicId, out int configured)
                ? configured
                : 100;
            return distance <= maxDistance;
        }

        private static void PromoteNonIgnoredCandidate(
            LabelOnGround label,
            string mechanicId,
            float distance,
            ClickSettings clickSettings,
            ref LabelOnGround? bestNonIgnoredByDistance,
            ref string? bestNonIgnoredMechanicId,
            ref float bestNonIgnoredDistance,
            ref float bestNonIgnoredWeightedScore)
        {
            int priorityIndex = GetMechanicPriorityIndex(clickSettings.MechanicPriorityIndexMap, mechanicId);
            float weightedScore = CalculateNonIgnoredWeightedScore(distance, priorityIndex, clickSettings.MechanicPriorityDistancePenalty);
            bool better = weightedScore < bestNonIgnoredWeightedScore
                || (Math.Abs(weightedScore - bestNonIgnoredWeightedScore) < 0.001f && distance < bestNonIgnoredDistance);
            if (!better)
                return;

            bestNonIgnoredByDistance = label;
            bestNonIgnoredMechanicId = mechanicId;
            bestNonIgnoredDistance = distance;
            bestNonIgnoredWeightedScore = weightedScore;
        }

        private static LabelOnGround? ResolveWinningCandidate(
            LabelOnGround? bestNonIgnoredByDistance,
            string? bestNonIgnoredMechanicId,
            LabelOnGround? bestIgnoredByPriority,
            int bestIgnoredPriority,
            ClickSettings clickSettings)
        {
            if (bestIgnoredByPriority == null)
                return bestNonIgnoredByDistance;
            if (bestNonIgnoredByDistance == null)
                return bestIgnoredByPriority;

            int nonIgnoredPriority = GetMechanicPriorityIndex(clickSettings.MechanicPriorityIndexMap, bestNonIgnoredMechanicId);
            return bestIgnoredPriority <= nonIgnoredPriority ? bestIgnoredByPriority : bestNonIgnoredByDistance;
        }

        private static int GetMechanicPriorityIndex(IReadOnlyDictionary<string, int> priorityMap, string? mechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                return int.MaxValue;

            return priorityMap.TryGetValue(mechanicId, out int index) ? index : int.MaxValue;
        }

        private static float CalculateNonIgnoredWeightedScore(float distance, int priorityIndex, int penalty)
            => priorityIndex == int.MaxValue
                ? float.MaxValue
                : distance + (priorityIndex * Math.Max(0, penalty));

        public bool ShouldCorruptEssence(LabelOnGround label)
            => _essenceService.ShouldCorruptEssence(label.Label);

        public static Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
            => EssenceService.GetCorruptionClickPosition(label, windowTopLeft);
    }
}