using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;

#nullable enable

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const string StrongboxUniqueIdentifier = "special:strongbox-unique";
        private const string MechanicItems = "items";
        private const string MechanicBasicChests = "basic-chests";
        private const string MechanicLeagueChests = "league-chests";
        private const string MechanicDoors = "doors";
        private const string MechanicLevers = "levers";
        private const string MechanicAreaTransitions = "area-transitions";
        private const string MechanicLabyrinthTrials = "labyrinth-trials";
        private const string MechanicHarvest = "harvest";
        private const string MechanicDelveSulphiteVeins = "delve-sulphite-veins";
        private const string MechanicStrongboxes = "strongboxes";
        private const string MechanicSanctum = "sanctum";
        private const string MechanicBetrayal = "betrayal";
        private const string MechanicBlight = "blight";
        private const string MechanicAlvaTempleDoors = "alva-temple-doors";
        private const string MechanicLegionPillars = "legion-pillars";
        private const string MechanicDelveAzuriteVeins = "delve-azurite-veins";
        private const string MechanicUltimatumInitialOverlay = "ultimatum-initial-overlay";
        private const string MechanicDelveEncounterInitiators = "delve-encounter-initiators";
        private const string MechanicCraftingRecipes = "crafting-recipes";
        private const string MechanicBreachNodes = "breach-nodes";
        private const string MechanicSettlersCrimsonIron = "settlers-crimson-iron";
        private const string MechanicSettlersCopper = "settlers-copper";
        private const string MechanicSettlersPetrifiedWood = "settlers-petrified-wood";
        private const string MechanicSettlersBismuth = "settlers-bismuth";
        private const string MechanicSettlersVerisium = "settlers-verisium";
        private const string SettlersNodeTypeRootPath = "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/";
        private const string SettlersCrimsonIronMarker = SettlersNodeTypeRootPath + "CrimsonIron";
        private const string SettlersCopperMarker = SettlersNodeTypeRootPath + "DemonCopperObjects/copper_altar";
        private const string SettlersPetrifiedWoodMarker = SettlersNodeTypeRootPath + "PetrifiedWood";
        private const string SettlersBismuthMarker = SettlersNodeTypeRootPath + "Bismuth";
        private const string SettlersVerisiumMarker = SettlersNodeTypeRootPath + "Verisium";
        private const string VerisiumBossSubAreaTransitionPathMarker = "VerisiumBossSubAreaTransition";
        private const string MechanicAltarsSearingExarch = "altars-searing-exarch";
        private const string MechanicAltarsEaterOfWorlds = "altars-eater-of-worlds";
        private const string MechanicEssences = "essences";
        private const string MechanicRitualInitiate = "ritual-initiate";
        private const string MechanicRitualCompleted = "ritual-completed";

        private static string? GetClickableMechanicId(LabelOnGround label, Entity item, ClickSettings settings, ExileCore.GameController? gameController)
        {
            EntityType type = item.Type;
            string path = type == EntityType.WorldItem
                ? GetWorldItemMetadataPath(item)
                : (item.Path ?? string.Empty);

            string? specialMechanicId = GetSpecialPathMechanicId(settings, path, label);
            if (!string.IsNullOrWhiteSpace(specialMechanicId))
                return specialMechanicId;

            string? altarMechanicId = GetAltarMechanicId(settings, path);
            if (!string.IsNullOrWhiteSpace(altarMechanicId))
                return altarMechanicId;

            if (ShouldClickEssence(settings.ClickEssences, label))
                return MechanicEssences;

            string? ritualMechanicId = GetRitualMechanicId(settings.ClickRitualInitiate, settings.ClickRitualCompleted, path, label);
            if (!string.IsNullOrWhiteSpace(ritualMechanicId))
                return ritualMechanicId;

            if (type == EntityType.WorldItem && !ShouldAllowWorldItemByMetadata(settings, item, gameController))
                return null;
            if (ShouldClickWorldItemCore(settings.ClickItems, type, item))
                return MechanicItems;

            string? chestMechanicId = GetChestMechanicId(settings.ClickBasicChests, settings.ClickLeagueChests, type, label);
            if (!string.IsNullOrWhiteSpace(chestMechanicId))
                return chestMechanicId;

            string? namedMechanicId = GetNamedInteractableMechanicId(settings.ClickDoors, settings.ClickLevers, item.RenderName, path);
            if (!string.IsNullOrWhiteSpace(namedMechanicId))
                return namedMechanicId;

            string? transitionMechanicId = GetAreaTransitionMechanicId(settings.ClickAreaTransitions, settings.ClickLabyrinthTrials, type, path);
            if (!string.IsNullOrWhiteSpace(transitionMechanicId))
                return transitionMechanicId;

            return null;
        }

        private static string? GetNamedInteractableMechanicId(bool clickDoors, bool clickLevers, string? renderName, string? metadataPath)
        {
            string path = string.IsNullOrWhiteSpace(metadataPath) ? string.Empty : metadataPath.Trim();

            bool isDoor = path.Contains("MiscellaneousObjects/Lights", StringComparison.OrdinalIgnoreCase)
                || path.Contains("MiscellaneousObjects/Door", StringComparison.OrdinalIgnoreCase);
            bool isLever = path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);

            if (clickDoors && isDoor)
                return MechanicDoors;
            if (clickLevers && isLever)
                return MechanicLevers;

            return null;
        }

        private static string? GetAreaTransitionMechanicId(bool clickAreaTransitions, bool clickLabyrinthTrials, EntityType type, string path)
        {
            bool isAreaTransition = type == EntityType.AreaTransition || path.Contains("AreaTransition", StringComparison.OrdinalIgnoreCase);
            if (!isAreaTransition)
                return null;

            if (IsLabyrinthTrialTransitionPath(path))
            {
                return clickLabyrinthTrials ? MechanicLabyrinthTrials : null;
            }

            return clickAreaTransitions ? MechanicAreaTransitions : null;
        }

        private static bool IsLabyrinthTrialTransitionPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.Contains("LabyrinthTrial", StringComparison.OrdinalIgnoreCase)
                || path.Contains("Labyrinth/Trial", StringComparison.OrdinalIgnoreCase)
                || path.Contains("TrialPortal", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldClickWorldItemCore(bool clickItems, EntityType type, Entity item)
        {
            if (!clickItems || type != EntityType.WorldItem)
                return false;

            // Prevent strongboxes from being clicked as items.
            string? itemPath = item.Path;
            if (!string.IsNullOrEmpty(itemPath) && itemPath.ToLowerInvariant().Contains("strongbox"))
                return false;

            return true;
        }

        private static string? GetChestMechanicId(bool clickBasicChests, bool clickLeagueChests, EntityType type, LabelOnGround label)
        {
            string? path = label.ItemOnGround?.Path;
            string renderName = label.ItemOnGround?.RenderName ?? string.Empty;
            return GetChestMechanicIdInternal(clickBasicChests, clickLeagueChests, type, path, renderName);
        }

        private static string? GetChestMechanicIdInternal(bool clickBasicChests, bool clickLeagueChests, EntityType type, string? path, string renderName)
        {
            if (type != EntityType.Chest)
                return null;

            // Avoid treating strongboxes as generic chests; strongboxes have their own settings.
            if (!string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains("strongbox"))
                return null;

            bool isBasicChest = IsBasicChestName(renderName);
            if (clickBasicChests && isBasicChest)
                return MechanicBasicChests;
            if (clickLeagueChests && !isBasicChest)
                return MechanicLeagueChests;

            return null;
        }

        private static string? GetSpecialPathMechanicId(ClickSettings settings, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (TryGetSettlersOreMechanicId(path, out string? settlersMechanicId) && !string.IsNullOrWhiteSpace(settlersMechanicId))
            {
                if (string.Equals(settlersMechanicId, MechanicSettlersCrimsonIron, StringComparison.OrdinalIgnoreCase))
                    return settings.ClickSettlersCrimsonIron ? MechanicSettlersCrimsonIron : null;

                if (string.Equals(settlersMechanicId, MechanicSettlersCopper, StringComparison.OrdinalIgnoreCase))
                    return settings.ClickSettlersCopper ? MechanicSettlersCopper : null;

                if (string.Equals(settlersMechanicId, MechanicSettlersPetrifiedWood, StringComparison.OrdinalIgnoreCase))
                    return settings.ClickSettlersPetrifiedWood ? MechanicSettlersPetrifiedWood : null;

                if (string.Equals(settlersMechanicId, MechanicSettlersBismuth, StringComparison.OrdinalIgnoreCase))
                    return settings.ClickSettlersBismuth ? MechanicSettlersBismuth : null;

                if (string.Equals(settlersMechanicId, MechanicSettlersVerisium, StringComparison.OrdinalIgnoreCase))
                    return settings.ClickSettlersVerisium ? MechanicSettlersVerisium : null;
            }

            bool strongboxesEnabled = settings.StrongboxClickMetadata?.Count > 0;

            var checks = new (bool On, string MechanicId, Func<string, bool> Matches)[]
            {
                (settings.NearestHarvest, MechanicHarvest, static p => IsHarvestPath(p)),
                (settings.ClickSulphite, MechanicDelveSulphiteVeins, static p => p.Contains("DelveMineral", StringComparison.OrdinalIgnoreCase)),
                (strongboxesEnabled, MechanicStrongboxes, p => ShouldClickStrongbox(settings, p, label)),
                (settings.ClickSanctum, MechanicSanctum, static p => p.Contains("Sanctum", StringComparison.OrdinalIgnoreCase)),
                (settings.ClickBetrayal, MechanicBetrayal, static p => p.Contains("BetrayalMakeChoice", StringComparison.OrdinalIgnoreCase)),
                (settings.ClickBlight, MechanicBlight, static p => p.Contains("BlightPump", StringComparison.OrdinalIgnoreCase)),
                (settings.ClickAlvaTempleDoors, MechanicAlvaTempleDoors, static p => p.Contains(Constants.ClosedDoorPast, StringComparison.OrdinalIgnoreCase)),
                (settings.ClickLegionPillars, MechanicLegionPillars, static p => p.Contains(Constants.LegionInitiator, StringComparison.OrdinalIgnoreCase)),
                (settings.ClickAzurite, MechanicDelveAzuriteVeins, static p => p.Contains("AzuriteEncounterController", StringComparison.OrdinalIgnoreCase)),
                (settings.ClickInitialUltimatum, MechanicUltimatumInitialOverlay, static p => IsUltimatumPath(p)),
                (settings.ClickDelveSpawners, MechanicDelveEncounterInitiators, static p => p.Contains("Delve/Objects/Encounter", StringComparison.OrdinalIgnoreCase)),
                (settings.ClickCrafting, MechanicCraftingRecipes, static p => p.Contains("CraftingUnlocks", StringComparison.OrdinalIgnoreCase)),
                (settings.ClickBreach, MechanicBreachNodes, static p => p.Contains(Constants.Brequel, StringComparison.OrdinalIgnoreCase))
            };

            foreach ((bool on, string mechanicId, Func<string, bool> matches) in checks)
            {
                if (!on)
                    continue;
                if (matches(path))
                    return mechanicId;
            }

            return null;
        }

        internal static bool TryGetSettlersOreMechanicId(string? path, out string? mechanicId)
        {
            mechanicId = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (IsSettlersCrimsonIronPath(path))
            {
                mechanicId = MechanicSettlersCrimsonIron;
                return true;
            }

            if (IsSettlersCopperPath(path))
            {
                mechanicId = MechanicSettlersCopper;
                return true;
            }

            if (IsSettlersPetrifiedWoodPath(path))
            {
                mechanicId = MechanicSettlersPetrifiedWood;
                return true;
            }

            if (IsSettlersBismuthPath(path))
            {
                mechanicId = MechanicSettlersBismuth;
                return true;
            }

            if (IsSettlersVerisiumPath(path))
            {
                mechanicId = MechanicSettlersVerisium;
                return true;
            }

            return false;
        }

        private static bool IsHarvestPath(string path)
        {
            return path.Contains("Harvest/Irrigator", StringComparison.OrdinalIgnoreCase)
                || path.Contains("Harvest/Extractor", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSettlersOrePath(string path)
        {
            return IsSettlersCrimsonIronPath(path)
                || IsSettlersCopperPath(path)
                || IsSettlersPetrifiedWoodPath(path)
                || IsSettlersBismuthPath(path)
                || IsSettlersVerisiumPath(path)
                || path.Contains(Constants.Hourglass, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSettlersCrimsonIronPath(string path)
        {
            return path.Contains(Constants.CrimsonIron, StringComparison.OrdinalIgnoreCase)
                || MatchesSettlersOrePathMarker(path, SettlersCrimsonIronMarker);
        }

        private static bool IsSettlersCopperPath(string path)
        {
            return MatchesSettlersOrePathMarker(path, SettlersCopperMarker);
        }

        private static bool IsSettlersPetrifiedWoodPath(string path)
        {
            return path.Contains(Constants.PetrifiedWood, StringComparison.OrdinalIgnoreCase)
                || MatchesSettlersOrePathMarker(path, SettlersPetrifiedWoodMarker);
        }

        private static bool IsSettlersBismuthPath(string path)
        {
            return path.Contains(Constants.Bismuth, StringComparison.OrdinalIgnoreCase)
                || MatchesSettlersOrePathMarker(path, SettlersBismuthMarker);
        }

        private static bool IsSettlersVerisiumPath(string path)
        {
            return (path.Contains(Constants.Verisium, StringComparison.OrdinalIgnoreCase)
                    || MatchesSettlersOrePathMarker(path, SettlersVerisiumMarker))
                && !path.Contains(VerisiumBossSubAreaTransitionPathMarker, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesSettlersOrePathMarker(string path, string fullMarker)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(fullMarker))
                return false;

            if (string.Equals(path, fullMarker, StringComparison.OrdinalIgnoreCase))
                return true;

            string markerWithSlash = fullMarker + "/";
            return path.StartsWith(markerWithSlash, StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetAltarMechanicId(ClickSettings settings, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if ((settings.HighlightExarch || settings.ClickExarch)
                && path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
            {
                return MechanicAltarsSearingExarch;
            }

            if ((settings.HighlightEater || settings.ClickEater)
                && path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
            {
                return MechanicAltarsEaterOfWorlds;
            }

            return null;
        }

        private static bool IsUltimatumPath(string path)
        {
            return Constants.IsUltimatumInteractablePath(path);
        }

        private static bool ShouldClickAltar(bool highlightEater, bool highlightExarch, bool clickEater, bool clickExarch, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return (highlightEater || highlightExarch || clickEater || clickExarch)
                && (path.Contains(Constants.CleansingFireAltar)
                    || path.Contains(Constants.TangleAltar));
        }

        private static bool ShouldClickEssence(bool clickEssences, LabelOnGround label)
        {
            if (!clickEssences)
                return false;

            return LabelUtils.GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

        private static string? GetRitualMechanicId(bool clickRitualInitiate, bool clickRitualCompleted, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("Leagues/Ritual"))
                return null;

            bool hasFavoursText = LabelUtils.GetElementByString(label.Label, "Interact to view Favours") != null;
            if (clickRitualInitiate && !hasFavoursText)
                return MechanicRitualInitiate;
            if (clickRitualCompleted && hasFavoursText)
                return MechanicRitualCompleted;

            return null;
        }

        private static bool ShouldClickStrongbox(ClickSettings settings, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (label?.ItemOnGround == null)
                return false;

            Chest? chest = label.ItemOnGround.GetComponent<Chest>();
            if (chest?.IsLocked != false)
                return false;

            IReadOnlyList<string> clickMetadata = settings.StrongboxClickMetadata ?? [];
            IReadOnlyList<string> dontClickMetadata = settings.StrongboxDontClickMetadata ?? [];
            string renderName = label.ItemOnGround.RenderName ?? string.Empty;
            bool isUniqueStrongbox = IsUniqueStrongbox(label);

            if (clickMetadata.Count == 0)
                return false;

            if (isUniqueStrongbox)
            {
                if (ContainsStrongboxUniqueIdentifier(dontClickMetadata))
                    return false;

                return ContainsStrongboxUniqueIdentifier(clickMetadata);
            }

            bool dontClickMatch = ContainsAnyMetadataIdentifier(path, renderName, dontClickMetadata);

            if (dontClickMatch)
                return false;

            return ContainsAnyMetadataIdentifier(path, renderName, clickMetadata);
        }

        private static bool ContainsStrongboxUniqueIdentifier(IReadOnlyList<string> metadataIdentifiers)
        {
            if (metadataIdentifiers == null || metadataIdentifiers.Count == 0)
                return false;

            for (int i = 0; i < metadataIdentifiers.Count; i++)
            {
                if (string.Equals(metadataIdentifiers[i], StrongboxUniqueIdentifier, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsUniqueStrongbox(LabelOnGround? label)
        {
            return label?.ItemOnGround?.Rarity == MonsterRarity.Unique;
        }

        private static bool IsBasicChestName(string name)
        {
            name ??= string.Empty;
            return name.Equals("chest", StringComparison.OrdinalIgnoreCase)
                || name.Equals("tribal chest", StringComparison.OrdinalIgnoreCase)
                || name.Equals("golden chest", StringComparison.OrdinalIgnoreCase)
                || name.Equals("bone chest", StringComparison.OrdinalIgnoreCase)
                || name.Contains("cocoon", StringComparison.OrdinalIgnoreCase)
                || name.Equals("weapon rack", StringComparison.OrdinalIgnoreCase)
                || name.Equals("armour rack", StringComparison.OrdinalIgnoreCase)
                || name.Equals("trunk", StringComparison.OrdinalIgnoreCase);
        }
    }
}
