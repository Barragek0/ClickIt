using System;
using System.Collections.Generic;
using System.Linq;

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
            new("unique-strange-barrel", "Strange Barrel (unique strongbox)", ["name:Strange Barrel"]),
            new("unique-ashes-of-the-condemned", "Ashes of the Condemned (unique strongbox)", ["name:Ashes of the Condemned"]),
            new("unique-redblade-cache", "Redblade Cache (unique strongbox)", ["name:Redblade Cache"]),
            new("unique-brinerot-cache", "Brinerot Cache (unique strongbox)", ["name:Brinerot Cache"]),
            new("unique-mutewind-cache", "Mutewind Cache (unique strongbox)", ["name:Mutewind Cache"]),
            new("unique-renegades-cache", "Renegades Cache (unique strongbox)", ["name:Renegades Cache"]),
            new("unique-deshrets-storm", "Deshret's Storm (unique strongbox)", ["name:Deshret's Storm"]),
            new("unique-perandus-bank", "Perandus Bank (unique strongbox)", ["name:Perandus Bank"]),
            new("unique-empyrean-apparatus", "Empyrean Apparatus (unique strongbox)", ["name:Empyrean Apparatus"]),
            new("unique-grandmasters-arcanist-cache", "Grandmaster's Arcanist Cache (unique strongbox)", ["name:Grandmaster's Arcanist Cache"]),
            new("unique-grandmasters-cartography-cache", "Grandmaster's Cartography Cache (unique strongbox)", ["name:Grandmaster's Cartography Cache"]),
            new("unique-grandmasters-corrupted-cache", "Grandmaster's Corrupted Cache (unique strongbox)", ["name:Grandmaster's Corrupted Cache"]),
            new("unique-grandmasters-gemcutting-cache", "Grandmaster's Gemcutting Cache (unique strongbox)", ["name:Grandmaster's Gemcutting Cache"]),
            new("unique-grandmasters-large-cache", "Grandmaster's Large Cache (unique strongbox)", ["name:Grandmaster's Large Cache"]),
            new("unique-grandmasters-ornate-cache", "Grandmaster's Ornate Cache (unique strongbox)", ["name:Grandmaster's Ornate Cache"]),
            new("unique-grandmasters-treasury", "Grandmaster's Treasury (unique strongbox)", ["name:Grandmaster's Treasury"]),
            new("unique-grandmasters-trove", "Grandmaster's Trove (unique strongbox)", ["name:Grandmaster's Trove"]),
            new("unique-kaoms-cache", "Kaom's Cache (unique strongbox)", ["name:Kaom's Cache"]),
            new("unique-maelstrom-cell", "The Maelstrom Cell (unique strongbox)", ["name:The Maelstrom Cell", "name:Maelstrom Cell"]),
            new("unique-obas-glittering-stash", "Oba's Glittering Stash (unique strongbox)", ["name:Oba's Glittering Stash"]),
            new("unique-obas-prized-cache", "Oba's Prized Cache (unique strongbox)", ["name:Oba's Prized Cache"]),
            new("unique-obas-riches", "Oba's Riches (unique strongbox)", ["name:Oba's Riches"]),
            new("unique-weylams-war-chest", "Weylam's War Chest (unique strongbox)", ["name:Weylam's War Chest"])
        ];

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
            "operative"
        ];

        private sealed record MechanicPriorityEntry(string Id, string DisplayName);

        private static readonly MechanicPriorityEntry[] MechanicPriorityEntries =
        [
            new("altars", "Altars"),
            new("shrines", "Shrines"),
            new("ultimatum", "Ultimatum"),
            new("essences", "Essences"),
            new("items", "Items"),
            new("strongboxes", "Strongboxes"),
            new("basic-chests", "Basic Chests"),
            new("league-chests", "League Mechanic Chests"),
            new("doors", "Doors"),
            new("levers", "Levers"),
            new("area-transitions", "Area Transitions"),
            new("crafting-recipes", "Crafting Recipes"),
            new("harvest", "Nearest Harvest Plot"),
            new("sanctum", "Sanctum"),
            new("betrayal", "Betrayal"),
            new("blight", "Blight"),
            new("breach-nodes", "Breach Nodes"),
            new("legion-pillars", "Legion Pillars"),
            new("alva-temple-doors", "Alva Temple Doors"),
            new("settlers-ore", "Settlers Ore Deposits"),
            new("ritual-initiate", "Ritual (Initiate)"),
            new("ritual-completed", "Ritual (Completed)"),
            new("sulphite-veins", "Sulphite Veins"),
            new("azurite-veins", "Azurite Veins"),
            new("delve-spawners", "Delve Encounter Initiators")
        ];

        private static readonly string[] MechanicPriorityDefaultOrderIds =
        [
            "shrines",
            "items",
            "altars",
            "essences",
            "strongboxes",
            "ritual-initiate",
            "ritual-completed",
            "ultimatum",
            "harvest",
            "crafting-recipes",
            "league-chests",
            "basic-chests",
            "doors",
            "levers",
            "area-transitions",
            "betrayal",
            "sanctum",
            "blight",
            "breach-nodes",
            "legion-pillars",
            "alva-temple-doors",
            "settlers-ore",
            "sulphite-veins",
            "azurite-veins",
            "delve-spawners"
        ];

        private static readonly HashSet<string> MechanicPriorityIds = new(MechanicPriorityEntries.Select(static x => x.Id), StringComparer.OrdinalIgnoreCase);
    }
}