using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using System;
using System.Collections.Generic;
#nullable enable
namespace ClickIt.Services
{
    public class LabelFilterService
    {
        private readonly ClickItSettings _settings;
        private readonly EssenceService _essenceService;
        private const string CleansingFireAltar = "CleansingFireAltar";
        private const string TangleAltar = "TangleAltar";
        private const string Brequel = "Brequel";
        private const string CrimsonIron = "CrimsonIron";
        private const string CopperAltar = "copper_altar";
        private const string PetrifiedWood = "PetrifiedWood";
        private const string Bismuth = "Bismuth";
        private const string Verisium = "Verisium";
        private const string ClosedDoorPast = "ClosedDoorPast";
        private const string LegionInitiator = "LegionInitiator";

        public LabelFilterService(ClickItSettings settings, EssenceService essenceService)
        {
            _settings = settings;
            _essenceService = essenceService;
        }
        public bool HasVerisiumOnScreen(List<LabelOnGround> allLabels)
        {
            if (!_settings.ClickSettlersOre.Value || allLabels == null)
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
        public static List<LabelOnGround> FilterHarvestLabels(List<LabelOnGround> allLabels, System.Func<Vector2, bool> isInClickableArea)
        {
            List<LabelOnGround> result = new();
            if (allLabels == null)
                return result;
            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                if (label.ItemOnGround?.Path == null || label.Label?.GetClientRect() is not RectangleF rect || label.Label?.IsValid != true || !isInClickableArea(rect.Center))
                    continue;
                string path = label.ItemOnGround.Path;
                if (path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor"))
                    result.Add(label);
            }
            if (result.Count > 1)
                result.Sort((a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));
            return result;
        }
        public LabelOnGround? GetNextLabelToClick(List<LabelOnGround> allLabels)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;
            var clickSettings = CreateClickSettings();

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                Entity item = label.ItemOnGround;
                if (item == null || item.DistancePlayer > clickSettings.ClickDistance)
                    continue;
                if (ShouldClickLabel(label, item, clickSettings))
                    return label;
            }
            return null;
        }
        private ClickSettings CreateClickSettings()
        {
            var s = _settings;
            return new ClickSettings
            {
                ClickDistance = s.ClickDistance.Value,
                ClickItems = s.ClickItems.Value,
                IgnoreUniques = s.IgnoreUniques.Value,
                ClickBasicChests = s.ClickBasicChests.Value,
                ClickLeagueChests = s.ClickLeagueChests.Value,
                ClickAreaTransitions = s.ClickAreaTransitions.Value,
                NearestHarvest = s.NearestHarvest.Value,
                ClickSulphite = s.ClickSulphiteVeins.Value,
                ClickAzurite = s.ClickAzuriteVeins.Value,
                HighlightEater = s.HighlightEaterAltars.Value,
                HighlightExarch = s.HighlightExarchAltars.Value,
                ClickEater = s.ClickEaterAltars.Value,
                ClickExarch = s.ClickExarchAltars.Value,
                ClickEssences = s.ClickEssences.Value,
                ClickCrafting = s.ClickCraftingRecipes.Value,
                ClickBreach = s.ClickBreachNodes.Value,
                ClickSettlersOre = s.ClickSettlersOre.Value,
                ClickAlvaTempleDoors = s.ClickAlvaTempleDoors.Value,
                ClickLegionPillars = s.ClickLegionPillars.Value,
            };
        }
        private static bool ShouldClickLabel(LabelOnGround label, Entity item, ClickSettings settings)
        {
            string path = item.Path;
            EntityType type = item.Type;
            if (ShouldClickWorldItem(settings.ClickItems, settings.IgnoreUniques, type, item))
                return true;
            if (ShouldClickChest(settings.ClickBasicChests, settings.ClickLeagueChests, type, label))
                return true;
            if (settings.ClickAreaTransitions && type == EntityType.AreaTransition)
                return true;
            // Note: Shrines are not ground items - they are detected through entity list, not LabelOnGround
            if (ShouldClickSpecialPath(settings, path))
                return true;
            if (ShouldClickAltar(settings.HighlightEater, settings.HighlightExarch, settings.ClickEater, settings.ClickExarch, path))
                return true;
            if (ShouldClickEssence(settings.ClickEssences, label))
                return true;
            return false;
        }
        private struct ClickSettings
        {
            public int ClickDistance { get; set; }
            public bool ClickItems { get; set; }
            public bool IgnoreUniques { get; set; }
            public bool ClickBasicChests { get; set; }
            public bool ClickLeagueChests { get; set; }
            public bool ClickAreaTransitions { get; set; }
            public bool NearestHarvest { get; set; }
            public bool ClickSulphite { get; set; }
            public bool ClickAlvaTempleDoors { get; set; }
            public bool ClickLegionPillars { get; set; }
            public bool ClickAzurite { get; set; }
            public bool HighlightEater { get; set; }
            public bool HighlightExarch { get; set; }
            public bool ClickEater { get; set; }
            public bool ClickExarch { get; set; }
            public bool ClickEssences { get; set; }
            public bool ClickCrafting { get; set; }
            public bool ClickBreach { get; set; }
            public bool ClickSettlersOre { get; set; }
        }
        private static bool ShouldClickWorldItem(bool clickItems, bool ignoreUniques, EntityType type, Entity item)
        {
            if (!clickItems || type != EntityType.WorldItem)
                return false;
            if (!ignoreUniques)
                return true;
            WorldItem? worldItemComp = item.GetComponent<WorldItem>();
            Entity? itemEntity = worldItemComp?.ItemEntity;
            Mods? mods = itemEntity?.GetComponent<Mods>();
            if (mods?.ItemRarity == ItemRarity.Unique && !(itemEntity?.Path?.StartsWith("Metadata/Items/Metamorphosis/") ?? false))
                return false;
            return true;
        }
        private static bool ShouldClickChest(bool clickBasicChests, bool clickLeagueChests, EntityType type, LabelOnGround label)
        {
            if (type != EntityType.Chest)
                return false;
            bool isBasicChest = IsBasicChest(label);
            return (clickBasicChests && isBasicChest) || (clickLeagueChests && !isBasicChest);
        }
        private static bool ShouldClickSpecialPath(ClickSettings settings, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            return (settings.NearestHarvest && (path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor"))) ||
                   (settings.ClickSulphite && path.Contains("DelveMineral")) ||
                   (settings.ClickAlvaTempleDoors && path.Contains(ClosedDoorPast)) ||
                   (settings.ClickLegionPillars && path.Contains(LegionInitiator)) ||
                   (settings.ClickAzurite && path.Contains("AzuriteEncounterController")) ||
                   (settings.ClickCrafting && path.Contains("CraftingUnlocks")) ||
                   (settings.ClickBreach && path.Contains(Brequel)) ||
                   (settings.ClickSettlersOre && (path.Contains(CrimsonIron) || path.Contains(CopperAltar) || path.Contains(PetrifiedWood) || path.Contains(Bismuth) || path.Contains(Verisium)));
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

        public bool ShouldCorruptEssence(LabelOnGround label)
        {
            return _essenceService.ShouldCorruptEssence(label.Label);
        }

        public Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            return _essenceService.GetCorruptionClickPosition(label, windowTopLeft);
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
