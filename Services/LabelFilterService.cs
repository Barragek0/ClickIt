using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using System.Collections.Generic;

#nullable enable

namespace ClickIt.Services
{
    /// <summary>
    /// Handles label processing and filtering logic
    /// </summary>
    public class LabelFilterService
    {
        private readonly ClickItSettings _settings;

        private const string CleansingFireAltar = "CleansingFireAltar";
        private const string TangleAltar = "TangleAltar";
        private const string Brequel = "Brequel";
        private const string CrimsonIron = "CrimsonIron";
        private const string CopperAltar = "copper_altar";
        private const string Verisium = "Verisium";

        public LabelFilterService(ClickItSettings settings)
        {
            _settings = settings;
        }

        public bool HasVerisiumOnScreen(List<LabelOnGround> allLabels)
        {
            if (!_settings.ClickVerisium.Value || allLabels == null)
                return false;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                Entity item = label.ItemOnGround;
                if (item != null && item.DistancePlayer <= _settings.ClickDistance.Value)
                {
                    string path = item.Path;
                    if (!string.IsNullOrEmpty(path) && path.Contains(Verisium))
                        return true;
                }
            }
            return false;
        }

        public List<LabelOnGround> FilterHarvestLabels(List<LabelOnGround> allLabels, System.Func<Vector2, bool> isInClickableArea)
        {
            List<LabelOnGround> result = new();

            if (allLabels == null)
                return result;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                if (label.ItemOnGround?.Path == null || !isInClickableArea(label.Label.GetClientRect().Center))
                    continue;

                string path = label.ItemOnGround.Path;
                if (path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor"))
                    result.Add(label);
            }

            // Sort by distance without LINQ
            if (result.Count > 1)
                result.Sort((a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));

            return result;
        }

        public LabelOnGround? GetNextLabelToClick(List<LabelOnGround> allLabels)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            var s = _settings;
            int clickDistance = s.ClickDistance.Value;
            bool clickItems = s.ClickItems.Value;
            bool ignoreUniques = s.IgnoreUniques.Value;
            bool clickBasicChests = s.ClickBasicChests.Value;
            bool clickLeagueChests = s.ClickLeagueChests.Value;
            bool clickAreaTransitions = s.ClickAreaTransitions.Value;
            bool clickShrines = s.ClickShrines.Value;
            bool nearestHarvest = s.NearestHarvest.Value;
            bool clickSulphite = s.ClickSulphiteVeins.Value;
            bool clickAzurite = s.ClickAzuriteVeins.Value;
            bool highlightEater = s.HighlightEaterAltars.Value;
            bool highlightExarch = s.HighlightExarchAltars.Value;
            bool clickEater = s.ClickEaterAltars.Value;
            bool clickExarch = s.ClickExarchAltars.Value;
            bool clickEssences = s.ClickEssences.Value;
            bool clickCrafting = s.ClickCraftingRecipes.Value;
            bool clickBreach = s.ClickBreachNodes.Value;
            bool clickSettlersOre = s.ClickSettlersOre.Value;
            bool clickVerisium = s.ClickVerisium.Value;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                Entity item = label.ItemOnGround;
                if (item == null || item.DistancePlayer > clickDistance)
                    continue;

                string path = item.Path;
                EntityType type = item.Type;

                // Check different item types
                // Priority check for Verisium (requires special handling)
                if (clickVerisium && !string.IsNullOrEmpty(path) && path.Contains(Verisium))
                    return label;

                if (ShouldClickWorldItem(clickItems, ignoreUniques, type, item))
                    return label;

                if (ShouldClickChest(clickBasicChests, clickLeagueChests, type, label))
                    return label;

                if (clickAreaTransitions && type == EntityType.AreaTransition)
                    return label;

                if (clickShrines && type == EntityType.Shrine)
                    return label;

                if (ShouldClickSpecialPath(nearestHarvest, clickSulphite, clickAzurite, clickCrafting, clickBreach, clickSettlersOre, path))
                    return label;

                if (ShouldClickAltar(highlightEater, highlightExarch, clickEater, clickExarch, path))
                    return label;

                if (ShouldClickEssence(clickEssences, label))
                    return label;
            }

            return null;
        }

        private static bool ShouldClickWorldItem(bool clickItems, bool ignoreUniques, EntityType type, Entity item)
        {
            if (!clickItems || type != EntityType.WorldItem)
                return false;

            if (!ignoreUniques)
                return true;

            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                Mods? mods = itemEntity?.GetComponent<Mods>();
                if (mods?.ItemRarity == ItemRarity.Unique && !(itemEntity?.Path?.StartsWith("Metadata/Items/Metamorphosis/") ?? false))
                    return false; // skip uniques when ignoring
            }
            catch
            {
                // ignore exceptions and treat as not-unique
            }

            return true;
        }

        private static bool ShouldClickChest(bool clickBasicChests, bool clickLeagueChests, EntityType type, LabelOnGround label)
        {
            if (type != EntityType.Chest)
                return false;

            bool isBasicChest = IsBasicChest(label);
            return (clickBasicChests && isBasicChest) || (clickLeagueChests && !isBasicChest);
        }

        private static bool ShouldClickSpecialPath(bool nearestHarvest, bool clickSulphite, bool clickAzurite, bool clickCrafting, bool clickBreach, bool clickSettlersOre, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return (nearestHarvest && (path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor"))) ||
                   (clickSulphite && path.Contains("DelveMineral")) ||
                   (clickAzurite && path.Contains("AzuriteEncounterController")) ||
                   (clickCrafting && path.Contains("CraftingUnlocks")) ||
                   (clickBreach && path.Contains(Brequel)) ||
                   (clickSettlersOre && (path.Contains(CrimsonIron) || path.Contains(CopperAltar)));
        }

        private static bool ShouldClickAltar(bool highlightEater, bool highlightExarch, bool clickEater, bool clickExarch, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return (highlightEater || highlightExarch || clickEater || clickExarch) &&
                   (path.Contains(CleansingFireAltar) || path.Contains(TangleAltar));
        }

        private static bool ShouldClickEssence(bool clickEssences, LabelOnGround label)
        {
            if (!clickEssences)
                return false;

            return ElementService.GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

        private static bool IsBasicChest(LabelOnGround label)
        {
            return label.ItemOnGround.RenderName.ToLower() switch
            {
                "chest" or "tribal chest" or "golden chest" or "cocoon" or "weapon rack" or "armour rack" or "trunk" => true,
                _ => false,
            };
        }
    }
}