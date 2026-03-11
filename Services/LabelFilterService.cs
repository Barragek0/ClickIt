using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ClickIt.Utils;
using ClickIt.Definitions;
using ExileCore;
#nullable enable
namespace ClickIt.Services
{
    public partial class LabelFilterService(ClickItSettings settings, EssenceService essenceService, ErrorHandler errorHandler, ExileCore.GameController? gameController)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly EssenceService _essenceService = essenceService;
        private readonly ErrorHandler _errorHandler = errorHandler;
        private readonly ExileCore.GameController? _gameController = gameController;

        public bool HasLazyModeRestrictedItemsOnScreen(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels)
        {
            return LazyModeRestrictedChecker(this, allLabels);
        }
        private bool HasLazyModeRestrictedItemsOnScreenImpl(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels)
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

                    // Check for restricted items: locked chest or settlers tree
                    var chestComponent = label.ItemOnGround.GetComponent<Chest>();
                    if (path.Contains(Constants.PetrifiedWood) || (chestComponent?.IsLocked == true && !chestComponent.IsStrongbox))
                    {
                        _errorHandler.LogMessage(true, true, $"Lazy mode: restricted item detected - Path: {path}", 5);
                        return true;
                    }
                }
            }
            return false;
        }
        public static List<LabelOnGround> FilterHarvestLabels(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels, Func<Vector2, bool> isInClickableArea)
        {
            List<LabelOnGround> result = [];
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
        public LabelOnGround? GetNextLabelToClick(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels)
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
                if (ShouldClickLabel(label, item, clickSettings, _gameController))
                {
                    return label;
                }
            }
            return null;
        }

        // Overload to search only a slice of the provided label list without allocating a new list.
        public LabelOnGround? GetNextLabelToClick(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
        {
            if (allLabels == null || allLabels.Count == 0) return null;
            var clickSettings = CreateClickSettings(allLabels);
            int end = Math.Min(allLabels.Count, startIndex + Math.Max(0, maxCount));
            for (int i = startIndex; i < end; i++)
            {
                LabelOnGround label = allLabels[i];
                Entity item = label.ItemOnGround;
                if (item == null || item.DistancePlayer > _settings.ClickDistance.Value)
                    continue;
                if (ShouldClickLabel(label, item, clickSettings, _gameController))
                {
                    return label;
                }
            }
            return null;
        }
        private ClickSettings CreateClickSettings(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels)
        {
            var s = _settings;

            // Check if lazy mode restrictions should be applied (only when lazy mode active, restricted items present, and hotkey NOT held)
            bool hasRestricted = LazyModeRestrictedChecker(this, allLabels);
            bool hotkeyHeld = KeyStateProvider(s.ClickLabelKey.Value);
            bool applyLazyModeRestrictions = s.LazyMode.Value && hasRestricted && !hotkeyHeld;

            return new ClickSettings
            {
                ClickDistance = s.ClickDistance.Value,
                ClickItems = s.ClickItems.Value,
                ItemTypeWhitelistMetadata = s.GetItemTypeWhitelistMetadataIdentifiers(),
                ItemTypeBlacklistMetadata = s.GetItemTypeBlacklistMetadataIdentifiers(),
                ClickBasicChests = s.ClickBasicChests.Value,
                ClickLeagueChests = !applyLazyModeRestrictions && s.ClickLeagueChests.Value,
                ClickDoors = s.ClickDoors.Value,
                ClickLevers = s.ClickLevers.Value,
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
                StrongboxClickMetadata = s.GetStrongboxClickMetadataIdentifiers(),
                StrongboxDontClickMetadata = s.GetStrongboxDontClickMetadataIdentifiers(),
                ClickSanctum = s.ClickSanctum.Value,
                ClickBetrayal = s.ClickBetrayal.Value,
                ClickBlight = s.ClickBlight.Value,
                ClickAlvaTempleDoors = s.ClickAlvaTempleDoors.Value,
                ClickLegionPillars = s.ClickLegionPillars.Value,
                ClickRitualInitiate = s.ClickRitualInitiate.Value,
                ClickRitualCompleted = s.ClickRitualCompleted.Value,
                ClickUltimatum = s.ClickUltimatum.Value,
            };
        }
        private struct ClickSettings
        {
            public int ClickDistance { get; set; }
            public bool ClickItems { get; set; }
            public IReadOnlyList<string> ItemTypeWhitelistMetadata { get; set; }
            public IReadOnlyList<string> ItemTypeBlacklistMetadata { get; set; }
            public bool ClickBasicChests { get; set; }
            public bool ClickLeagueChests { get; set; }
            public bool ClickDoors { get; set; }
            public bool ClickLevers { get; set; }
            public bool ClickAreaTransitions { get; set; }
            public bool NearestHarvest { get; set; }
            public bool ClickSulphite { get; set; }
            public bool ClickBlight { get; set; }
            public bool ClickAlvaTempleDoors { get; set; }
            public bool ClickLegionPillars { get; set; }
            public bool ClickRitualInitiate { get; set; }
            public bool ClickRitualCompleted { get; set; }
            public bool ClickUltimatum { get; set; }
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
            public IReadOnlyList<string> StrongboxClickMetadata { get; set; }
            public IReadOnlyList<string> StrongboxDontClickMetadata { get; set; }
            public bool ClickSanctum { get; set; }
            public bool ClickBetrayal { get; set; }
        }


        public bool ShouldCorruptEssence(LabelOnGround label)
        {
            return _essenceService.ShouldCorruptEssence(label.Label);
        }

        public static Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            return EssenceService.GetCorruptionClickPosition(label, windowTopLeft);
        }

        // Note: overlap heuristics are preserved for tests via the seam helper
        // IsLabelObscuredByCloserLabelForTests in LabelFilterService.Seams.cs.  The
        // runtime click resolution now uses UIHover verification in ClickService
        // so we avoid filtering essence labels here.

        private static bool DoRectanglesOverlap(RectangleF a, RectangleF b)
        {
            return GeometryHelpers.RectanglesOverlapExclusive(a, b);
        }

    }
}

