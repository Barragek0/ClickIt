using ClickIt.Definitions;
using ClickIt.Utils;
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

namespace ClickIt.Services
{
    public partial class LabelFilterService(ClickItSettings settings, EssenceService essenceService, ErrorHandler errorHandler, ExileCore.GameController? gameController)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly EssenceService _essenceService = essenceService;
        private readonly ErrorHandler _errorHandler = errorHandler;
        private readonly ExileCore.GameController? _gameController = gameController;
        private readonly IWorldItemMetadataPolicy _worldItemMetadataPolicy = new WorldItemMetadataPolicy();

        private readonly IMechanicPrioritySnapshotProvider _mechanicPrioritySnapshotService = new MechanicPrioritySnapshotService();
        private ILabelSelectionService? _labelSelectionService;
        private LabelDebugService? _labelDebugService;
        private LabelMechanicResolutionService? _labelMechanicResolutionService;
        private LazyModeBlockerService? _lazyModeBlockerService;

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

        internal LazyModeBlockerService GetLazyModeBlockerService()
            => LazyModeBlockerService;

        public static List<LabelOnGround> FilterHarvestLabels(IReadOnlyList<LabelOnGround>? allLabels, Func<Vector2, bool> isInClickableArea)
        {
            List<LabelOnGround> result = [];
            if (allLabels == null)
                return result;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                if (!TryGetClickableLabelRectCenter(label, out Vector2 center))
                    continue;
                if (!isInClickableArea(center))
                    continue;

                string path = label.ItemOnGround?.Path ?? string.Empty;
                if (path.Contains("Harvest/Irrigator", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("Harvest/Extractor", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(label);
                }
            }

            if (result.Count > 1)
                result.Sort(static (a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));

            return result;
        }

        private static bool TryGetClickableLabelRectCenter(LabelOnGround? label, out Vector2 center)
        {
            center = default;
            var element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            RectangleF rect = element.GetClientRect();
            center = rect.Center;
            return rect.Width > 0f && rect.Height > 0f;
        }

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

        internal static bool ShouldAllowHarvestRootElementVisibility(string? path, bool harvestRootElementVisible)
            => LabelTargetabilityPolicy.ShouldAllowHarvestRootElementVisibility(path, harvestRootElementVisible);

        internal static bool RequiresTargetabilityGate(string path)
            => LabelTargetabilityPolicy.RequiresTargetabilityGate(path);

        internal static bool ShouldApplyPetrifiedWoodEntityTargetabilityGate(string? path)
            => LabelTargetabilityPolicy.ShouldApplyPetrifiedWoodEntityTargetabilityGate(path);

        internal static bool ShouldAllowPetrifiedWoodTargetability(bool hasLabelEntityTargetable, bool labelEntityTargetable)
            => LabelTargetabilityPolicy.ShouldAllowPetrifiedWoodTargetability(hasLabelEntityTargetable, labelEntityTargetable);

        internal static void ResolveLabelEntityTargetableForClick(LabelOnGround label, out bool hasLabelEntityTargetable, out bool labelEntityTargetable)
            => LabelTargetabilityPolicy.ResolveLabelEntityTargetableForClick(label, out hasLabelEntityTargetable, out labelEntityTargetable);

        internal static void ResolveLabelEntityTargetableFromRaw(object? rawLabelEntity, out bool hasLabelEntityTargetable, out bool labelEntityTargetable)
            => LabelTargetabilityPolicy.ResolveLabelEntityTargetableFromRaw(rawLabelEntity, out hasLabelEntityTargetable, out labelEntityTargetable);

        internal static bool ShouldSkipUntargetableEntity(bool hasLabelEntityTargetable, bool labelEntityTargetable, bool itemIsTargetable, bool allowNullEntityFallback = false)
            => LabelTargetabilityPolicy.ShouldSkipUntargetableEntity(hasLabelEntityTargetable, labelEntityTargetable, itemIsTargetable, allowNullEntityFallback);


        private static int GetMechanicPriorityIndex(IReadOnlyDictionary<string, int> priorityMap, string? mechanicId)
            => MechanicCandidateRanker.ResolvePriorityIndex(mechanicId, priorityMap);

        public bool ShouldCorruptEssence(LabelOnGround label)
            => _essenceService.ShouldCorruptEssence(label.Label);

        public static Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
            => EssenceService.GetCorruptionClickPosition(label, windowTopLeft);
    }

}