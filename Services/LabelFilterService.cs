using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ClickIt.Utils;
using ClickIt.Definitions;
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
        {
            return LazyModeRestrictedChecker(this, allLabels);
        }

        private bool HasLazyModeRestrictedItemsOnScreenImpl(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null)
                return false;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                Entity item = label.ItemOnGround;
                if (item != null && item.DistancePlayer <= _settings.ClickDistance.Value)
                {
                    string path = item.Path;
                    if (string.IsNullOrEmpty(path))
                        continue;

                    var chestComponent = label.ItemOnGround.GetComponent<Chest>();
                    if (chestComponent?.IsLocked == true && !chestComponent.IsStrongbox)
                    {
                        _errorHandler.LogMessage(true, true, $"Lazy mode: restricted item detected - Path: {path}", 5);
                        return true;
                    }
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
                if (label.ItemOnGround?.Path == null || label.Label?.GetClientRect() is not RectangleF rect || label.Label?.IsValid != true || !isInClickableArea(rect.Center))
                    continue;
                string path = label.ItemOnGround.Path;
                if (path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor"))
                    result.Add(label);
            }
            if (result.Count > 1)
                result.Sort((a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));
            return result;
        }

        public LabelOnGround? GetNextLabelToClick(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
        {
            if (allLabels == null || allLabels.Count == 0)
            {
                PublishLabelDebugStage(
                    stage: "NoLabels",
                    startIndex: 0,
                    endExclusive: 0,
                    totalLabels: 0,
                    consideredCandidates: 0,
                    nullOrDistanceRejected: 0,
                    untargetableRejected: 0,
                    noMechanicRejected: 0,
                    ignoredByDistanceCandidates: 0,
                    selectedMechanicId: string.Empty,
                    selectedEntityPath: string.Empty,
                    selectedDistance: 0f,
                    notes: "GetNextLabelToClick received an empty label collection");
                return null;
            }

            var clickSettings = CreateClickSettings(allLabels);
            int start = Math.Max(0, startIndex);
            int end = Math.Min(allLabels.Count, startIndex + Math.Max(0, maxCount));

            PublishLabelDebugStage(
                stage: "SelectionRequested",
                startIndex: start,
                endExclusive: end,
                totalLabels: allLabels.Count,
                consideredCandidates: 0,
                nullOrDistanceRejected: 0,
                untargetableRejected: 0,
                noMechanicRejected: 0,
                ignoredByDistanceCandidates: 0,
                selectedMechanicId: string.Empty,
                selectedEntityPath: string.Empty,
                selectedDistance: 0f,
                notes: $"start={startIndex} maxCount={maxCount}");

            LabelOnGround? selected = SelectNextLabelByPriority(allLabels, start, end, clickSettings);
            if (selected == null)
            {
                PublishLabelDebugStage(
                    stage: "SelectionReturnedNone",
                    startIndex: start,
                    endExclusive: end,
                    totalLabels: allLabels.Count,
                    consideredCandidates: 0,
                    nullOrDistanceRejected: 0,
                    untargetableRejected: 0,
                    noMechanicRejected: 0,
                    ignoredByDistanceCandidates: 0,
                    selectedMechanicId: string.Empty,
                    selectedEntityPath: string.Empty,
                    selectedDistance: 0f,
                    notes: "No label selected");
                return null;
            }

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

            return selected;
        }

        public readonly struct SelectionDebugSummary
        {
            public SelectionDebugSummary(
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
                this.Start = Start;
                this.End = End;
                this.Total = Total;
                this.NullLabel = NullLabel;
                this.NullEntity = NullEntity;
                this.OutOfDistance = OutOfDistance;
                this.Untargetable = Untargetable;
                this.NoMechanic = NoMechanic;
                this.WorldItem = WorldItem;
                this.WorldItemMetadataRejected = WorldItemMetadataRejected;
                this.SettlersPathSeen = SettlersPathSeen;
                this.SettlersMechanicMatched = SettlersMechanicMatched;
                this.SettlersMechanicDisabled = SettlersMechanicDisabled;
            }

            public int Start { get; }
            public int End { get; }
            public int Total { get; }
            public int NullLabel { get; }
            public int NullEntity { get; }
            public int OutOfDistance { get; }
            public int Untargetable { get; }
            public int NoMechanic { get; }
            public int WorldItem { get; }
            public int WorldItemMetadataRejected { get; }
            public int SettlersPathSeen { get; }
            public int SettlersMechanicMatched { get; }
            public int SettlersMechanicDisabled { get; }

            public string ToCompactString()
            {
                return $"r:{Start}-{End} t:{Total} nl:{NullLabel} ne:{NullEntity} d:{OutOfDistance} u:{Untargetable} nm:{NoMechanic} wi:{WorldItem}/{WorldItemMetadataRejected} sp:{SettlersPathSeen} sm:{SettlersMechanicMatched} sd:{SettlersMechanicDisabled}";
            }
        }

        public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
        {
            if (allLabels == null || allLabels.Count == 0)
                return default;

            var clickSettings = CreateClickSettings(allLabels);
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
                    {
                        worldItemMetadataRejected++;
                    }
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
                {
                    settlersMechanicMatched++;
                }
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

            var clickSettings = CreateClickSettings(allLabels);
            int start = Math.Max(0, startIndex);
            int end = Math.Min(allLabels.Count, start + Math.Max(0, maxCount));
            if (start >= end)
            {
                _errorHandler.LogMessage(true, true, $"[LabelFilterDiag] bad-range s:{start} e:{end} c:{allLabels.Count}", 5);
                return;
            }

            SelectionDebugSummary summary = GetSelectionDebugSummary(allLabels, start, end - start);
            string msg =
                $"[LabelFilterDiag] {summary.ToCompactString()} " +
                $"sv:{(clickSettings.ClickSettlersVerisium ? 1 : 0)} sp:{(clickSettings.ClickSettlersPetrifiedWood ? 1 : 0)}";

            _errorHandler.LogMessage(true, true, msg, 5);
        }

        public string? GetMechanicIdForLabel(LabelOnGround? label)
        {
            if (label?.ItemOnGround == null)
                return null;

            if (!IsEntityTargetableForClick(label, label.ItemOnGround))
                return null;

            var clickSettings = CreateClickSettings(null);
            return GetClickableMechanicId(label, label.ItemOnGround, clickSettings, _gameController);
        }

        private LabelOnGround? SelectNextLabelByPriority(System.Collections.Generic.IReadOnlyList<LabelOnGround> allLabels, int startIndex, int endExclusive, ClickSettings clickSettings)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            startIndex = Math.Max(0, startIndex);
            endExclusive = Math.Min(allLabels.Count, endExclusive);
            if (startIndex >= endExclusive)
                return null;

            LabelOnGround? bestNonIgnoredByDistance = null;
            string? bestNonIgnoredMechanicId = null;
            float bestNonIgnoredDistance = float.MaxValue;
            float bestNonIgnoredWeightedScore = float.MaxValue;

            LabelOnGround? bestIgnoredByPriority = null;
            int bestIgnoredPriority = int.MaxValue;

            int consideredCandidates = 0;
            int nullOrDistanceRejected = 0;
            int untargetableRejected = 0;
            int noMechanicRejected = 0;
            int ignoredByDistanceCandidates = 0;

            for (int i = startIndex; i < endExclusive; i++)
            {
                LabelOnGround label = allLabels[i];
                consideredCandidates++;
                if (!TryBuildLabelCandidate(label, clickSettings, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason))
                {
                    switch (rejectReason)
                    {
                        case LabelCandidateRejectReason.NullItemOrOutOfDistance:
                            nullOrDistanceRejected++;
                            break;
                        case LabelCandidateRejectReason.Untargetable:
                            untargetableRejected++;
                            break;
                        case LabelCandidateRejectReason.NoMechanic:
                            noMechanicRejected++;
                            break;
                    }
                    continue;
                }

                if (TryPromoteIgnoredCandidate(label, mechanicId, item.DistancePlayer, clickSettings, ref bestIgnoredByPriority, ref bestIgnoredPriority))
                {
                    ignoredByDistanceCandidates++;
                    continue;
                }

                PromoteNonIgnoredCandidate(
                    label,
                    mechanicId,
                    item.DistancePlayer,
                    clickSettings,
                    ref bestNonIgnoredByDistance,
                    ref bestNonIgnoredMechanicId,
                    ref bestNonIgnoredDistance,
                    ref bestNonIgnoredWeightedScore);
            }

            LabelOnGround? selected = ResolveWinningCandidate(bestNonIgnoredByDistance, bestNonIgnoredMechanicId, bestIgnoredByPriority, bestIgnoredPriority, clickSettings);
            Entity? selectedEntity = selected?.ItemOnGround;
            string? selectedMechanicId = string.Empty;
            if (selected != null && selectedEntity != null)
            {
                selectedMechanicId = GetClickableMechanicId(selected!, selectedEntity, clickSettings, _gameController);
            }

            PublishLabelDebugStage(
                stage: selected == null ? "SelectionScanNone" : "SelectionScanSelected",
                startIndex: startIndex,
                endExclusive: endExclusive,
                totalLabels: allLabels.Count,
                consideredCandidates: consideredCandidates,
                nullOrDistanceRejected: nullOrDistanceRejected,
                untargetableRejected: untargetableRejected,
                noMechanicRejected: noMechanicRejected,
                ignoredByDistanceCandidates: ignoredByDistanceCandidates,
                selectedMechanicId: selectedMechanicId,
                selectedEntityPath: selectedEntity?.Path,
                selectedDistance: selectedEntity?.DistancePlayer ?? 0f,
                notes: $"c:{consideredCandidates} nd:{nullOrDistanceRejected} u:{untargetableRejected} nm:{noMechanicRejected} ig:{ignoredByDistanceCandidates}");

            return selected;
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

            // Only petrified wood uses label/entity-specific targetability semantics.
            if (!ShouldApplyPetrifiedWoodEntityTargetabilityGate(path))
                return true;

            ResolveLabelEntityTargetableForClick(label, out bool hasLabelEntityTargetable, out bool labelEntityTargetable);
            return ShouldAllowPetrifiedWoodTargetability(hasLabelEntityTargetable, labelEntityTargetable);
        }

        private static bool RequiresTargetabilityGate(string path)
        {
            return !string.IsNullOrEmpty(path) && IsSettlersOrePath(path);
        }

        internal static bool ShouldApplyPetrifiedWoodEntityTargetabilityGate(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && IsSettlersPetrifiedWoodPath(path);
        }

        internal static bool ShouldAllowPetrifiedWoodTargetability(bool hasLabelEntityTargetable, bool labelEntityTargetable)
        {
            // If no label entity exists yet, treat petrified wood as not-yet-clicked and allow it.
            if (!hasLabelEntityTargetable)
                return true;

            return labelEntityTargetable;
        }

        internal static void ResolveLabelEntityTargetableForClick(LabelOnGround label, out bool hasLabelEntityTargetable, out bool labelEntityTargetable)
        {
            hasLabelEntityTargetable = false;
            labelEntityTargetable = true;

            Entity? item = label.ItemOnGround;
            if (item != null)
            {
                var targetable = item.GetComponent<Targetable>();
                if (targetable != null)
                {
                    hasLabelEntityTargetable = true;
                    labelEntityTargetable = targetable.isTargetable;
                    return;
                }
            }

            object? rawLabelEntity = null;
            bool hasDirectEntity = TryGetDynamicValue(label, l => l.Entity, out rawLabelEntity);

            if (hasDirectEntity
                && TryResolveLabelEntityTargetableFromRaw(rawLabelEntity, out bool directTargetable, out bool directHasTargetable)
                && directHasTargetable)
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = directTargetable;
                return;
            }

            if (TryGetDynamicValue(label, l => l.Label, out object? rawLabelElement)
                && TryGetDynamicValue(rawLabelElement, l => l.Entity, out object? rawElementEntity)
                && TryResolveLabelEntityTargetableFromRaw(rawElementEntity, out bool labelElementTargetable, out bool labelElementHasTargetable)
                && labelElementHasTargetable)
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = labelElementTargetable;
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

        internal static bool ShouldSkipUntargetableEntity(
            bool hasLabelEntityTargetable,
            bool labelEntityTargetable,
            bool itemIsTargetable,
            bool allowNullEntityFallback = false)
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
            if (!IsIgnoreDistanceActiveForMechanic(
                    mechanicId,
                    distance,
                    clickSettings.IgnoreDistanceMechanicIds,
                    clickSettings.IgnoreDistanceWithinByMechanicId))
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
            if (string.IsNullOrWhiteSpace(mechanicId))
                return false;
            if (!ignoreDistanceMechanicIds.Contains(mechanicId))
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
            int nonIgnoredPriority = GetMechanicPriorityIndex(clickSettings.MechanicPriorityIndexMap, mechanicId);
            float weightedScore = CalculateNonIgnoredWeightedScore(distance, nonIgnoredPriority, clickSettings.MechanicPriorityDistancePenalty);

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

            int bestNonIgnoredPriority = GetMechanicPriorityIndex(clickSettings.MechanicPriorityIndexMap, bestNonIgnoredMechanicId);
            return bestIgnoredPriority <= bestNonIgnoredPriority ? bestIgnoredByPriority : bestNonIgnoredByDistance;
        }

        private static int GetMechanicPriorityIndex(IReadOnlyDictionary<string, int> priorityMap, string? mechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                return int.MaxValue;

            return priorityMap.TryGetValue(mechanicId, out int index) ? index : int.MaxValue;
        }

        private static float CalculateNonIgnoredWeightedScore(float distance, int priorityIndex, int penalty)
        {
            if (priorityIndex == int.MaxValue)
                return float.MaxValue;

            return distance + (priorityIndex * Math.Max(0, penalty));
        }

        private static Dictionary<string, int> BuildMechanicPriorityIndexMap(IReadOnlyList<string> priorities)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < priorities.Count; i++)
            {
                string id = priorities[i] ?? string.Empty;
                if (id.Length == 0 || map.ContainsKey(id))
                    continue;

                map[id] = i;
            }

            return map;
        }

        private void RefreshMechanicPriorityCaches(
            IReadOnlyList<string> mechanicPriorities,
            IReadOnlyCollection<string> ignoreDistance,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
        {
            if (!ReferenceEquals(_cachedMechanicPriorityOrder, mechanicPriorities))
            {
                _cachedMechanicPriorityOrder = mechanicPriorities;
                _cachedMechanicPriorityIndexMap = BuildMechanicPriorityIndexMap(mechanicPriorities);
            }

            if (!ReferenceEquals(_cachedMechanicIgnoreDistanceIds, ignoreDistance))
            {
                _cachedMechanicIgnoreDistanceIds = ignoreDistance;
                _cachedMechanicIgnoreDistanceSet = new HashSet<string>(ignoreDistance, StringComparer.OrdinalIgnoreCase);
            }

            if (!ReferenceEquals(_cachedMechanicIgnoreDistanceWithinById, ignoreDistanceWithinByMechanicId))
            {
                _cachedMechanicIgnoreDistanceWithinById = ignoreDistanceWithinByMechanicId;
                _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(ignoreDistanceWithinByMechanicId, StringComparer.OrdinalIgnoreCase);
            }
        }

        private ClickSettings CreateClickSettings(IReadOnlyList<LabelOnGround>? allLabels)
        {
            var s = _settings;

            bool hasRestricted = LazyModeRestrictedChecker(this, allLabels);
            bool hotkeyHeld = KeyStateProvider(s.ClickLabelKey.Value);
            bool applyLazyModeRestrictions = s.LazyMode.Value && hasRestricted && !hotkeyHeld;
            bool settlersOreEnabled = !applyLazyModeRestrictions && s.ClickSettlersOre.Value;
            IReadOnlyList<string> mechanicPriorities = s.GetMechanicPriorityOrder();
            IReadOnlyCollection<string> ignoreDistance = s.GetMechanicPriorityIgnoreDistanceIds();
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId = s.GetMechanicPriorityIgnoreDistanceWithinById();
            RefreshMechanicPriorityCaches(mechanicPriorities, ignoreDistance, ignoreDistanceWithinByMechanicId);

            return new ClickSettings
            {
                ClickDistance = s.ClickDistance.Value,
                ClickItems = s.ClickItems.Value,
                ItemTypeWhitelistMetadata = s.GetItemTypeWhitelistMetadataIdentifiers(),
                ItemTypeBlacklistMetadata = s.GetItemTypeBlacklistMetadataIdentifiers(),
                ClickBasicChests = s.ClickBasicChests.Value,
                ClickLeagueChests = !applyLazyModeRestrictions && s.ClickLeagueChests.Value,
                ClickDoors = s.ClickDoors.Value,
                ClickLevers = s.ClickLevers.Value,
                ClickAreaTransitions = s.ClickAreaTransitions.Value,
                ClickLabyrinthTrials = s.ClickLabyrinthTrials.Value,
                NearestHarvest = s.NearestHarvest.Value,
                ClickSulphite = s.ClickSulphiteVeins.Value,
                ClickAzurite = s.ClickAzuriteVeins.Value,
                ClickDelveSpawners = s.ClickDelveSpawners.Value,
                HighlightEater = s.HighlightEaterAltars.Value,
                HighlightExarch = s.HighlightExarchAltars.Value,
                ClickEater = s.ClickEaterAltars.Value,
                ClickExarch = s.ClickExarchAltars.Value,
                ClickEssences = s.ClickEssences.Value,
                ClickCrafting = s.ClickCraftingRecipes.Value,
                ClickBreach = s.ClickBreachNodes.Value,
                ClickSettlersOre = settlersOreEnabled,
                ClickSettlersCrimsonIron = settlersOreEnabled,
                ClickSettlersCopper = settlersOreEnabled,
                ClickSettlersPetrifiedWood = settlersOreEnabled,
                ClickSettlersBismuth = settlersOreEnabled,
                ClickSettlersVerisium = settlersOreEnabled,
                StrongboxClickMetadata = s.GetStrongboxClickMetadataIdentifiers(),
                StrongboxDontClickMetadata = s.GetStrongboxDontClickMetadataIdentifiers(),
                ClickSanctum = s.ClickSanctum.Value,
                ClickBetrayal = s.ClickBetrayal.Value,
                ClickBlight = s.ClickBlight.Value,
                ClickAlvaTempleDoors = s.ClickAlvaTempleDoors.Value,
                ClickLegionPillars = s.ClickLegionPillars.Value,
                ClickRitualInitiate = s.ClickRitualInitiate.Value,
                ClickRitualCompleted = s.ClickRitualCompleted.Value,
                ClickInitialUltimatum = s.IsInitialUltimatumClickEnabled(),
                ClickOtherUltimatum = s.IsOtherUltimatumClickEnabled(),
                MechanicPriorityIndexMap = _cachedMechanicPriorityIndexMap,
                IgnoreDistanceMechanicIds = _cachedMechanicIgnoreDistanceSet,
                IgnoreDistanceWithinByMechanicId = _cachedMechanicIgnoreDistanceWithinMap,
                MechanicPriorityDistancePenalty = s.MechanicPriorityDistancePenalty.Value
            };
        }

        internal struct ClickSettings
        {
            public int ClickDistance { get; set; }
            public bool ClickItems { get; set; }
            public IReadOnlyList<string> ItemTypeWhitelistMetadata { get; set; }
            public IReadOnlyList<string> ItemTypeBlacklistMetadata { get; set; }
            public bool ClickBasicChests { get; set; }
            public bool ClickLeagueChests { get; set; }
            public bool ClickDoors { get; set; }
            public bool ClickLevers { get; set; }
            public bool ClickAreaTransitions { get; set; }
            public bool ClickLabyrinthTrials { get; set; }
            public bool NearestHarvest { get; set; }
            public bool ClickSulphite { get; set; }
            public bool ClickBlight { get; set; }
            public bool ClickAlvaTempleDoors { get; set; }
            public bool ClickLegionPillars { get; set; }
            public bool ClickRitualInitiate { get; set; }
            public bool ClickRitualCompleted { get; set; }
            public bool ClickInitialUltimatum { get; set; }
            public bool ClickOtherUltimatum { get; set; }
            public bool ClickAzurite { get; set; }
            public bool ClickDelveSpawners { get; set; }
            public bool HighlightEater { get; set; }
            public bool HighlightExarch { get; set; }
            public bool ClickEater { get; set; }
            public bool ClickExarch { get; set; }
            public bool ClickEssences { get; set; }
            public bool ClickCrafting { get; set; }
            public bool ClickBreach { get; set; }
            public bool ClickSettlersOre { get; set; }
            public bool ClickSettlersCrimsonIron { get; set; }
            public bool ClickSettlersCopper { get; set; }
            public bool ClickSettlersPetrifiedWood { get; set; }
            public bool ClickSettlersBismuth { get; set; }
            public bool ClickSettlersVerisium { get; set; }
            public IReadOnlyList<string> StrongboxClickMetadata { get; set; }
            public IReadOnlyList<string> StrongboxDontClickMetadata { get; set; }
            public bool ClickSanctum { get; set; }
            public bool ClickBetrayal { get; set; }
            public IReadOnlyDictionary<string, int> MechanicPriorityIndexMap { get; set; }
            public IReadOnlySet<string> IgnoreDistanceMechanicIds { get; set; }
            public IReadOnlyDictionary<string, int> IgnoreDistanceWithinByMechanicId { get; set; }
            public int MechanicPriorityDistancePenalty { get; set; }
        }

        public bool ShouldCorruptEssence(LabelOnGround label)
        {
            return _essenceService.ShouldCorruptEssence(label.Label);
        }

        public static Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            return EssenceService.GetCorruptionClickPosition(label, windowTopLeft);
        }

    }
}

