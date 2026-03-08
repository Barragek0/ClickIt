using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt.Constants
{
    internal enum ItemListKind
    {
        Whitelist,
        Blacklist
    }

    internal sealed record ItemCategoryDefinition(
        string Id,
        string DisplayName,
        IReadOnlyList<string> MetadataIdentifiers,
        ItemListKind DefaultList,
        IReadOnlyList<string> ExampleItems)
    {
        public ItemCategoryDefinition(string id, string displayName, IReadOnlyList<string> metadataIdentifiers, ItemListKind defaultList)
            : this(id, displayName, metadataIdentifiers, defaultList, [])
        {
        }

        public ItemCategoryDefinition(string id, string displayName, string metadataIdentifier, ItemListKind defaultList)
            : this(id, displayName, [metadataIdentifier], defaultList, [])
        {
        }

        public ItemCategoryDefinition(string id, string displayName, string metadataIdentifier, ItemListKind defaultList, IReadOnlyList<string> exampleItems)
            : this(id, displayName, [metadataIdentifier], defaultList, exampleItems)
        {
        }
    }

    internal static class ItemCategoryCatalog
    {
        // Source for item-class coverage and naming: PoE Wiki Item class pages.
        // This catalog intentionally excludes historical/removed/event-only/non-ground-loot classes.
        // Metadata identifiers here are substring match fragments against ItemEntity metadata/path.
        public static readonly ItemCategoryDefinition[] All =
        [
            // High-value defaults.
            new("currency", "Currency", "Items/Currency/", ItemListKind.Whitelist, ["Chaos Orb", "Divine Orb", "Orb of Alchemy"]),
            new("unique-items", "Unique Items", ["special:unique-items"], ItemListKind.Whitelist, ["Tabula Rasa", "Headhunter"]),
            new("inscribed-ultimatums", "Inscribed Ultimatums", ["special:inscribed-ultimatum"], ItemListKind.Whitelist, ["Inscribed Ultimatum"]),
            new("scarabs", "Scarabs", ["Items/Scarabs/", "Items/Currency/Scarabs/"], ItemListKind.Whitelist, ["Ambush Scarab", "Divination Scarab"]),
            // PoE Wiki: Contract page separates normal contracts from quest contracts.
            new("heist-contracts", "Heist Contracts", ["special:heist-non-quest-contract"], ItemListKind.Whitelist, ["Contract: Bunker", "Contract: Smuggler's Den"]),
            new("heist-quest-contracts", "Heist Quest Contracts", ["special:heist-quest-contract"], ItemListKind.Whitelist, ["Contract: Trial Run", "Contract: The Finest Costumes"]),
            new("rogue-markers", "Rogue Markers", "Items/Heist/HeistCoin", ItemListKind.Whitelist, ["Rogue's Marker"]),

            // Core equipment.
            new("armour", "Armour", "Items/Armours/", ItemListKind.Blacklist, ["Iron Hat", "Astral Plate"]),
            new("weapons", "Weapons", "Items/Weapons/", ItemListKind.Blacklist, ["Rusted Sword", "Driftwood Wand"]),
            new("amulets", "Amulets", "Items/Amulets/", ItemListKind.Blacklist, ["Coral Amulet", "Citrine Amulet"]),
            new("belts", "Belts", "Items/Belts/", ItemListKind.Blacklist, ["Leather Belt", "Stygian Vise"]),
            new("rings", "Rings", "Items/Rings/", ItemListKind.Blacklist, ["Iron Ring", "Amethyst Ring"]),
            new("quivers", "Quivers", "Items/Quivers/", ItemListKind.Blacklist, ["Two-Point Arrow Quiver", "Penetrating Arrow Quiver"]),

            // Character progression/socketables.
            new("gems", "Skill Gems", "Items/Gems/", ItemListKind.Blacklist, ["Fireball", "Added Lightning Damage Support"]),
            new("jewels", "Jewels", "Items/Jewels/", ItemListKind.Blacklist, ["Crimson Jewel", "Murderous Eye Jewel", "Large Cluster Jewel"]),

            // Flask and flask-adjacent.
            new("flasks", "Flasks", "Items/Flasks/", ItemListKind.Blacklist, ["Divine Life Flask", "Granite Flask"]),
            new("tinctures", "Tinctures", "Items/Tinctures/", ItemListKind.Blacklist, ["Prismatic Tincture", "Ironwood Tincture"]),

            // Mapping and pinnacle access.
            new("maps", "Maps", "Items/Maps/", ItemListKind.Whitelist, ["Map (Tier 1)", "Map (Tier 16)"]),
            new("memory-lines", "Memories", ["Items/MemoryLines/", "Items/Currency/MemoryLine/"], ItemListKind.Whitelist, ["Niko's Memory", "Alva's Memory"]),
            new("map-fragments", "Map Fragments", "Items/MapFragments/", ItemListKind.Whitelist, ["Offering to the Goddess", "Sacrifice at Dusk"]),
            new("unique-fragments", "Item Fragments", "Items/UniqueFragments/", ItemListKind.Whitelist, ["Beachhead Weapon Pieces", "Atziri Vaal Aspects"]),
            new("breachstones", "Breachstones", ["Items/Breach/Breachstone", "Items/Currency/Breach/"], ItemListKind.Whitelist, ["Chayula's Breachstone", "Xoph's Breachstone"]),
            new("atlas-upgrade-items", "Atlas Upgrade Items", "Items/AtlasUpgrades/", ItemListKind.Whitelist, ["Voidstone"]),
            new("vault-keys", "Vault Keys", "VaultKey", ItemListKind.Whitelist, ["Vaal Reliquary Key", "Decaying Reliquary Key"]),

            // Side-content reward classes.
            new("divination-cards", "Divination Cards", "Items/DivinationCards/", ItemListKind.Whitelist, ["The Doctor", "The Apothecary"]),
            new("incubators", "Incubators", ["Items/Incubator/", "Items/Currency/Incubation/"], ItemListKind.Whitelist, ["Skittering Incubator", "Diviner's Incubator"]),
            new("resonators", "Resonators", ["Items/Resonators/", "Items/Delve/DelveStackableSocketableCurrencyReroll", "Items/Currency/Delve/Reroll"], ItemListKind.Whitelist, ["Primitive Chaotic Resonator", "Prime Chaotic Resonator"]),
            new("fossils", "Fossils", ["Items/Fossils/", "Items/Currency/Delve/", "Items/Currency/CurrencyDelveCrafting"], ItemListKind.Whitelist, ["Jagged Fossil", "Faceted Fossil"]),
            new("expedition-logbooks", "Expedition Logbooks", "Items/Expedition/ExpeditionLogbook", ItemListKind.Whitelist, ["Expedition Logbook", "Black Scythe Logbook"]),
            new("tattoos", "Tattoos", ["Items/Tattoos/", "Items/Currency/Ancestors/"], ItemListKind.Whitelist, ["Tattoo of the Arohongui Warrior", "Tattoo of the Ramako Archer"]),
            new("runegrafts", "Runegrafts", ["Items/Runegrafts/", "Items/Currency/Settlers/VillageRune", "Items/Currency/KalguuranRune"], ItemListKind.Whitelist, ["Bound Rune", "Life Rune"]),
            new("omens", "Omens", ["Items/Omens/", "Items/Currency/Azmeri/VoodooOmens", "Items/Currency/AncestralOmen"], ItemListKind.Whitelist, ["Omen of Amelioration", "Omen of Fortune"]),
            new("corpses", "Corpse Items", ["Items/Corpses/", "Items/Currency/Azmeri/", "Items/Currency/Necropolis/"], ItemListKind.Whitelist, ["Hyrri's Corpse", "Admiral Darnaw's Corpse"]),

            // Heist.
            new("heist-blueprints", "Heist Blueprints", ["Items/Heist/HeistBlueprint", "Items/Currency/Heist/Blueprint"], ItemListKind.Whitelist, ["Blueprint: Smuggler's Den", "Blueprint: Tunnels"]),
            new("heist-targets", "Heist Targets", "Items/Heist/HeistFinalObjective", ItemListKind.Whitelist, ["Urn of Farud", "Staff of the First Sin"]),
            new("heist-brooches", "Heist Brooches", "Items/Heist/HeistEquipmentReward", ItemListKind.Whitelist, ["Steel Brooch", "Golden Brooch"]),
            new("heist-cloaks", "Heist Cloaks", "Items/Heist/HeistEquipmentUtility", ItemListKind.Whitelist, ["Reinforced Cloak", "Whisper-woven Cloak"]),
            new("heist-gear", "Heist Gear", "Items/Heist/HeistEquipmentWeapon", ItemListKind.Whitelist, ["Sharpening Stone", "Obsidian Sharpening Stone"]),
            new("heist-tools", "Heist Tools", "Items/Heist/HeistEquipment", ItemListKind.Whitelist, ["Fine Lockpick", "Precise Lockpick"]),
            new("trinkets", "Trinkets", ["Items/Trinkets/", "Items/Currency/Heist/LuckCharm"], ItemListKind.Whitelist, ["Thief's Trinket", "Polished Thief's Trinket"]),

            // Sanctum / lab / special.
            new("relics", "Relics", "Items/Relics/", ItemListKind.Whitelist, ["Sanctified Relic", "The Original Sin"]),
            new("sanctum-research", "Sanctum Research", ["Items/Sanctum/ItemisedSanctum", "Items/Sanctum/SanctumKey", "Items/Currency/Sanctum/SanctumKey"], ItemListKind.Whitelist, ["Forbidden Tome", "Original Scripture"]),
            new("captured-souls", "Captured Souls", ["Items/PantheonSouls/", "Items/MapFragments/CurrencyFragmentPantheonFlask"], ItemListKind.Whitelist, ["Divine Vessel", "Captured Soul of Lunaris"]),
            new("labyrinth-items", "Labyrinth Keys", "Items/Labyrinth/", ItemListKind.Whitelist, ["Silver Key", "Golden Key"]),
            new("labyrinth-trinkets", "Labyrinth Trinkets", "Items/Labyrinth/Trinket", ItemListKind.Whitelist, ["Bane of the Loyal", "Cogs of Disruption"]),

            // Edge / novelty.
            new("fishing-rods", "Fishing Rods", ["Items/FishingRods/", "Items/Weapons/TwoHandWeapons/FishingRods/", "Items/Weapons/TwoHandWeapon/FishingRods/"], ItemListKind.Whitelist, ["Fishing Rod", "Reefbane"]),
            new("gold", "Gold", ["Items/Gold/", "Items/Currency/GoldCoin"], ItemListKind.Blacklist, ["Gold"])
        ];

        public static readonly HashSet<string> DefaultWhitelistIds =
            new(All.Where(x => x.DefaultList == ItemListKind.Whitelist).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);

        public static readonly HashSet<string> DefaultBlacklistIds =
            new(All.Where(x => x.DefaultList == ItemListKind.Blacklist).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);

        public static readonly HashSet<string> AllIds =
            new(All.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);

        public static bool TryGet(string id, out ItemCategoryDefinition category)
        {
            ItemCategoryDefinition? match = All.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            category = match ?? default!;
            return match != null;
        }
    }
}
