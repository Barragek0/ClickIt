using System;
using System.Collections.Generic;
using System.Linq;
using ClickIt.Definitions;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private sealed record ItemSubtypeDefinition(string Id, string DisplayName, IReadOnlyList<string> MetadataIdentifiers);

        private static readonly Dictionary<string, ItemSubtypeDefinition[]> ItemSubtypeCatalog = new(StringComparer.OrdinalIgnoreCase)
        {
            ["jewels"] =
            [
                new("regular-jewels", "Jewels", ["special:jewels-regular"]),
                new("abyss-jewels", "Abyss Jewels", ["Items/Jewels/JewelAbyss"]),
                new("cluster-jewels", "Cluster Jewels", ["Items/Jewels/JewelPassiveTreeExpansion"])
            ],
            ["armour"] =
            [
                new("helmets", "Helmets", ["Items/Armours/Helmets/"]),
                new("body-armours", "Body Armours", ["Items/Armours/BodyArmours/"]),
                new("gloves", "Gloves", ["Items/Armours/Gloves/"]),
                new("boots", "Boots", ["Items/Armours/Boots/"]),
                new("shields", "Shields", ["Items/Armours/Shields/"])
            ],
            ["weapons"] =
            [
                new("one-hand-swords", "One-Hand Swords", ["Items/Weapons/OneHandWeapons/OneHandSwords/"]),
                new("two-hand-swords", "Two-Hand Swords", ["Items/Weapons/TwoHandWeapons/TwoHandSwords/", "Items/Weapons/TwoHandWeapon/TwoHandSwords/"]),
                new("one-hand-axes", "One-Hand Axes", ["Items/Weapons/OneHandWeapons/OneHandAxes/"]),
                new("two-hand-axes", "Two-Hand Axes", ["Items/Weapons/TwoHandWeapons/TwoHandAxes/", "Items/Weapons/TwoHandWeapon/TwoHandAxes/"]),
                new("one-hand-maces", "One-Hand Maces", ["Items/Weapons/OneHandWeapons/OneHandMaces/"]),
                new("sceptres", "Sceptres", ["Items/Weapons/OneHandWeapons/Sceptres/"]),
                new("two-hand-maces", "Two-Hand Maces", ["Items/Weapons/TwoHandWeapons/TwoHandMaces/", "Items/Weapons/TwoHandWeapon/TwoHandMaces/"]),
                new("bows", "Bows", ["Items/Weapons/TwoHandWeapons/Bows/", "Items/Weapons/TwoHandWeapon/Bows/"]),
                new("wands", "Wands", ["Items/Weapons/OneHandWeapons/Wands/"]),
                new("daggers", "Daggers", ["Items/Weapons/OneHandWeapons/Daggers/"]),
                new("rune-daggers", "Rune Daggers", ["Items/Weapons/OneHandWeapons/RuneDaggers/"]),
                new("claws", "Claws", ["Items/Weapons/OneHandWeapons/Claws/"]),
                new("staves", "Staves", ["Items/Weapons/TwoHandWeapons/Staves/", "Items/Weapons/TwoHandWeapon/Staves/"]),
                new("warstaves", "Warstaves", ["Items/Weapons/TwoHandWeapons/Warstaves/", "Items/Weapons/TwoHandWeapon/Warstaves/"])
            ],
            ["flasks"] =
            [
                new("life", "Life Flasks", ["Items/Flasks/LifeFlask"]),
                new("mana", "Mana Flasks", ["Items/Flasks/ManaFlask"]),
                new("hybrid", "Hybrid Flasks", ["Items/Flasks/HybridFlask"]),
                new("utility", "Utility Flasks", ["Items/Flasks/UtilityFlask"])
            ]
        };

        private static readonly string[] EssenceSuffixes =
        [
            "Greed", "Contempt", "Hatred", "Woe",
            "Fear", "Anger", "Torment", "Sorrow",
            "Rage", "Suffering", "Wrath", "Doubt",
            "Loathing", "Zeal", "Anguish", "Spite",
            "Scorn", "Envy", "Misery", "Dread"
        ];

        private static readonly HashSet<string> EssenceMedsSuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Misery", "Envy", "Dread", "Scorn"
        };

        private static readonly string[] EssenceAllTableNames =
            EssenceSuffixes.SelectMany(suffix => new[]
            {
                $"Screaming Essence of {suffix}",
                $"Shrieking Essence of {suffix}",
                $"Deafening Essence of {suffix}"
            }).ToArray();

        private sealed record StrongboxFilterEntry(string Id, string DisplayName, string[] MetadataIdentifiers);

        private static readonly StrongboxFilterEntry[] StrongboxTableEntries =
        [
            new("regular", "Regular Strongbox (mixed loot)", ["StrongBoxes/Strongbox"]),
            new("arcanist", "Arcanist Strongbox (currency)", ["StrongBoxes/Arcanist"]),
            new("armourer", "Armourer Strongbox (armour)", ["StrongBoxes/Armory"]),
            new("artisan", "Artisan Strongbox (quality currency)", ["StrongBoxes/Artisan"]),
            new("blacksmith", "Blacksmith Strongbox (weapons)", ["StrongBoxes/Arsenal"]),
            new("cartographer", "Cartographer Strongbox (maps)", ["StrongBoxes/CartographerEndMaps"]),
            new("diviner", "Diviner Strongbox (divination cards)", ["StrongBoxes/StrongboxDivination"]),
            new("gemcutter", "Gemcutter Strongbox (gems)", ["StrongBoxes/Gemcutter"]),
            new("jeweller", "Jeweller Strongbox (jewellery)", ["StrongBoxes/Jeweller"]),
            new("large", "Large Strongbox (+ quantity)", ["StrongBoxes/Large"]),
            new("ornate", "Ornate Strongbox (+ rarity)", ["StrongBoxes/Ornate"]),
            new("operative", "Operative Strongbox (scarabs)", ["StrongBoxes/StrongboxScarab"]),
            new("opalescent", "Opalescent Strongbox (jewels)", ["StrongBox/StrongboxJewels", "StrongBoxes/StrongboxJewels"]),
            new("unique-strongbox", "Unique Strongboxes", ["special:strongbox-unique"])
        ];

        private static readonly Dictionary<string, StrongboxFilterEntry> StrongboxTableEntriesById =
            StrongboxTableEntries.ToDictionary(static x => x.Id, static x => x, StringComparer.OrdinalIgnoreCase);

        private static readonly string[] StrongboxDefaultClickIds =
        [
            "regular",
            "arcanist",
            "armourer",
            "artisan",
            "blacksmith",
            "cartographer",
            "diviner",
            "gemcutter",
            "jeweller",
            "large",
            "ornate",
            "operative",
            "opalescent"
        ];

        private sealed record MechanicPriorityEntry(string Id, string DisplayName);

        private static readonly MechanicPriorityEntry[] MechanicPriorityEntries =
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
            new(MechanicIds.SettlersVerisium, "Settlers Verisium"),
            new(MechanicIds.RitualInitiate, "Ritual (Initiate)"),
            new(MechanicIds.RitualCompleted, "Ritual (Completed)"),
            new(MechanicIds.DelveSulphiteVeins, "Sulphite Veins"),
            new(MechanicIds.DelveAzuriteVeins, "Azurite Veins"),
            new(MechanicIds.DelveEncounterInitiators, "Delve Encounter Initiators")
        ];

        private static readonly Dictionary<string, MechanicPriorityEntry> MechanicPriorityEntriesById =
            MechanicPriorityEntries.ToDictionary(static x => x.Id, static x => x, StringComparer.OrdinalIgnoreCase);

        private static readonly string[] MechanicPriorityDefaultOrderIds =
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
            MechanicIds.SettlersVerisium,
            MechanicIds.DelveSulphiteVeins,
            MechanicIds.DelveAzuriteVeins,
            MechanicIds.DelveEncounterInitiators
        ];

        private static readonly HashSet<string> MechanicPriorityIds = new(MechanicPriorityEntries.Select(static x => x.Id), StringComparer.OrdinalIgnoreCase);
    }
}