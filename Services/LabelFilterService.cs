using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using ClickIt.Services.Label.Application;
using ClickIt.Services.Label.Selection;
using ClickIt.Services.Mechanics;
using ClickIt.Services.Click.Ranking;
using ClickIt.Services.Label.Classification;
using ClickIt.Services.Label.Diagnostics;
using ClickIt.Services.Label.Inventory;

namespace ClickIt.Services
{
    public class LabelFilterService(ClickItSettings settings, EssenceService essenceService, ErrorHandler errorHandler, ExileCore.GameController? gameController)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly EssenceService _essenceService = essenceService;
        private readonly ErrorHandler _errorHandler = errorHandler;
        private readonly ExileCore.GameController? _gameController = gameController;
        private readonly IWorldItemMetadataPolicy _worldItemMetadataPolicy = new WorldItemMetadataPolicy();
        private const string StoneOfPassageMetadataIdentifier = "Incursion/IncursionKey";
        private const int LabelDebugTrailCapacity = 24;

        private readonly IMechanicPrioritySnapshotProvider _mechanicPrioritySnapshotService = new MechanicPrioritySnapshotService();
        private readonly LabelSelectionDiagnostics _labelSelectionDiagnostics = new(LabelDebugTrailCapacity);
        private LabelDomainFacade? _labelDomain;

        private LabelDomainFacade LabelDomain
            => _labelDomain ??= new LabelDomainFacade(new LabelDomainFacadeDependencies(
                _settings,
                _errorHandler,
                _gameController,
                _worldItemMetadataPolicy,
                _mechanicPrioritySnapshotService,
                _labelSelectionDiagnostics,
                Keyboard.IsKeyDown,
                ShouldCaptureLabelDebug,
                StoneOfPassageMetadataIdentifier));

        private ILabelSelectionService LabelSelectionService => LabelDomain.SelectionService;

        private LabelDebugService LabelDebugService => LabelDomain.DebugService;

        private LabelMechanicResolutionService LabelMechanicResolutionService => LabelDomain.MechanicResolutionService;

        private LazyModeBlockerService LazyModeBlockerService => LabelDomain.LazyModeBlockerService;

        private InventoryDomainFacade InventoryDomain => LabelDomain.InventoryDomain;

        internal LazyModeBlockerService GetLazyModeBlockerService()
            => LazyModeBlockerService;

        public LabelDebugSnapshot GetLatestLabelDebug()
            => _labelSelectionDiagnostics.GetLatest();

        public IReadOnlyList<string> GetLatestLabelDebugTrail()
            => _labelSelectionDiagnostics.GetTrail();

        public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => LabelSelectionService.GetNextLabelToClick(allLabels, startIndex, maxCount);

        public readonly struct SelectionDebugSummary(
            int Start,
            int End,
            int Total,
            int NullLabel,
            int NullEntity,
            int OutOfDistance,
            int Untargetable,
            int NoMechanic,
            int WorldItem,
            int WorldItemMetadataRejected,
            int SettlersPathSeen,
            int SettlersMechanicMatched,
            int SettlersMechanicDisabled)
        {
            public string ToCompactString()
            {
                return $"r:{Start}-{End} t:{Total} nl:{NullLabel} ne:{NullEntity} d:{OutOfDistance} u:{Untargetable} nm:{NoMechanic} wi:{WorldItem}/{WorldItemMetadataRejected} sp:{SettlersPathSeen} sm:{SettlersMechanicMatched} sd:{SettlersMechanicDisabled}";
            }
        }

        public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => LabelDebugService.GetSelectionDebugSummary(allLabels, startIndex, maxCount);

        public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => LabelDebugService.LogSelectionDiagnostics(allLabels, startIndex, maxCount);

        public string? GetMechanicIdForLabel(LabelOnGround? label)
            => LabelSelectionService.GetMechanicIdForLabel(label);

        public string? LastLazyModeRestrictionReason => LazyModeBlockerService.LastRestrictionReason;

        public bool HasLazyModeRestrictedItemsOnScreen(IReadOnlyList<LabelOnGround>? allLabels)
            => LazyModeBlockerService.HasRestrictedItemsOnScreen(allLabels);

        internal InventoryDebugSnapshot GetLatestInventoryDebug()
            => InventoryDomain.GetLatestDebug();

        internal IReadOnlyList<string> GetLatestInventoryDebugTrail()
            => InventoryDomain.GetLatestDebugTrail();

        internal void ClearInventoryProbeCacheForShutdown()
            => InventoryDomain.ClearForShutdown();

        private bool ShouldCaptureLabelDebug()
        {
            return _settings.DebugMode.Value && _settings.DebugShowLabels.Value;
        }

        internal static bool ShouldBlockLazyModeForNearbyMonsters(
            int nearbyNormalCount,
            int normalThreshold,
            int nearbyMagicCount,
            int magicThreshold,
            int nearbyRareCount,
            int rareThreshold,
            int nearbyUniqueCount,
            int uniqueThreshold)
            => LazyModeBlockerService.ShouldBlockLazyModeForNearbyMonsters(
                nearbyNormalCount,
                normalThreshold,
                nearbyMagicCount,
                magicThreshold,
                nearbyRareCount,
                rareThreshold,
                nearbyUniqueCount,
                uniqueThreshold);

        internal static string BuildNearbyMonsterBlockReason(
            int nearbyNormalCount,
            int normalThreshold,
            int normalDistance,
            bool normalTriggered,
            int nearbyMagicCount,
            int magicThreshold,
            int magicDistance,
            bool magicTriggered,
            int nearbyRareCount,
            int rareThreshold,
            int rareDistance,
            bool rareTriggered,
            int nearbyUniqueCount,
            int uniqueThreshold,
            int uniqueDistance,
            bool uniqueTriggered)
            => LazyModeBlockerService.BuildNearbyMonsterBlockReason(
                nearbyNormalCount,
                normalThreshold,
                normalDistance,
                normalTriggered,
                nearbyMagicCount,
                magicThreshold,
                magicDistance,
                magicTriggered,
                nearbyRareCount,
                rareThreshold,
                rareDistance,
                rareTriggered,
                nearbyUniqueCount,
                uniqueThreshold,
                uniqueDistance,
                uniqueTriggered);

        public bool ShouldCorruptEssence(LabelOnGround label)
            => _essenceService.ShouldCorruptEssence(label.Label);

        public static Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
            => EssenceService.GetCorruptionClickPosition(label, windowTopLeft);
    }

}