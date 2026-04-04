namespace ClickIt.Features.Labels.Selection
{
    internal sealed class ClickSettingsFactory(
        ClickItSettings settings,
        IMechanicPrioritySnapshotProvider mechanicPrioritySnapshotProvider,
        Func<IReadOnlyList<LabelOnGround>?, bool> hasLazyModeRestrictedItems,
        Func<Keys, bool> isClickHotkeyHeld)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly IMechanicPrioritySnapshotProvider _mechanicPrioritySnapshotProvider = mechanicPrioritySnapshotProvider;
        private readonly Func<IReadOnlyList<LabelOnGround>?, bool> _hasLazyModeRestrictedItems = hasLazyModeRestrictedItems;
        private readonly Func<Keys, bool> _isClickHotkeyHeld = isClickHotkeyHeld;

        internal ClickSettings Create(IReadOnlyList<LabelOnGround>? allLabels)
        {
            bool hasRestricted = _hasLazyModeRestrictedItems(allLabels);
            bool hotkeyHeld = _isClickHotkeyHeld(_settings.ClickLabelKeyBinding);
            bool applyLazyRestrictions = _settings.LazyMode.Value && hasRestricted && !hotkeyHeld;

            bool settlersOreEnabled = !applyLazyRestrictions && _settings.ClickSettlersOre.Value;
            bool leagueChestsEnabled = !applyLazyRestrictions && _settings.ClickLeagueChests.Value;
            IReadOnlySet<string> enabledLeagueChestSpecificIds = BuildEnabledLeagueChestSpecificIds(_settings, leagueChestsEnabled);
            IReadOnlyList<string> mechanicPriorities = _settings.GetMechanicPriorityOrder();
            IReadOnlyCollection<string> ignoreDistance = _settings.GetMechanicPriorityIgnoreDistanceIds();
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId = _settings.GetMechanicPriorityIgnoreDistanceWithinById();
            var mechanicPrioritySnapshot = _mechanicPrioritySnapshotProvider.Refresh(
                mechanicPriorities,
                ignoreDistance,
                ignoreDistanceWithinByMechanicId);

            return new ClickSettings
            {
                ClickDistance = _settings.ClickDistance.Value,
                ClickItems = _settings.ClickItems.Value,
                ItemTypeWhitelistMetadata = _settings.GetItemTypeWhitelistMetadataIdentifiers(),
                ItemTypeBlacklistMetadata = _settings.GetItemTypeBlacklistMetadataIdentifiers(),
                ClickBasicChests = _settings.ClickBasicChests.Value,
                ClickLeagueChests = leagueChestsEnabled,
                ClickLeagueChestsOther = leagueChestsEnabled && _settings.ClickLeagueChestsOther.Value,
                EnabledLeagueChestSpecificIds = enabledLeagueChestSpecificIds,
                ClickDoors = _settings.ClickDoors.Value,
                ClickLevers = _settings.ClickLevers.Value,
                ClickAreaTransitions = _settings.ClickAreaTransitions.Value,
                ClickLabyrinthTrials = _settings.ClickLabyrinthTrials.Value,
                NearestHarvest = _settings.NearestHarvest.Value,
                ClickSulphite = _settings.ClickSulphiteVeins.Value,
                ClickAzurite = _settings.ClickAzuriteVeins.Value,
                ClickDelveSpawners = _settings.ClickDelveSpawners.Value,
                HighlightEater = _settings.HighlightEaterAltars.Value,
                HighlightExarch = _settings.HighlightExarchAltars.Value,
                ClickEater = _settings.ClickEaterAltars.Value,
                ClickExarch = _settings.ClickExarchAltars.Value,
                ClickEssences = _settings.ClickEssences.Value,
                ClickCrafting = _settings.ClickCraftingRecipes.Value,
                ClickBreach = _settings.ClickBreachNodes.Value,
                ClickSettlersOre = settlersOreEnabled,
                ClickSettlersCrimsonIron = settlersOreEnabled && _settings.ClickSettlersCrimsonIron.Value,
                ClickSettlersCopper = settlersOreEnabled && _settings.ClickSettlersCopper.Value,
                ClickSettlersPetrifiedWood = settlersOreEnabled && _settings.ClickSettlersPetrifiedWood.Value,
                ClickSettlersBismuth = settlersOreEnabled && _settings.ClickSettlersBismuth.Value,
                ClickSettlersVerisium = settlersOreEnabled && _settings.ClickSettlersVerisium.Value,
                ClickStrongboxes = _settings.ClickStrongboxes.Value,
                StrongboxClickMetadata = _settings.GetStrongboxClickMetadataIdentifiers(),
                StrongboxDontClickMetadata = _settings.GetStrongboxDontClickMetadataIdentifiers(),
                ClickSanctum = _settings.ClickSanctum.Value,
                ClickBetrayal = _settings.ClickBetrayal.Value,
                ClickBlight = _settings.ClickBlight.Value,
                ClickAlvaTempleDoors = _settings.ClickAlvaTempleDoors.Value,
                ClickLegionPillars = _settings.ClickLegionPillars.Value,
                ClickRitualInitiate = _settings.ClickRitualInitiate.Value,
                ClickRitualCompleted = _settings.ClickRitualCompleted.Value,
                ClickInitialUltimatum = _settings.IsInitialUltimatumClickEnabled(),
                ClickOtherUltimatum = _settings.IsOtherUltimatumClickEnabled(),
                MechanicPriorityIndexMap = mechanicPrioritySnapshot.PriorityIndexMap,
                IgnoreDistanceMechanicIds = mechanicPrioritySnapshot.IgnoreDistanceSet,
                IgnoreDistanceWithinByMechanicId = mechanicPrioritySnapshot.IgnoreDistanceWithinByMechanicId,
                MechanicPriorityDistancePenalty = _settings.MechanicPriorityDistancePenalty.Value
            };
        }

        internal static IReadOnlySet<string> BuildEnabledLeagueChestSpecificIds(ClickItSettings settings, bool leagueChestsEnabled)
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