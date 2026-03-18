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

        private static readonly (string MechanicId, Func<string, bool> MatchesPath)[] SettlersOreResolvers =
        [
            (MechanicIds.SettlersCrimsonIron, IsSettlersCrimsonIronPath),
            (MechanicIds.SettlersCopper, IsSettlersCopperPath),
            (MechanicIds.SettlersPetrifiedWood, IsSettlersPetrifiedWoodPath),
            (MechanicIds.SettlersBismuth, IsSettlersBismuthPath),
            (MechanicIds.SettlersVerisium, IsSettlersVerisiumPath)
        ];

        private static string? GetClickableMechanicId(LabelOnGround label, Entity item, ClickSettings settings, ExileCore.GameController? gameController)
        {
            EntityType type = item.Type;
            string path = type == EntityType.WorldItem
                ? GetWorldItemMetadataPath(item)
                : (item.Path ?? string.Empty);

            string? mechanicId = ResolvePrimaryMechanicId(settings, path, label);
            if (!string.IsNullOrWhiteSpace(mechanicId))
                return mechanicId;

            if (type == EntityType.WorldItem)
            {
                if (!ShouldAllowWorldItemByMetadata(settings, item, gameController, label))
                    return null;

                if (ShouldClickWorldItemCore(settings.ClickItems, type, item))
                    return MechanicIds.Items;
            }

            return ResolveFallbackMechanicId(settings, type, path, label, item);
        }

        private static string? ResolvePrimaryMechanicId(ClickSettings settings, string path, LabelOnGround label)
        {
            string? special = GetSpecialPathMechanicId(settings, path, label);
            if (!string.IsNullOrWhiteSpace(special))
                return special;

            string? altar = GetAltarMechanicId(settings, path);
            if (!string.IsNullOrWhiteSpace(altar))
                return altar;

            if (ShouldClickEssence(settings.ClickEssences, label))
                return MechanicIds.Essences;

            return GetRitualMechanicId(settings.ClickRitualInitiate, settings.ClickRitualCompleted, path, label);
        }

        private static string? ResolveFallbackMechanicId(ClickSettings settings, EntityType type, string path, LabelOnGround label, Entity item)
        {
            string? chest = GetChestMechanicId(settings.ClickBasicChests, settings.ClickLeagueChests, type, label);
            if (!string.IsNullOrWhiteSpace(chest))
                return chest;

            string? named = GetNamedInteractableMechanicId(settings.ClickDoors, settings.ClickLevers, item.RenderName, path);
            if (!string.IsNullOrWhiteSpace(named))
                return named;

            return GetAreaTransitionMechanicId(settings.ClickAreaTransitions, settings.ClickLabyrinthTrials, type, path);
        }

        private static string? GetNamedInteractableMechanicId(bool clickDoors, bool clickLevers, string? renderName, string? metadataPath)
        {
            string path = metadataPath?.Trim() ?? string.Empty;

            bool isDoor = path.Contains("MiscellaneousObjects/Lights", StringComparison.OrdinalIgnoreCase)
                || path.Contains("MiscellaneousObjects/Door", StringComparison.OrdinalIgnoreCase);
            bool isLever = path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);

            if (clickDoors && isDoor)
                return MechanicIds.Doors;
            if (clickLevers && isLever)
                return MechanicIds.Levers;

            return null;
        }

        private static string? GetAreaTransitionMechanicId(bool clickAreaTransitions, bool clickLabyrinthTrials, EntityType type, string path)
        {
            bool isAreaTransition = type == EntityType.AreaTransition || path.Contains("AreaTransition", StringComparison.OrdinalIgnoreCase);
            if (!isAreaTransition)
                return null;

            if (IsLabyrinthTrialTransitionPath(path))
                return clickLabyrinthTrials ? MechanicIds.LabyrinthTrials : null;

            return clickAreaTransitions ? MechanicIds.AreaTransitions : null;
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

            string? itemPath = item.Path;
            return string.IsNullOrEmpty(itemPath)
                || !itemPath.Contains("strongbox", StringComparison.OrdinalIgnoreCase);
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

            if (!string.IsNullOrEmpty(path) && path.Contains("strongbox", StringComparison.OrdinalIgnoreCase))
                return null;

            bool isBasic = IsBasicChestName(renderName);
            if (clickBasicChests && isBasic)
                return MechanicIds.BasicChests;
            if (clickLeagueChests && !isBasic)
                return MechanicIds.LeagueChests;

            return null;
        }

        private static string? GetSpecialPathMechanicId(ClickSettings settings, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (TryGetSettlersOreMechanicId(path, out string? settlersMechanicId) && !string.IsNullOrWhiteSpace(settlersMechanicId))
            {
                return IsSettlersMechanicEnabled(settings, settlersMechanicId)
                    ? settlersMechanicId
                    : null;
            }

            return ResolveSpecialNonSettlersMechanic(settings, path, label);
        }

        private static string? ResolveSpecialNonSettlersMechanic(ClickSettings settings, string path, LabelOnGround label)
        {
            if (settings.NearestHarvest && IsHarvestPath(path))
                return MechanicIds.Harvest;
            if (settings.ClickSulphite && path.Contains("DelveMineral", StringComparison.OrdinalIgnoreCase))
                return MechanicIds.DelveSulphiteVeins;
            if ((settings.StrongboxClickMetadata?.Count ?? 0) > 0 && ShouldClickStrongbox(settings, path, label))
                return MechanicIds.Strongboxes;
            if (settings.ClickSanctum && path.Contains("Sanctum", StringComparison.OrdinalIgnoreCase))
                return MechanicIds.Sanctum;
            if (settings.ClickBetrayal && path.Contains("BetrayalMakeChoice", StringComparison.OrdinalIgnoreCase))
                return MechanicIds.Betrayal;
            if (settings.ClickBlight && path.Contains("BlightPump", StringComparison.OrdinalIgnoreCase))
                return MechanicIds.Blight;
            if (settings.ClickAlvaTempleDoors && path.Contains(Constants.ClosedDoorPast, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.AlvaTempleDoors;
            if (settings.ClickLegionPillars && path.Contains(Constants.LegionInitiator, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.LegionPillars;
            if (settings.ClickAzurite && path.Contains("AzuriteEncounterController", StringComparison.OrdinalIgnoreCase))
                return MechanicIds.DelveAzuriteVeins;
            if (settings.ClickInitialUltimatum && IsUltimatumPath(path))
                return MechanicIds.UltimatumInitialOverlay;
            if (settings.ClickDelveSpawners && path.Contains("Delve/Objects/Encounter", StringComparison.OrdinalIgnoreCase))
                return MechanicIds.DelveEncounterInitiators;
            if (settings.ClickCrafting && path.Contains("CraftingUnlocks", StringComparison.OrdinalIgnoreCase))
                return MechanicIds.CraftingRecipes;
            if (settings.ClickBreach && path.Contains(Constants.Brequel, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.BreachNodes;

            return null;
        }

        internal static bool TryGetSettlersOreMechanicId(string? path, out string? mechanicId)
        {
            mechanicId = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            for (int i = 0; i < SettlersOreResolvers.Length; i++)
            {
                (string resolvedMechanicId, Func<string, bool> matchesPath) = SettlersOreResolvers[i];
                if (!matchesPath(path))
                    continue;

                mechanicId = resolvedMechanicId;
                return true;
            }

            return false;
        }

        private static bool IsSettlersMechanicEnabled(ClickSettings settings, string mechanicId)
        {
            return mechanicId switch
            {
                var id when string.Equals(id, MechanicIds.SettlersCrimsonIron, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersCrimsonIron,
                var id when string.Equals(id, MechanicIds.SettlersCopper, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersCopper,
                var id when string.Equals(id, MechanicIds.SettlersPetrifiedWood, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersPetrifiedWood,
                var id when string.Equals(id, MechanicIds.SettlersBismuth, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersBismuth,
                var id when string.Equals(id, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersVerisium,
                _ => false
            };
        }

        private static bool IsHarvestPath(string path)
            => path.Contains("Harvest/Irrigator", StringComparison.OrdinalIgnoreCase)
               || path.Contains("Harvest/Extractor", StringComparison.OrdinalIgnoreCase);

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
            => MatchesSettlersOrePathMarker(path, MechanicIds.SettlersCrimsonIronMarker);

        private static bool IsSettlersCopperPath(string path)
            => MatchesSettlersOrePathMarker(path, MechanicIds.SettlersCopperMarker);

        private static bool IsSettlersPetrifiedWoodPath(string path)
            => MatchesSettlersOrePathMarker(path, MechanicIds.SettlersPetrifiedWoodMarker);

        private static bool IsSettlersBismuthPath(string path)
            => MatchesSettlersOrePathMarker(path, MechanicIds.SettlersBismuthMarker);

        private static bool IsSettlersVerisiumPath(string path)
            => MatchesSettlersOrePathMarker(path, MechanicIds.SettlersVerisiumMarker)
               && !path.Contains(MechanicIds.VerisiumBossSubAreaTransitionPathMarker, StringComparison.OrdinalIgnoreCase);

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
                return MechanicIds.AltarsSearingExarch;
            }

            if ((settings.HighlightEater || settings.ClickEater)
                && path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
            {
                return MechanicIds.AltarsEaterOfWorlds;
            }

            return null;
        }

        private static bool IsUltimatumPath(string path)
            => Constants.IsUltimatumInteractablePath(path);

        private static bool ShouldClickAltar(bool highlightEater, bool highlightExarch, bool clickEater, bool clickExarch, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (!(highlightEater || highlightExarch || clickEater || clickExarch))
                return false;

            return path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase)
                || path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldClickEssence(bool clickEssences, LabelOnGround label)
        {
            if (!clickEssences)
                return false;

            return LabelUtils.GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

        private static string? GetRitualMechanicId(bool clickRitualInitiate, bool clickRitualCompleted, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("Leagues/Ritual", StringComparison.OrdinalIgnoreCase))
                return null;

            bool hasFavoursText = LabelUtils.GetElementByString(label.Label, "Interact to view Favours") != null;
            if (clickRitualInitiate && !hasFavoursText)
                return MechanicIds.RitualInitiate;
            if (clickRitualCompleted && hasFavoursText)
                return MechanicIds.RitualCompleted;

            return null;
        }

        private static bool ShouldClickStrongbox(ClickSettings settings, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || label?.ItemOnGround == null)
                return false;

            Chest? chest = label.ItemOnGround.GetComponent<Chest>();
            if (chest?.IsLocked != false)
                return false;

            IReadOnlyList<string> clickMetadata = settings.StrongboxClickMetadata ?? [];
            IReadOnlyList<string> dontClickMetadata = settings.StrongboxDontClickMetadata ?? [];
            if (clickMetadata.Count == 0)
                return false;

            if (IsUniqueStrongbox(label))
            {
                if (ContainsStrongboxUniqueIdentifier(dontClickMetadata))
                    return false;

                return ContainsStrongboxUniqueIdentifier(clickMetadata);
            }

            string renderName = label.ItemOnGround.RenderName ?? string.Empty;
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
            => label?.ItemOnGround?.Rarity == MonsterRarity.Unique;

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