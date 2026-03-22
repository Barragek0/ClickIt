using ExileCore.PoEMemory.Elements;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private static Dictionary<string, int> BuildMechanicPriorityIndexMap(IReadOnlyList<string> priorities)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < priorities.Count; i++)
            {
                string id = priorities[i] ?? string.Empty;
                if (id.Length > 0)
                    map.TryAdd(id, i);
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
            ClickItSettings s = _settings;

            bool hasRestricted = LazyModeRestrictedChecker(this, allLabels);
            bool hotkeyHeld = KeyStateProvider(s.ClickLabelKey.Value);
            bool applyLazyRestrictions = s.LazyMode.Value && hasRestricted && !hotkeyHeld;

            bool settlersOreEnabled = !applyLazyRestrictions && s.ClickSettlersOre.Value;
            bool leagueChestsEnabled = !applyLazyRestrictions && s.ClickLeagueChests.Value;
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
                ClickLeagueChests = leagueChestsEnabled,
                ClickLeagueChestsOther = leagueChestsEnabled && s.ClickLeagueChestsOther.Value,
                ClickMirageGoldenDjinnCache = leagueChestsEnabled && s.ClickMirageGoldenDjinnCache.Value,
                ClickMirageSilverDjinnCache = leagueChestsEnabled && s.ClickMirageSilverDjinnCache.Value,
                ClickMirageBronzeDjinnCache = leagueChestsEnabled && s.ClickMirageBronzeDjinnCache.Value,
                ClickHeistSecureLocker = leagueChestsEnabled && s.ClickHeistSecureLocker.Value,
                ClickBreachGraspingCoffers = leagueChestsEnabled && s.ClickBreachGraspingCoffers.Value,
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
                ClickSettlersCrimsonIron = settlersOreEnabled && s.ClickSettlersCrimsonIron.Value,
                ClickSettlersCopper = settlersOreEnabled && s.ClickSettlersCopper.Value,
                ClickSettlersPetrifiedWood = settlersOreEnabled && s.ClickSettlersPetrifiedWood.Value,
                ClickSettlersBismuth = settlersOreEnabled && s.ClickSettlersBismuth.Value,
                ClickSettlersVerisium = settlersOreEnabled && s.ClickSettlersVerisium.Value,
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
            public bool ClickLeagueChestsOther { get; set; }
            public bool ClickMirageGoldenDjinnCache { get; set; }
            public bool ClickMirageSilverDjinnCache { get; set; }
            public bool ClickMirageBronzeDjinnCache { get; set; }
            public bool ClickHeistSecureLocker { get; set; }
            public bool ClickBreachGraspingCoffers { get; set; }
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
    }
}