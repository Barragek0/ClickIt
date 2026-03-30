using ClickIt.Definitions;

namespace ClickIt
{
    internal sealed record MechanicPriorityEntry(string Id, string DisplayName);

    internal static class MechanicPriorityCatalog
    {
        public static readonly MechanicPriorityEntry[] Entries =
        [
            new(MechanicIds.AltarsSearingExarch, "Searing Exarch"),
            new(MechanicIds.AltarsEaterOfWorlds, "Eater of Worlds"),
            new(MechanicIds.Shrines, "Shrines"),
            new(MechanicIds.LostShipment, "Lost Shipment"),
            new(MechanicIds.UltimatumInitialOverlay, "Initial Ultimatum Overlay"),
            new(MechanicIds.UltimatumWindow, "Ultimatum Window"),
            new(MechanicIds.Essences, "Essences"),
            new(MechanicIds.Items, "Items"),
            new(MechanicIds.Strongboxes, "Strongboxes"),
            new(MechanicIds.BasicChests, "Basic Chests"),
            new(MechanicIds.LeagueChests, "League Mechanic Chests"),
            new(MechanicIds.Doors, "Doors"),
            new(MechanicIds.Levers, "Levers"),
            new(MechanicIds.AreaTransitions, "Area Transitions"),
            new(MechanicIds.LabyrinthTrials, "Labyrinth Trials"),
            new(MechanicIds.CraftingRecipes, "Crafting Recipes"),
            new(MechanicIds.Harvest, "Nearest Harvest Plot"),
            new(MechanicIds.Sanctum, "Sanctum"),
            new(MechanicIds.Betrayal, "Betrayal"),
            new(MechanicIds.Blight, "Blight"),
            new(MechanicIds.BreachNodes, "Breach Nodes"),
            new(MechanicIds.LegionPillars, "Legion Pillars"),
            new(MechanicIds.AlvaTempleDoors, "Alva Temple Doors"),
            new(MechanicIds.SettlersCrimsonIron, "Settlers Crimson Iron"),
            new(MechanicIds.SettlersCopper, "Settlers Copper"),
            new(MechanicIds.SettlersPetrifiedWood, "Settlers Petrified Wood"),
            new(MechanicIds.SettlersBismuth, "Settlers Bismuth"),
            new(MechanicIds.SettlersHourglass, "Settlers Hourglass"),
            new(MechanicIds.SettlersVerisium, "Settlers Verisium"),
            new(MechanicIds.RitualInitiate, "Ritual (Initiate)"),
            new(MechanicIds.RitualCompleted, "Ritual (Completed)"),
            new(MechanicIds.DelveSulphiteVeins, "Sulphite Veins"),
            new(MechanicIds.DelveAzuriteVeins, "Azurite Veins"),
            new(MechanicIds.DelveEncounterInitiators, "Delve Encounter Initiators")
        ];

        public static readonly IReadOnlyDictionary<string, MechanicPriorityEntry> EntriesById =
            new Dictionary<string, MechanicPriorityEntry>(Entries.ToDictionary(static x => x.Id, static x => x, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        public static readonly string[] DefaultOrderIds =
        [
            MechanicIds.Shrines,
            MechanicIds.LostShipment,
            MechanicIds.Items,
            MechanicIds.AltarsSearingExarch,
            MechanicIds.AltarsEaterOfWorlds,
            MechanicIds.Essences,
            MechanicIds.Strongboxes,
            MechanicIds.RitualInitiate,
            MechanicIds.RitualCompleted,
            MechanicIds.UltimatumInitialOverlay,
            MechanicIds.UltimatumWindow,
            MechanicIds.Harvest,
            MechanicIds.CraftingRecipes,
            MechanicIds.LeagueChests,
            MechanicIds.BasicChests,
            MechanicIds.Doors,
            MechanicIds.Levers,
            MechanicIds.AreaTransitions,
            MechanicIds.LabyrinthTrials,
            MechanicIds.Betrayal,
            MechanicIds.Sanctum,
            MechanicIds.Blight,
            MechanicIds.BreachNodes,
            MechanicIds.LegionPillars,
            MechanicIds.AlvaTempleDoors,
            MechanicIds.SettlersCrimsonIron,
            MechanicIds.SettlersCopper,
            MechanicIds.SettlersPetrifiedWood,
            MechanicIds.SettlersBismuth,
            MechanicIds.SettlersHourglass,
            MechanicIds.SettlersVerisium,
            MechanicIds.DelveSulphiteVeins,
            MechanicIds.DelveAzuriteVeins,
            MechanicIds.DelveEncounterInitiators
        ];

        public static readonly IReadOnlySet<string> Ids = new HashSet<string>(Entries.Select(static x => x.Id), StringComparer.OrdinalIgnoreCase);
    }
}