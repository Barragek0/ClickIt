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
            MechanicPrioritySnapshot mechanicPrioritySnapshot = _mechanicPrioritySnapshotProvider.Refresh(
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
                ClickHeistDoors = _settings.ClickHeistDoors.Value,
                ClickLevers = _settings.ClickLevers.Value,
                ClickAreaTransitions = _settings.ClickAreaTransitions.Value,
                ClickLabyrinthTrials = _settings.ClickLabyrinthTrials.Value,
                ClickHarvest = _settings.ClickHarvest.Value,
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
                ClickBlightPump = _settings.ClickBlightPump.Value,
                ClickAlvaTempleDoors = _settings.ClickAlvaTempleDoors.Value,
                ClickLegionPillars = _settings.ClickLegionPillars.Value,
                ClickRitualInitiate = _settings.ClickRitualInitiate.Value,
                ClickRitualCompleted = _settings.ClickRitualCompleted.Value,
                ClickInitialUltimatum = _settings.IsInitialUltimatumClickEnabled(),
                ClickOtherUltimatum = _settings.IsOtherUltimatumClickEnabled(),
                MechanicPriorityIndexMap = mechanicPrioritySnapshot.PriorityIndexMap,
                IgnoreDistanceMechanicIds = mechanicPrioritySnapshot.IgnoreDistanceSet,
                IgnoreDistanceWithinByMechanicId = mechanicPrioritySnapshot.IgnoreDistanceWithinByMechanicId,
                MechanicPriorityDistancePenalty = _settings.MechanicPriorityDistancePenalty.Value,
                HarvestLabelSelectionBlocked = _settings.ClickHigherHarvestEstimate.Value
            };
        }

        private static readonly HashSet<string> s_emptyEnabledLeagueChestIds = new(StringComparer.OrdinalIgnoreCase);

        // League-chest-specific-id HashSet cache: the 13 booleans change only when the user edits settings, so the 13-bool snapshot is a stable cache key. Saves the per-scan HashSet allocation when the scan runs repeatedly with the same settings.
        private static HashSet<string>? s_cachedLeagueChestIds;
        private static long s_cachedLeagueChestFlags;

        internal static IReadOnlySet<string> BuildEnabledLeagueChestSpecificIds(ClickItSettings settings, bool leagueChestsEnabled)
        {
            if (!leagueChestsEnabled)
                return s_emptyEnabledLeagueChestIds;

            // Pack the 13 booleans into a snapshot key (2 bits per value, 7 booleans per qword). When the snapshot matches the cache, return the cached HashSet without re-allocating.
            long flags = PackLeagueChestFlags(settings);
            if (flags == s_cachedLeagueChestFlags && s_cachedLeagueChestIds != null)
                return s_cachedLeagueChestIds;

            HashSet<string> enabled = new(StringComparer.OrdinalIgnoreCase);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickMirageGoldenDjinnCache.Value, MechanicIds.MirageGoldenDjinnCache);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickMirageSilverDjinnCache.Value, MechanicIds.MirageSilverDjinnCache);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickMirageBronzeDjinnCache.Value, MechanicIds.MirageBronzeDjinnCache);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickHeistSecureLocker.Value, MechanicIds.HeistSecureLocker);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickHeistSecureRepository.Value, MechanicIds.HeistSecureRepository);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickHeistHazards.Value, MechanicIds.HeistHazards);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickBlightCyst.Value, MechanicIds.BlightCyst);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickLegionChest.Value, MechanicIds.LegionChest);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickBreachGraspingCoffers.Value, MechanicIds.BreachGraspingCoffers);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickSynthesisSynthesisedStash.Value, MechanicIds.SynthesisSynthesisedStash);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickAllflameCursedTreasure.Value, MechanicIds.AllflameCursedTreasure);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickAllflameBrinerotPlunder.Value, MechanicIds.AllflameBrinerotPlunder);
            AddEnabledLeagueChestSpecificId(enabled, settings.ClickAllflameCoralNest.Value, MechanicIds.AllflameCoralNest);
            s_cachedLeagueChestFlags = flags;
            s_cachedLeagueChestIds = enabled;
            return enabled;
        }

        // 13 booleans packed into a long: 1 bit per flag, occupying the low 13 bits with deterministic ordering so the bit positions are stable. The cache treats the long as a content hash of the input set.
        private static long PackLeagueChestFlags(ClickItSettings settings)
        {
            long flags = 0;
            if (settings.ClickMirageGoldenDjinnCache.Value) flags |= 1L << 0;
            if (settings.ClickMirageSilverDjinnCache.Value) flags |= 1L << 1;
            if (settings.ClickMirageBronzeDjinnCache.Value) flags |= 1L << 2;
            if (settings.ClickHeistSecureLocker.Value) flags |= 1L << 3;
            if (settings.ClickHeistSecureRepository.Value) flags |= 1L << 4;
            if (settings.ClickHeistHazards.Value) flags |= 1L << 5;
            if (settings.ClickBlightCyst.Value) flags |= 1L << 6;
            if (settings.ClickLegionChest.Value) flags |= 1L << 7;
            if (settings.ClickBreachGraspingCoffers.Value) flags |= 1L << 8;
            if (settings.ClickSynthesisSynthesisedStash.Value) flags |= 1L << 9;
            if (settings.ClickAllflameCursedTreasure.Value) flags |= 1L << 10;
            if (settings.ClickAllflameBrinerotPlunder.Value) flags |= 1L << 11;
            if (settings.ClickAllflameCoralNest.Value) flags |= 1L << 12;
            return flags;
        }

        private static void AddEnabledLeagueChestSpecificId(HashSet<string> enabledIds, bool isEnabled, string specificId)
        {
            if (!isEnabled || string.IsNullOrWhiteSpace(specificId))
                return;

            enabledIds.Add(specificId);
        }
    }
}