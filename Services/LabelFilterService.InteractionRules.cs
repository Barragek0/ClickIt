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
        private static bool ShouldClickLabel(LabelOnGround label, Entity item, ClickSettings settings, ExileCore.GameController? gameController)
        {
            string path = item.Path;
            EntityType type = item.Type;

            if (type == EntityType.WorldItem && !ShouldAllowWorldItemByMetadata(settings, item))
                return false;
            if (ShouldClickWorldItemCore(settings.ClickItems, type, item))
                return true;
            if (ShouldClickChest(settings.ClickBasicChests, settings.ClickLeagueChests, type, label))
                return true;
            if (ShouldClickNamedInteractable(settings.ClickDoors, settings.ClickLevers, item.RenderName, path))
                return true;
            if (settings.ClickAreaTransitions && (type == EntityType.AreaTransition || path.Contains("AreaTransition")))
                return true;

            // Note: Shrines are not ground items - they are detected through entity list, not LabelOnGround.
            if (ShouldClickSpecialPath(settings, path, label))
                return true;
            if (ShouldClickAltar(settings.HighlightEater, settings.HighlightExarch, settings.ClickEater, settings.ClickExarch, path))
                return true;
            if (ShouldClickEssence(settings.ClickEssences, label))
                return true;
            if (ShouldClickRitual(settings.ClickRitualInitiate, settings.ClickRitualCompleted, path, label))
                return true;

            return false;
        }

        private static bool ShouldClickNamedInteractable(bool clickDoors, bool clickLevers, string? renderName, string? metadataPath)
        {
            string path = string.IsNullOrWhiteSpace(metadataPath) ? string.Empty : metadataPath.Trim();

            bool isDoor = path.Contains("MiscellaneousObjects/Lights", StringComparison.OrdinalIgnoreCase)
                || path.Contains("MiscellaneousObjects/Door", StringComparison.OrdinalIgnoreCase);
            bool isLever = path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);

            return (clickDoors && isDoor) || (clickLevers && isLever);
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

#pragma warning disable IDE0051 // Used via reflection in tests
        private static bool ShouldClickWorldItem(bool clickItems, bool ignoreUniques, bool ignoreHeistQuestContracts, bool ignoreInscribedUltimatums, bool onlyPickupCurrencyItems, EntityType type, Entity item, ExileCore.GameController? gameController)
#pragma warning restore IDE0051
        {
            return ShouldClickWorldItemCore(clickItems, type, item);
        }

        private static bool ShouldClickChest(bool clickBasicChests, bool clickLeagueChests, EntityType type, LabelOnGround label)
        {
            string? path = label.ItemOnGround?.Path;
            string renderName = label.ItemOnGround?.RenderName ?? string.Empty;
            return ShouldClickChestInternal(clickBasicChests, clickLeagueChests, type, path, renderName);
        }

        private static bool ShouldClickChestInternal(bool clickBasicChests, bool clickLeagueChests, EntityType type, string? path, string renderName)
        {
            if (type != EntityType.Chest)
                return false;

            // Avoid treating strongboxes as generic chests; strongboxes have their own settings.
            if (!string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains("strongbox"))
                return false;

            bool isBasicChest = IsBasicChestName(renderName);
            return (clickBasicChests && isBasicChest) || (clickLeagueChests && !isBasicChest);
        }

        private static bool ShouldClickSpecialPath(ClickSettings settings, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            bool strongboxesEnabled = settings.StrongboxClickMetadata?.Count > 0;

            var checks = new (bool On, Func<string, bool> Matches)[]
            {
                (settings.NearestHarvest, static p => IsHarvestPath(p)),
                (settings.ClickSulphite, static p => p.Contains("DelveMineral")),
                (strongboxesEnabled, p => ShouldClickStrongbox(settings, p, label)),
                (settings.ClickSanctum, static p => p.Contains("Sanctum")),
                (settings.ClickBetrayal, static p => p.Contains("BetrayalMakeChoice")),
                (settings.ClickBlight, static p => p.Contains("BlightPump")),
                (settings.ClickAlvaTempleDoors, static p => p.Contains(Constants.ClosedDoorPast)),
                (settings.ClickLegionPillars, static p => p.Contains(Constants.LegionInitiator)),
                (settings.ClickAzurite, static p => p.Contains("AzuriteEncounterController")),
                (settings.ClickUltimatum, static p => IsUltimatumPath(p)),
                (settings.ClickDelveSpawners, static p => p.Contains("Delve/Objects/Encounter")),
                (settings.ClickCrafting, static p => p.Contains("CraftingUnlocks")),
                (settings.ClickBreach, static p => p.Contains(Constants.Brequel)),
                (settings.ClickSettlersOre, static p => IsSettlersOrePath(p))
            };

            foreach ((bool on, Func<string, bool> matches) in checks)
            {
                if (!on)
                    continue;
                if (matches(path))
                    return true;
            }

            return false;
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
                || path.Contains(Constants.Bismuth);
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

        private static bool ShouldClickRitual(bool clickRitualInitiate, bool clickRitualCompleted, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("Leagues/Ritual"))
                return false;

            bool hasFavoursText = LabelUtils.GetElementByString(label.Label, "Interact to view Favours") != null;
            if (clickRitualInitiate && !hasFavoursText)
                return true;
            if (clickRitualCompleted && hasFavoursText)
                return true;

            return false;
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

            IReadOnlyList<string> clickMetadata = settings.StrongboxClickMetadata ?? Array.Empty<string>();
            IReadOnlyList<string> dontClickMetadata = settings.StrongboxDontClickMetadata ?? Array.Empty<string>();
            string renderName = label.ItemOnGround.RenderName ?? string.Empty;

            if (clickMetadata.Count == 0)
                return false;
            if (ContainsAnyMetadataIdentifier(path, renderName, dontClickMetadata))
                return false;

            return ContainsAnyMetadataIdentifier(path, renderName, clickMetadata);
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