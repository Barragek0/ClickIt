using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ClickIt.Utils;
using System;
using System.Collections.Generic;
using ExileCore;
#nullable enable
namespace ClickIt.Services
{
    public class LabelFilterService
    {
        private readonly ClickItSettings _settings;
        private readonly EssenceService _essenceService;
        private readonly ErrorHandler _errorHandler;
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

        public LabelFilterService(ClickItSettings settings, EssenceService essenceService, ErrorHandler errorHandler)
        {
            _settings = settings;
            _essenceService = essenceService;
            _errorHandler = errorHandler;
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

        public bool HasLazyModeRestrictedItemsOnScreen(List<LabelOnGround> allLabels)
        {
            if (allLabels == null)
                return false;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                Entity item = label.ItemOnGround;
                if (item != null && item.DistancePlayer <= _settings.ClickDistance.Value)
                {
                    string path = item.Path;
                    if (string.IsNullOrEmpty(path))
                        continue;

                    // Check for restricted items: locked strongbox or chest, settlers tree
                    var chestComponent = label.ItemOnGround.GetComponent<Chest>();
                    if (path.Contains(PetrifiedWood) || chestComponent?.IsLocked == true || !label.IsVisible)
                    {
                        _errorHandler.LogMessage(true, true, $"Lazy mode: restricted item detected - Path: {path}", 5);
                        return true;
                    }
                }
            }
            return false;
        }
        public static List<LabelOnGround> FilterHarvestLabels(List<LabelOnGround> allLabels, Func<Vector2, bool> isInClickableArea)
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
            var clickSettings = CreateClickSettings(allLabels);

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
        private ClickSettings CreateClickSettings(List<LabelOnGround> allLabels)
        {
            var s = _settings;

            // Check if lazy mode restrictions should be applied (only when lazy mode active, restricted items present, and hotkey NOT held)
            bool hasRestricted = HasLazyModeRestrictedItemsOnScreen(allLabels);
            bool hotkeyHeld = Input.GetKeyState(s.ClickLabelKey.Value);
            bool applyLazyModeRestrictions = s.LazyMode.Value && hasRestricted && !hotkeyHeld;

            return new ClickSettings
            {
                ClickDistance = s.ClickDistance.Value,
                ClickItems = s.ClickItems.Value,
                IgnoreUniques = s.IgnoreUniques.Value,
                IgnoreHeistQuestContracts = s.IgnoreHeistQuestContracts.Value,
                ClickBasicChests = s.ClickBasicChests.Value,
                ClickLeagueChests = !applyLazyModeRestrictions && s.ClickLeagueChests.Value,
                ClickAreaTransitions = s.ClickAreaTransitions.Value,
                NearestHarvest = s.NearestHarvest.Value,
                ClickSulphite = s.ClickSulphiteVeins.Value,
                ClickAzurite = s.ClickAzuriteVeins.Value,
                ClickDelveSpawners = s.ClickDelveSpawners.Value,
                HighlightEater = s.HighlightEaterAltars.Value,
                HighlightExarch = s.HighlightExarchAltars.Value,
                ClickEater = s.ClickEaterAltars.Value,
                ClickExarch = s.ClickExarchAltars.Value,
                ClickEssences = s.ClickEssences.Value,
                ClickCrafting = s.ClickCraftingRecipes.Value,
                ClickBreach = s.ClickBreachNodes.Value,
                ClickSettlersOre = !applyLazyModeRestrictions && s.ClickSettlersOre.Value,
                RegularStrongbox = !applyLazyModeRestrictions && s.RegularStrongbox.Value,
                ArcanistStrongbox = !applyLazyModeRestrictions && s.ArcanistStrongbox.Value,
                ArmourerStrongbox = !applyLazyModeRestrictions && s.ArmourerStrongbox.Value,
                ArtisanStrongbox = !applyLazyModeRestrictions && s.ArtisanStrongbox.Value,
                BlacksmithStrongbox = !applyLazyModeRestrictions && s.BlacksmithStrongbox.Value,
                CartographerStrongbox = !applyLazyModeRestrictions && s.CartographerStrongbox.Value,
                DivinerStrongbox = !applyLazyModeRestrictions && s.DivinerStrongbox.Value,
                GemcutterStrongbox = !applyLazyModeRestrictions && s.GemcutterStrongbox.Value,
                JewellerStrongbox = !applyLazyModeRestrictions && s.JewellerStrongbox.Value,
                LargeStrongbox = !applyLazyModeRestrictions && s.LargeStrongbox.Value,
                OrnateStrongbox = !applyLazyModeRestrictions && s.OrnateStrongbox.Value,
                ClickSanctum = s.ClickSanctum.Value,
                ClickBetrayal = s.ClickBetrayal.Value,
                ClickBlight = s.ClickBlight.Value,
                ClickAlvaTempleDoors = s.ClickAlvaTempleDoors.Value,
                ClickLegionPillars = s.ClickLegionPillars.Value,
                ClickRitualInitiate = s.ClickRitualInitiate.Value,
                ClickRitualCompleted = s.ClickRitualCompleted.Value,
            };
        }
        private struct ClickSettings
        {
            public int ClickDistance { get; set; }
            public bool ClickItems { get; set; }
            public bool IgnoreUniques { get; set; }
            public bool IgnoreHeistQuestContracts { get; set; }
            public bool ClickBasicChests { get; set; }
            public bool ClickLeagueChests { get; set; }
            public bool ClickAreaTransitions { get; set; }
            public bool NearestHarvest { get; set; }
            public bool ClickSulphite { get; set; }
            public bool ClickBlight { get; set; }
            public bool ClickAlvaTempleDoors { get; set; }
            public bool ClickLegionPillars { get; set; }
            public bool ClickRitualInitiate { get; set; }
            public bool ClickRitualCompleted { get; set; }
            public bool ClickAzurite { get; set; }
            public bool ClickDelveSpawners { get; set; }
            public bool HighlightEater { get; set; }
            public bool HighlightExarch { get; set; }
            public bool ClickEater { get; set; }
            public bool ClickExarch { get; set; }
            public bool ClickEssences { get; set; }
            public bool ClickCrafting { get; set; }
            public bool ClickBreach { get; set; }
            public bool ClickSettlersOre { get; set; }
            public bool RegularStrongbox { get; set; }
            public bool ArcanistStrongbox { get; set; }
            public bool ArmourerStrongbox { get; set; }
            public bool ArtisanStrongbox { get; set; }
            public bool BlacksmithStrongbox { get; set; }
            public bool CartographerStrongbox { get; set; }
            public bool DivinerStrongbox { get; set; }
            public bool GemcutterStrongbox { get; set; }
            public bool JewellerStrongbox { get; set; }
            public bool LargeStrongbox { get; set; }
            public bool OrnateStrongbox { get; set; }
            public bool ClickSanctum { get; set; }
            public bool ClickBetrayal { get; set; }
        }

        private static bool ShouldClickLabel(LabelOnGround label, Entity item, ClickSettings settings)
        {
            string path = item.Path;
            EntityType type = item.Type;
            if (ShouldClickWorldItem(settings.ClickItems, settings.IgnoreUniques, settings.IgnoreHeistQuestContracts, type, item))
                return true;
            if (ShouldClickChest(settings.ClickBasicChests, settings.ClickLeagueChests, type, label))
                return true;
            if (settings.ClickAreaTransitions && (type == EntityType.AreaTransition || path.Contains("AreaTransition")))
                return true;
            // Note: Shrines are not ground items - they are detected through entity list, not LabelOnGround
            if (ShouldClickSpecialPath(settings, path))
                return true;
            if (ShouldClickAltar(settings.HighlightEater, settings.HighlightExarch, settings.ClickEater, settings.ClickExarch, path))
                return true;
            if (ShouldClickEssence(settings.ClickEssences, label))
                return true;
            if (ShouldClickRitual(settings.ClickRitualInitiate, settings.ClickRitualCompleted, path, label))
                return true;
            return false;
        }
        private static bool ShouldClickWorldItem(bool clickItems, bool ignoreUniques, bool ignoreHeistQuestContracts, EntityType type, Entity item)
        {
            if (!clickItems || type != EntityType.WorldItem)
                return false;
            // Prevent strongboxes from being clicked as items
            string? itemPath = item.Path;
            if (!string.IsNullOrEmpty(itemPath) && itemPath.ToLowerInvariant().Contains("strongbox"))
                return false;
            if (!ignoreUniques)
                return true;
            WorldItem? worldItemComp = item.GetComponent<WorldItem>();
            Entity? itemEntity = worldItemComp?.ItemEntity;
            Mods? mods = itemEntity?.GetComponent<Mods>();
            if (mods?.ItemRarity == ItemRarity.Unique && !(itemEntity?.Path?.StartsWith("Metadata/Items/Metamorphosis/") ?? false))
                return false;
            // Check for heist contracts
            if (ignoreHeistQuestContracts && itemEntity?.GetComponent<Base>()?.Name != null)
            {
                string itemName = itemEntity.GetComponent<Base>().Name;
                if (Constants.Constants.HeistQuestContractNames.Contains(itemName))
                    return false;
            }
            return true;
        }
        private static bool ShouldClickChest(bool clickBasicChests, bool clickLeagueChests, EntityType type, LabelOnGround label)
        {
            if (type != EntityType.Chest)
                return false;
            // Avoid treating strongboxes as generic chests; strongboxes have their own settings
            string? path = label.ItemOnGround?.Path;
            if (!string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains("strongbox"))
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
                   ShouldClickStrongbox(settings, path) ||
                   (settings.ClickSanctum && path.Contains("Sanctum")) ||
                   (settings.ClickBetrayal && path.Contains("BetrayalMakeChoice")) ||
                   (settings.ClickBlight && path.Contains("BlightPump")) ||
                   (settings.ClickAlvaTempleDoors && path.Contains(ClosedDoorPast)) ||
                   (settings.ClickLegionPillars && path.Contains(LegionInitiator)) ||
                   (settings.ClickAzurite && path.Contains("AzuriteEncounterController")) ||
                   (settings.ClickDelveSpawners && path.Contains("Delve/Objects/Encounter")) ||
                   (settings.ClickCrafting && path.Contains("CraftingUnlocks")) ||
                   (settings.ClickBreach && path.Contains(Brequel)) ||
                   (settings.ClickSettlersOre && (path.Contains(CrimsonIron) || path.Contains(CopperAltar) || path.Contains(PetrifiedWood) || path.Contains(Bismuth)));
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
            return LabelUtils.GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

        private static bool ShouldClickRitual(bool clickRitualInitiate, bool clickRitualCompleted, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("Leagues/Ritual"))
                return false;

            bool hasFavoursText = LabelUtils.GetElementByString(label.Label, "Interact to view Favours") != null;

            // Click initiate altars (those without "Interact to view Favours" text)
            if (clickRitualInitiate && !hasFavoursText)
                return true;

            // Click completed altars (those with "Interact to view Favours" text)
            if (clickRitualCompleted && hasFavoursText)
                return true;

            return false;
        }

        private static bool ShouldClickStrongbox(ClickSettings settings, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Require both global ClickStrongboxes and the specific strongbox setting
            return (settings.RegularStrongbox && path.Contains("StrongBoxes/Strongbox")) ||
                   (settings.ArcanistStrongbox && path.Contains("StrongBoxes/Arcanist")) ||
                   (settings.ArmourerStrongbox && path.Contains("StrongBoxes/Armory")) ||
                   (settings.ArtisanStrongbox && path.Contains("StrongBoxes/Artisan")) ||
                   (settings.BlacksmithStrongbox && path.Contains("StrongBoxes/Arsenal")) ||
                   (settings.CartographerStrongbox && path.Contains("StrongBoxes/CartographerEndMaps")) ||
                   (settings.DivinerStrongbox && path.Contains("StrongBoxes/StrongboxDivination")) ||
                   (settings.GemcutterStrongbox && path.Contains("StrongBoxes/Gemcutter")) ||
                   (settings.JewellerStrongbox && path.Contains("StrongBoxes/Jeweller")) ||
                   (settings.LargeStrongbox && path.Contains("StrongBoxes/Large")) ||
                   (settings.OrnateStrongbox && path.Contains("StrongBoxes/Ornate"));
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
                "chest" or "tribal chest" or "golden chest" or "cocoon" or "weapon rack" or "armour rack" or "trunk" or "rotted cocoon" => true,
                _ => false,
            };
        }
    }
}
