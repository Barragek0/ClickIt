namespace ClickIt
{
    public partial class ClickItSettings
    {
        private static Dictionary<string, TEntry> BuildIdLookup<TEntry>(IEnumerable<TEntry> entries, Func<TEntry, string> idSelector)
        {
            return entries.ToDictionary(idSelector, static x => x, StringComparer.OrdinalIgnoreCase);
        }

        internal sealed record ItemSubtypeDefinition(string Id, string DisplayName, IReadOnlyList<string> MetadataIdentifiers);

        internal static readonly Dictionary<string, ItemSubtypeDefinition[]> ItemSubtypeCatalog = new(StringComparer.OrdinalIgnoreCase)
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
            ],
            ["wombgifts"] =
            [
                new("mysterious-wombgift", "Mysterious Wombgift", ["special:mysterious-wombgift-label"]),
                new("provisioning-wombgift", "Provisioning Wombgift", ["Chayula/EquipmentFruit"]),
                new("lavish-wombgift", "Lavish Wombgift", ["Chayula/CurrencyFruit"]),
                new("ancient-wombgift", "Ancient Wombgift", ["Chayula/UniqueItemFruit"])
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

        internal static readonly HashSet<string> EssenceMedsSuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Misery", "Envy", "Dread", "Scorn"
        };

        internal static readonly string[] EssenceAllTableNames =
            [.. EssenceSuffixes.SelectMany(suffix => new[]
            {
                $"Screaming Essence of {suffix}",
                $"Shrieking Essence of {suffix}",
                $"Deafening Essence of {suffix}"
            })];

        internal sealed record StrongboxFilterEntry(string Id, string DisplayName, string[] MetadataIdentifiers);

        internal static readonly StrongboxFilterEntry[] StrongboxTableEntries =
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

        internal static readonly Dictionary<string, StrongboxFilterEntry> StrongboxTableEntriesById =
            BuildIdLookup(StrongboxTableEntries, static x => x.Id);

        internal static readonly string[] StrongboxDefaultClickIds =
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

    }
}