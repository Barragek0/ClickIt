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
                    if (path.Contains(Constants.PetrifiedWood) || (chestComponent?.IsLocked == true && !chestComponent.IsStrongbox))
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
            if (allLabels == null || allLabels.Count == 0) return null;
            var clickSettings = CreateClickSettings(allLabels);
            int end = Math.Min(allLabels.Count, startIndex + Math.Max(0, maxCount));

            return SelectNextLabelByPriority(allLabels, startIndex, end, clickSettings);
        }

        public string? GetMechanicIdForLabel(LabelOnGround? label)
        {
            if (label?.ItemOnGround == null)
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

            for (int i = startIndex; i < endExclusive; i++)
            {
                LabelOnGround label = allLabels[i];
                if (!TryBuildLabelCandidate(label, clickSettings, out Entity? item, out string? mechanicId))
                    continue;

                if (TryPromoteIgnoredCandidate(label, mechanicId, item.DistancePlayer, clickSettings, ref bestIgnoredByPriority, ref bestIgnoredPriority))
                    continue;

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

            return ResolveWinningCandidate(bestNonIgnoredByDistance, bestNonIgnoredMechanicId, bestIgnoredByPriority, bestIgnoredPriority, clickSettings);
        }

        private bool TryBuildLabelCandidate(
            LabelOnGround label,
            ClickSettings clickSettings,
            [NotNullWhen(true)] out Entity? item,
            [NotNullWhen(true)] out string? mechanicId)
        {
            item = label.ItemOnGround;
            mechanicId = null;
            if (item == null || item.DistancePlayer > clickSettings.ClickDistance)
                return false;

            mechanicId = GetClickableMechanicId(label, item, clickSettings, _gameController);
            return !string.IsNullOrWhiteSpace(mechanicId);
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
                ClickSettlersOre = !applyLazyModeRestrictions && s.ClickSettlersOre.Value,
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

        private struct ClickSettings
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

