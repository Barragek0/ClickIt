namespace ClickIt.Core.Settings.Mechanics
{
    internal static class MechanicTableModelService
    {
        private const string LeagueChestSubgroupMirage = "Mirage";
        private const string HeistSubgroupChests = "Chests";
        private const string HeistSubgroupDoors = "Doors";
        private const string HeistSubgroupHazards = "Hazards";
        private const string LeagueChestSubgroupBlight = "Blight";
        private const string LeagueChestSubgroupBreach = "Breach";
        private const string LeagueChestSubgroupSynthesis = "Synthesis";
        private const string DoorsSubgroupRegular = "Regular";
        private const string DoorsSubgroupHeist = "Heist";
        private const string DoorsSubgroupAlvaTemple = "Alva Temple";

        internal static bool ShouldRenderEntry(MechanicToggleTableEntry entry, bool moveToClick, string filter)
        {
            bool inSourceSet = moveToClick ? !entry.Node.Value : entry.Node.Value;
            return inSourceSet && SettingsUiRenderHelpers.MatchesSearch(filter, entry.DisplayName);
        }

        internal static bool ShouldRenderGroup(MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries, bool moveToClick, string filter)
        {
            bool matchesFilter = SettingsUiRenderHelpers.MatchesSearch(filter, group.DisplayName);

            foreach (MechanicToggleTableEntry entry in entries)
            {
                if (!string.Equals(entry.GroupId, group.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool inSourceSet = moveToClick ? !entry.Node.Value : entry.Node.Value;
                bool childMatchesFilter = SettingsUiRenderHelpers.MatchesSearch(filter, entry.DisplayName);
                if (inSourceSet && (matchesFilter || childMatchesFilter))
                    return true;
            }

            return false;
        }

        internal static void SetGroupState(string groupId, IReadOnlyList<MechanicToggleTableEntry> entries, bool enabled)
        {
            foreach (MechanicToggleTableEntry entry in entries)
                if (string.Equals(entry.GroupId, groupId, StringComparison.OrdinalIgnoreCase))
                    entry.Node.Value = enabled;


        }

        internal static IReadOnlyList<MechanicToggleTableEntry> GetTableEntries(ClickItSettings settings)
        {
            ClickItSettingsRuntimeCacheState runtimeCache = settings.TransientState.RuntimeCache;
            if (runtimeCache.MechanicTableEntriesCache == null)
            {
                runtimeCache.MechanicTableEntriesCache = BuildTableEntries(settings);
                runtimeCache.MechanicToggleNodeByIdCache = BuildToggleNodeById(runtimeCache.MechanicTableEntriesCache);
            }

            return runtimeCache.MechanicTableEntriesCache;
        }

        private static Dictionary<string, ToggleNode> BuildToggleNodeById(IEnumerable<MechanicToggleTableEntry> entries)
        {
            Dictionary<string, ToggleNode> nodesById = new(StringComparer.OrdinalIgnoreCase);
            foreach (MechanicToggleTableEntry entry in entries)
                nodesById[entry.Id] = entry.Node;


            return nodesById;
        }

        private static MechanicToggleTableEntry[] BuildTableEntries(ClickItSettings settings)
        {
            return
            [
                new(MechanicIds.BasicChests, "Basic Chests", settings.ClickBasicChests, "basic-chests", false),
                new(MechanicIds.MirageGoldenDjinnCache, "Golden Djinn's Cache", settings.ClickMirageGoldenDjinnCache, "league-chests", true, LeagueChestSubgroupMirage),
                new(MechanicIds.MirageSilverDjinnCache, "Silver Djinn's Cache", settings.ClickMirageSilverDjinnCache, "league-chests", true, LeagueChestSubgroupMirage),
                new(MechanicIds.MirageBronzeDjinnCache, "Bronze Djinn's Cache", settings.ClickMirageBronzeDjinnCache, "league-chests", true, LeagueChestSubgroupMirage),
                new(MechanicIds.HeistSecureLocker, "Secure Locker (small chest)", settings.ClickHeistSecureLocker, "heist", true, HeistSubgroupChests),
                new(MechanicIds.HeistSecureRepository, "Secure Repository (big chest)", settings.ClickHeistSecureRepository, "heist", true, HeistSubgroupChests),
                new(MechanicIds.HeistHazards, "Hazards", settings.ClickHeistHazards, "heist", false, HeistSubgroupHazards),
                new(MechanicIds.BlightCyst, "Blight Cyst", settings.ClickBlightCyst, "league-chests", true, LeagueChestSubgroupBlight),
                new(MechanicIds.BreachGraspingCoffers, "Grasping Coffers", settings.ClickBreachGraspingCoffers, "league-chests", true, LeagueChestSubgroupBreach),
                new(MechanicIds.SynthesisSynthesisedStash, "Synthesised Stash", settings.ClickSynthesisSynthesisedStash, "league-chests", true, LeagueChestSubgroupSynthesis),
                new(MechanicIds.LeagueChests, "Other League Mechanic Chests", settings.ClickLeagueChestsOther, "league-chests", true),
                new(MechanicIds.Shrines, "Shrines", settings.ClickShrines, null, true),
                new(MechanicIds.AreaTransitions, "Area Transitions", settings.ClickAreaTransitions, null, false),
                new(MechanicIds.LabyrinthTrials, "Labyrinth Trials", settings.ClickLabyrinthTrials, null, false),
                new(MechanicIds.CraftingRecipes, "Crafting Recipes", settings.ClickCraftingRecipes, null, true),
                new(MechanicIds.Doors, "Regular Doors", settings.ClickDoors, "doors", false, DoorsSubgroupRegular),
                new(MechanicIds.HeistDoors, "Doors", settings.ClickHeistDoors, "heist", false, HeistSubgroupDoors),
                new(MechanicIds.Levers, "Levers", settings.ClickLevers, null, false),
                new(MechanicIds.AlvaTempleDoors, "Alva Temple Doors", settings.ClickAlvaTempleDoors, "doors", true, DoorsSubgroupAlvaTemple),
                new(MechanicIds.Betrayal, "Betrayal", settings.ClickBetrayal, null, false),
                new(MechanicIds.Blight, "Blight", settings.ClickBlight, null, true),
                new(MechanicIds.BreachNodes, "Breach Nodes", settings.ClickBreachNodes, null, false),
                new(MechanicIds.LegionPillars, "Legion Pillars", settings.ClickLegionPillars, null, true),
                new(MechanicIds.Harvest, "Nearest Harvest Plot", settings.NearestHarvest, null, true),
                new(MechanicIds.Sanctum, "Sanctum", settings.ClickSanctum, null, true),
                new(MechanicIds.Items, "Items", settings.ClickItems, null, true),
                new(MechanicIds.Essences, "Essences", settings.ClickEssences, null, true),
                new(MechanicIds.Strongboxes, "Strongboxes", settings.ClickStrongboxes, null, true),
                new(MechanicIds.RitualInitiate, "Uncompleted Altars", settings.ClickRitualInitiate, "ritual-altars", true),
                new(MechanicIds.RitualCompleted, "Completed Altars", settings.ClickRitualCompleted, "ritual-altars", true),
                new(MechanicIds.LostShipment, "Lost Shipment", settings.ClickLostShipmentCrates, "settlers", true),
                new(MechanicIds.SettlersCrimsonIron, "Crimson Iron", settings.ClickSettlersCrimsonIron, "settlers", true),
                new(MechanicIds.SettlersCopper, "Copper", settings.ClickSettlersCopper, "settlers", true),
                new(MechanicIds.SettlersPetrifiedWood, "Petrified Wood", settings.ClickSettlersPetrifiedWood, "settlers", true),
                new(MechanicIds.SettlersBismuth, "Bismuth", settings.ClickSettlersBismuth, "settlers", true),
                new(MechanicIds.SettlersHourglass, "Hourglass", settings.ClickSettlersOre, "settlers", true),
                new(MechanicIds.SettlersVerisium, "Verisium", settings.ClickSettlersVerisium, "settlers", true),
                new(MechanicIds.DelveAzuriteVeins, "Azurite Veins", settings.ClickAzuriteVeins, "delve", true),
                new(MechanicIds.DelveSulphiteVeins, "Sulphite Veins", settings.ClickSulphiteVeins, "delve", true),
                new(MechanicIds.DelveEncounterInitiators, "Encounter Initiators", settings.ClickDelveSpawners, "delve", true),
                new(MechanicIds.UltimatumInitialOverlay, "Initial Ultimatum Overlay", settings.ClickInitialUltimatum, "ultimatum", false),
                new(MechanicIds.UltimatumWindow, "Ultimatum Window", settings.ClickUltimatumChoices, "ultimatum", false),
                new(MechanicIds.AltarsSearingExarch, "Searing Exarch", settings.ClickExarchAltars, "altars", false),
                new(MechanicIds.AltarsEaterOfWorlds, "Eater of Worlds", settings.ClickEaterAltars, "altars", false)
            ];
        }
    }
}