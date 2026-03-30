using ExileCore.PoEMemory.Elements;
using ClickIt.Definitions;

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

        internal ClickSettings CreateClickSettings(IReadOnlyList<LabelOnGround>? allLabels)
        {
            ClickItSettings s = _settings;

            bool hasRestricted = LazyModeRestrictedChecker(this, allLabels);
            bool hotkeyHeld = KeyStateProvider(s.ClickLabelKey.Value);
            bool applyLazyRestrictions = s.LazyMode.Value && hasRestricted && !hotkeyHeld;

            bool settlersOreEnabled = !applyLazyRestrictions && s.ClickSettlersOre.Value;
            bool leagueChestsEnabled = !applyLazyRestrictions && s.ClickLeagueChests.Value;
            IReadOnlySet<string> enabledLeagueChestSpecificIds = BuildEnabledLeagueChestSpecificIds(s, leagueChestsEnabled);
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
                EnabledLeagueChestSpecificIds = enabledLeagueChestSpecificIds,
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

        private static IReadOnlySet<string> BuildEnabledLeagueChestSpecificIds(ClickItSettings settings, bool leagueChestsEnabled)
        {
            if (!leagueChestsEnabled)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            HashSet<string> enabled = new(StringComparer.OrdinalIgnoreCase);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickMirageGoldenDjinnCache.Value, MechanicIds.MirageGoldenDjinnCache);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickMirageSilverDjinnCache.Value, MechanicIds.MirageSilverDjinnCache);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickMirageBronzeDjinnCache.Value, MechanicIds.MirageBronzeDjinnCache);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickHeistSecureLocker.Value, MechanicIds.HeistSecureLocker);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickBlightCyst.Value, MechanicIds.BlightCyst);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickBreachGraspingCoffers.Value, MechanicIds.BreachGraspingCoffers);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickSynthesisSynthesisedStash.Value, MechanicIds.SynthesisSynthesisedStash);
            return enabled;
        }

        private static void AddEnabledLeagueChestSpecificId(HashSet<string> enabledIds, bool isEnabled, string specificId)
        {
            if (!isEnabled || string.IsNullOrWhiteSpace(specificId))
                return;

            enabledIds.Add(specificId);
        }

    }
}