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
        private const string MechanicSulphiteVeins = "sulphite-veins";
        private const string MechanicStrongboxes = "strongboxes";
        private const string MechanicSanctum = "sanctum";
        private const string MechanicBetrayal = "betrayal";
        private const string MechanicBlight = "blight";
        private const string MechanicAlvaTempleDoors = "alva-temple-doors";
        private const string MechanicLegionPillars = "legion-pillars";
        private const string MechanicAzuriteVeins = "azurite-veins";
        private const string MechanicUltimatum = "ultimatum";
        private const string MechanicDelveSpawners = "delve-spawners";
        private const string MechanicCraftingRecipes = "crafting-recipes";
        private const string MechanicBreachNodes = "breach-nodes";
        private const string MechanicSettlersOre = "settlers-ore";
        private const string MechanicAltars = "altars";
        private const string MechanicEssences = "essences";
        private const string MechanicRitualInitiate = "ritual-initiate";
        private const string MechanicRitualCompleted = "ritual-completed";

        private static string? GetClickableMechanicId(LabelOnGround label, Entity item, ClickSettings settings, ExileCore.GameController? gameController)
        {
            string path = item.Path;
            EntityType type = item.Type;

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

            string? specialMechanicId = GetSpecialPathMechanicId(settings, path, label);
            if (!string.IsNullOrWhiteSpace(specialMechanicId))
                return specialMechanicId;

            if (ShouldClickAltar(settings.HighlightEater, settings.HighlightExarch, settings.ClickEater, settings.ClickExarch, path))
                return MechanicAltars;

            if (ShouldClickEssence(settings.ClickEssences, label))
                return MechanicEssences;

            string? ritualMechanicId = GetRitualMechanicId(settings.ClickRitualInitiate, settings.ClickRitualCompleted, path, label);
            if (!string.IsNullOrWhiteSpace(ritualMechanicId))
                return ritualMechanicId;

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

            bool strongboxesEnabled = settings.StrongboxClickMetadata?.Count > 0;

            var checks = new (bool On, string MechanicId, Func<string, bool> Matches)[]
            {
                (settings.NearestHarvest, MechanicHarvest, static p => IsHarvestPath(p)),
                (settings.ClickSulphite, MechanicSulphiteVeins, static p => p.Contains("DelveMineral")),
                (strongboxesEnabled, MechanicStrongboxes, p => ShouldClickStrongbox(settings, p, label)),
                (settings.ClickSanctum, MechanicSanctum, static p => p.Contains("Sanctum")),
                (settings.ClickBetrayal, MechanicBetrayal, static p => p.Contains("BetrayalMakeChoice")),
                (settings.ClickBlight, MechanicBlight, static p => p.Contains("BlightPump")),
                (settings.ClickAlvaTempleDoors, MechanicAlvaTempleDoors, static p => p.Contains(Constants.ClosedDoorPast)),
                (settings.ClickLegionPillars, MechanicLegionPillars, static p => p.Contains(Constants.LegionInitiator)),
                (settings.ClickAzurite, MechanicAzuriteVeins, static p => p.Contains("AzuriteEncounterController")),
                (settings.ClickInitialUltimatum, MechanicUltimatum, static p => IsUltimatumPath(p)),
                (settings.ClickDelveSpawners, MechanicDelveSpawners, static p => p.Contains("Delve/Objects/Encounter")),
                (settings.ClickCrafting, MechanicCraftingRecipes, static p => p.Contains("CraftingUnlocks")),
                (settings.ClickBreach, MechanicBreachNodes, static p => p.Contains(Constants.Brequel)),
                (settings.ClickSettlersOre, MechanicSettlersOre, static p => IsSettlersOrePath(p))
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

        private static bool IsHarvestPath(string path)
        {
            return path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor");
        }

        private static bool IsSettlersOrePath(string path)
        {
            return path.Contains(Constants.CrimsonIron)
                || path.Contains(Constants.CopperAltar)
                || path.Contains(Constants.PetrifiedWood)
                || path.Contains(Constants.Bismuth)
                || path.Contains(Constants.Hourglass);
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
