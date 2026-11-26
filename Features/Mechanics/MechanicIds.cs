namespace ClickIt.Features.Mechanics
{
    public static class MechanicIds
    {
        public const string Shrines = "shrines";
        public const string Items = "items";
        public const string BasicChests = "basic-chests";
        public const string LeagueChests = "league-chests";
        public const string LeagueChestsOther = "league-chests-other";
        public const string MirageGoldenDjinnCache = "mirage-golden-djinn-cache";
        public const string MirageSilverDjinnCache = "mirage-silver-djinn-cache";
        public const string MirageBronzeDjinnCache = "mirage-bronze-djinn-cache";
        public const string HeistSecureLocker = "heist-secure-locker";
        public const string HeistSecureRepository = "heist-secure-repository";
        public const string HeistHazards = "heist-hazards";
        public const string BreachGraspingCoffers = "breach-grasping-coffers";
        public const string DelveResonatorStash = "delve-resonator-stash";
        public const string DelveFossil = "delve-fossil";
        public const string DelveMaps = "delve-maps";
        public const string DelveFlareChests = "delve-flare-chests";
        public const string DelveDynamiteChests = "delve-dynamite-chests";
        public const string DelveSpecialArmour = "delve-special-armour";
        public const string DelveSpecialWeapon = "delve-special-weapon";
        public const string DelveSpecialTrinket = "delve-special-trinket";
        public const string DelveCurrency = "delve-currency";
        public const string DelveFallbackJewellery = "delve-fallback-jewellery";
        public const string DelveFallbackArmour = "delve-fallback-armour";
        public const string DelveFallbackWeapons = "delve-fallback-weapons";
        public const string BlightCyst = "blight-cyst";
        public const string LegionChest = "legion-chest";
        public const string SynthesisSynthesisedStash = "synthesis-synthesised-stash";
        public const string AllflameCursedTreasure = "allflame-cursed-treasure";
        public const string AllflameBrinerotPlunder = "allflame-brinerot-plunder";
        public const string AllflameCoralNest = "allflame-coral-nest";
        public const string Doors = "doors";
        public const string HeistDoors = "heist-doors";
        public const string Levers = "levers";
        public const string AreaTransitions = "area-transitions";
        public const string LabyrinthTrials = "labyrinth-trials";
        public const string Harvest = "harvest";
        public const string DelveSulphiteVeins = "delve-sulphite-veins";
        public const string Strongboxes = "strongboxes";
        public const string Sanctum = "sanctum";
        public const string Betrayal = "betrayal";
        public const string BetrayalCorpseSculpturePath = "Betrayal/BetrayalCorpseSculpture";
        public const string Blight = "blight";
        public const string AlvaTempleDoors = "alva-temple-doors";
        public const string LegionPillars = "legion-pillars";
        public const string Beyond = "beyond";
        public const string Sentinel = "sentinel";
        public const string Fates = "fates";
        internal const string FatesBoss = "fates-boss";
        public const string Memories = "memories";
        public const string BeyondInitiatorPath = "BeyondDemons/BeyondInitiator";
        public const string SentinelEncounterPath = "Sentinel/SentinelRandomEncounter";
        public const string FatesPath = "Augury/Objects/AuguryCardObject";
        public const string FatesBossPath = "Augury/Objects/AuguryBoss";
        public const string BeginMemoryLinePath = "MapAtlasMemory/Objects/BeginMemoryLine";
        public const string DelveAzuriteVeins = "delve-azurite-veins";
        public const string UltimatumInitialOverlay = "ultimatum-initial-overlay";
        public const string UltimatumWindow = "ultimatum-window";
        public const string DelveEncounterInitiators = "delve-encounter-initiators";
        public const string DelveCrawler = "delve-crawler";
        public const string DelveCrawlerPath = "Delve/Objects/DelveNode";
        public const string IceCavesFossil = "delve-ice-caves-fossil";

        internal static bool IsIceCavesFossilPath(string? path)
            => path?.Contains("EncounterControlObjects/IceCavesFossil", StringComparison.OrdinalIgnoreCase) == true;

        internal static int GetFallbackTier(string? mechanicId)
            => mechanicId switch
            {
                DelveCrawler => 2,
                IceCavesFossil or FatesBoss => 1,
                _ => 0
            };
        public const string CraftingRecipes = "crafting-recipes";
        public const string BreachNodes = "breach-nodes";
        public const string SettlersCrimsonIron = "settlers-crimson-iron";
        public const string SettlersCopper = "settlers-copper";
        public const string SettlersPetrifiedWood = "settlers-petrified-wood";
        public const string SettlersBismuth = "settlers-bismuth";
        public const string SettlersHourglass = "settlers-hourglass";
        public const string SettlersVerisium = "settlers-verisium";
        public const string AltarsSearingExarch = "altars-searing-exarch";
        public const string AltarsEaterOfWorlds = "altars-eater-of-worlds";
        public const string Essences = "essences";
        public const string RitualInitiate = "ritual-initiate";
        public const string RitualCompleted = "ritual-completed";
        public const string LostShipment = "lost-shipment";

        public const string SettlersNodeTypeRootPath = "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/";
        public const string SettlersCrimsonIronMarker = SettlersNodeTypeRootPath + "CrimsonIron";
        public const string SettlersCopperMarker = SettlersNodeTypeRootPath + "DemonCopperObjects/copper_altar";
        public const string SettlersPetrifiedWoodMarker = SettlersNodeTypeRootPath + "PetrifiedWood";
        public const string SettlersBismuthMarker = SettlersNodeTypeRootPath + "Bismuth";
        public const string SettlersHourglassMarker = SettlersNodeTypeRootPath + "HourglassObjects/" + Constants.Hourglass;
        public const string SettlersVerisiumMarker = SettlersNodeTypeRootPath + "Verisium";
        public const string VerisiumBossSubAreaTransitionPathMarker = "VerisiumBossSubAreaTransition";

        /// <summary>
        /// Whether a path belongs to the Augury league mechanic: a Cauldron's Fate card, or the area boss the collected
        /// Fates are invoked on. Both follow the single "Fates" entry in the click table.
        /// </summary>
        public static bool IsFatesPath(string? path)
            => IsFatesCardPath(path) || IsFatesBossPath(path);

        internal static bool IsFatesCardPath(string? path)
            => path?.Contains(FatesPath, StringComparison.OrdinalIgnoreCase) == true;

        internal static bool IsFatesBossPath(string? path)
            => path?.Contains(FatesBossPath, StringComparison.OrdinalIgnoreCase) == true;

        public static bool IsDelveCrawlerPath(string? path)
            => !string.IsNullOrEmpty(path)
                && path.Contains(DelveCrawlerPath, StringComparison.OrdinalIgnoreCase);

        public static bool IsTangledFossilInitiatorPath(string? path)
            => !string.IsNullOrEmpty(path)
                && path.Contains("DelveChests/TangledFossil", StringComparison.OrdinalIgnoreCase);

        public static bool IsDelveRewardChestPath(string? path)
            => !string.IsNullOrEmpty(path)
                && path.StartsWith("Metadata/Chests/DelveChests/", StringComparison.OrdinalIgnoreCase)
                && !IsTangledFossilInitiatorPath(path);

        public static bool IsDelveOffscreenWalkTargetPath(string? path)
            => !string.IsNullOrEmpty(path)
                && (path.StartsWith("Metadata/Chests/DelveChests/", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        path,
                        "Metadata/Terrain/Leagues/Delve/Objects/EncounterControlObjects/AzuriteEncounterController",
                        StringComparison.OrdinalIgnoreCase));
    }
}
