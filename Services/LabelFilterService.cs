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
using ClickIt.Services.Label.Classification.Policies;
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
        private ILabelSelectionService? _labelSelectionService;
        private LabelDebugService? _labelDebugService;
        private LabelMechanicResolutionService? _labelMechanicResolutionService;
        private LazyModeBlockerService? _lazyModeBlockerService;
        private MechanicClassifierDependencies? _classificationDependencies;
        private InventoryDomainFacade? _inventoryDomain;

        private ILabelSelectionService LabelSelectionService
            => _labelSelectionService ??= new LabelSelectionService(new LabelSelectionServiceDependencies(
                _gameController,
                CreateClickSettings,
                ShouldCaptureLabelDebug,
                debugEvent => PublishLabelDebugStage(debugEvent),
                TryBuildLabelCandidate,
                LabelMechanicResolutionService.GetMechanicIdForLabel));

        private LabelDebugService LabelDebugService
            => _labelDebugService ??= new LabelDebugService(
                _settings,
                _errorHandler,
                _gameController,
                CreateClickSettings,
                ShouldAllowWorldItemByMetadata,
                LabelMechanicResolutionService);

        private LabelMechanicResolutionService LabelMechanicResolutionService
            => _labelMechanicResolutionService ??= new LabelMechanicResolutionService(
                _gameController,
                CreateClickSettings,
                () => ClassificationDependencies);

        private LazyModeBlockerService LazyModeBlockerService
            => _lazyModeBlockerService ??= new LazyModeBlockerService(
                _settings,
                _gameController,
                reason => _errorHandler.LogMessage(true, true, reason, 5));

        private MechanicClassifierDependencies ClassificationDependencies
            => _classificationDependencies ??= new MechanicClassifierDependencies(
                _worldItemMetadataPolicy.GetWorldItemMetadataPath,
                ShouldAllowWorldItemByMetadata,
                ShouldClickStrongbox,
                static (clickEssences, label) => ShouldClickEssence(clickEssences, label),
                static (clickRitualInitiate, clickRitualCompleted, path, label) => GetRitualMechanicId(clickRitualInitiate, clickRitualCompleted, path, label),
                ShouldAllowClosedDoorPastMechanic);

        private InventoryDomainFacade InventoryDomain
            => _inventoryDomain ??= InventoryDomainComposition.Create(
                new InventoryDomainCompositionDependencies(_worldItemMetadataPolicy.GetWorldItemBaseName));

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

        private bool TryBuildLabelCandidate(
            LabelOnGround label,
            ClickSettings clickSettings,
            [NotNullWhen(true)] out Entity? item,
            [NotNullWhen(true)] out string? mechanicId,
            out LabelCandidateRejectReason rejectReason)
        {
            return LabelEligibilityEngine.TryBuildCandidate(
                label,
                clickSettings,
                LabelTargetabilityPolicy.IsEntityTargetableForClick,
                LabelMechanicResolutionService.ResolveMechanicId,
                out item,
                out mechanicId,
                out rejectReason);
        }

        private static bool IsEntityTargetableForClick(LabelOnGround label, Entity item)
            => LabelTargetabilityPolicy.IsEntityTargetableForClick(label, item);


        private static int GetMechanicPriorityIndex(IReadOnlyDictionary<string, int> priorityMap, string? mechanicId)
            => MechanicCandidateRanker.ResolvePriorityIndex(mechanicId, priorityMap);

        private bool ShouldCaptureLabelDebug()
        {
            return _settings.DebugMode.Value && _settings.DebugShowLabels.Value;
        }

        private void PublishLabelDebugStage(in LabelDebugEvent debugEvent)
        {
            if (!ShouldCaptureLabelDebug())
                return;

            _labelSelectionDiagnostics.PublishEvent(debugEvent);
        }

        internal ClickSettings CreateClickSettings(IReadOnlyList<LabelOnGround>? allLabels)
        {
            var factory = new ClickSettingsFactory(
                _settings,
                _mechanicPrioritySnapshotService,
                HasLazyModeRestrictedItemsOnScreen,
                Keyboard.IsKeyDown);

            return factory.Create(allLabels);
        }

        private bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label)
        {
            return _worldItemMetadataPolicy.ShouldAllowWorldItemByMetadata(settings, item, gameController, label, ShouldAllowWorldItemWhenInventoryFull);
        }

        private bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            => InventoryDomain.ShouldAllowWorldItemWhenInventoryFull(groundItem, gameController);

        private bool ShouldAllowClosedDoorPastMechanic(GameController? gameController)
            => InventoryDomain.ShouldAllowClosedDoorPastMechanic(gameController, StoneOfPassageMetadataIdentifier);

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
            bool dontClickMatch = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(path, renderName, dontClickMetadata);
            if (dontClickMatch)
                return false;

            return MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(path, renderName, clickMetadata);
        }

        private static bool ContainsStrongboxUniqueIdentifier(IReadOnlyList<string> metadataIdentifiers)
        {
            if (metadataIdentifiers == null || metadataIdentifiers.Count == 0)
                return false;

            for (int i = 0; i < metadataIdentifiers.Count; i++)
            {
                if (string.Equals(metadataIdentifiers[i], "special:strongbox-unique", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsUniqueStrongbox(LabelOnGround? label)
            => label?.ItemOnGround?.Rarity == MonsterRarity.Unique;

        public bool ShouldCorruptEssence(LabelOnGround label)
            => _essenceService.ShouldCorruptEssence(label.Label);

        public static Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
            => EssenceService.GetCorruptionClickPosition(label, windowTopLeft);
    }

}