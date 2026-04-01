using System.Collections;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ClickIt.Definitions;
using ClickIt.Utils;
using ClickIt.Services.Observability;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory.Components;
using ClickIt.Services.Click.Label;
using ClickIt.Services.Click.Runtime;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        [ThreadStatic]
        private static HashSet<long>? _threadGroundLabelEntityAddresses;

        internal bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? allLabels)
        {
            // Stable ClickService facade entry point; implementation lives in the label-selection coordinator.
            return LabelSelection.TryClickManualUiHoverLabel(allLabels);
        }

        internal IEnumerator ProcessRegularClick()
        {
            // Stable ClickService facade entry point; orchestration lives in the regular-click coordinator.
            return RegularClick.Run();
        }

        private float? TryGetCursorDistanceSquaredToEntity(Entity? entity, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            if (entity == null || !entity.IsValid)
                return null;

            try
            {
                var worldScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
                return ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, worldScreenAbsolute, windowTopLeft);
            }
            catch
            {
                return null;
            }
        }

        private bool TryCorruptEssence(LabelOnGround label, Vector2 windowTopLeft)
        {
            if (settings.ClickEssences && labelFilterService.ShouldCorruptEssence(label))
            {
                Vector2? corruptionPos = LabelFilterService.GetCorruptionClickPosition(label, windowTopLeft);
                if (corruptionPos.HasValue)
                {
                    if (!EnsureCursorInsideGameWindowForClick("[TryCorruptEssence] Skipping corruption click - cursor outside PoE window"))
                        return false;

                    DebugLog(() => $"[ProcessRegularClick] Corruption click at {corruptionPos.Value}");
                    PerformLockedClick(corruptionPos.Value, null, gameController);
                    performanceMonitor.RecordClickInterval();
                    return true;
                }
            }

            return false;
        }

        private bool PerformLabelClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelClick] Skipping label click - cursor outside PoE window"))
                return false;

            PerformLockedClick(clickPos, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool PerformLabelHoldClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            int holdDurationMs,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelHoldClick] Skipping hold click - cursor outside PoE window"))
                return false;

            PerformLockedHoldClick(clickPos, holdDurationMs, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool TryResolveLabelClickPosition(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            out Vector2 clickPos,
            string? explicitPath = null)
        {
            string path = explicitPath ?? label.ItemOnGround?.Path ?? string.Empty;

            if (inputHandler.TryCalculateClickPosition(
                label,
                windowTopLeft,
                allLabels,
                point => IsClickableInEitherSpace(point, path),
                out clickPos))
            {
                return true;
            }

            // Settlers labels can remain clickable while the backing world entity projection is off-screen.
            // In that case, relax area validation and let UIHover verification guard the final click.
            if (!LabelClickPointResolutionPolicy.ShouldRetryWithoutClickableArea(mechanicId))
                return false;

            if (!LabelClickPointResolutionPolicy.ShouldAllowSettlersRelaxedFallback(label.ItemOnGround != null, IsItemWorldProjectionInWindow(label.ItemOnGround, windowTopLeft)))
                return false;

            return inputHandler.TryCalculateClickPosition(
                label,
                windowTopLeft,
                allLabels,
                isClickableArea: null,
                out clickPos);
        }


        private bool IsItemWorldProjectionInWindow(Entity? item, Vector2 windowTopLeft)
        {
            if (item == null)
                return false;

            var worldScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(item.PosNum);
            Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
            return IsInsideWindowInEitherSpace(worldScreenAbsolute);
        }

        private string BuildNoLabelDebugSummary(IReadOnlyList<LabelOnGround>? allLabels)
        {
            int labelCount = allLabels?.Count ?? 0;
            string sourceSummary = BuildLabelSourceDebugSummary(allLabels);
            if (labelCount <= 0)
                return $"{sourceSummary} | selection:r:0-0 t:0";

            var summary = labelFilterService.GetSelectionDebugSummary(allLabels, 0, labelCount);
            return $"{sourceSummary} | selection:{summary.ToCompactString()}";
        }

        private string BuildLabelRangeRejectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int start, int endExclusive, int examined)
        {
            int maxCount = Math.Max(0, endExclusive - start);
            var summary = labelFilterService.GetSelectionDebugSummary(allLabels, start, maxCount);
            return $"range:{start}-{endExclusive} examined:{examined} | {summary.ToCompactString()}";
        }

        private string BuildLabelSourceDebugSummary(IReadOnlyList<LabelOnGround>? cachedLabelSnapshot)
        {
            int cachedCount = cachedLabelSnapshot?.Count ?? 0;
            int visibleCount = 0;
            try
            {
                visibleCount = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count ?? 0;
            }
            catch
            {
                visibleCount = 0;
            }

            bool groundVisible = groundItemsVisible();
            return $"visible:{visibleCount} cached:{cachedCount} groundVisible:{groundVisible}";
        }

        private void PublishClickFlowDebugStage(string stage, string notes, string? mechanicId = null)
        {
            if (!ShouldCaptureClickDebug())
                return;

            SetLatestClickDebug(new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
                MechanicId: mechanicId ?? string.Empty,
                EntityPath: string.Empty,
                Distance: 0f,
                WorldScreenRaw: default,
                WorldScreenAbsolute: default,
                ResolvedClickPoint: default,
                Resolved: false,
                CenterInWindow: false,
                CenterClickable: false,
                ResolvedInWindow: false,
                ResolvedClickable: false,
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        private void PublishLabelClickDebug(
            string stage,
            string? mechanicId,
            LabelOnGround label,
            Vector2 resolvedClickPos,
            bool resolved,
            string notes)
        {
            if (!ShouldCaptureClickDebug())
                return;

            Entity? entity = label?.ItemOnGround;
            if (entity == null)
                return;

            string entityPath = entity.Path ?? string.Empty;
            var worldScreenRawVec = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 worldScreenRaw = new(worldScreenRawVec.X, worldScreenRawVec.Y);

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 worldScreenAbsolute = worldScreenRaw + windowTopLeft;

            bool centerInWindow = IsInsideWindowInEitherSpace(worldScreenAbsolute);
            bool centerClickable = IsClickableInEitherSpace(worldScreenAbsolute, entityPath);
            bool resolvedInWindow = IsInsideWindowInEitherSpace(resolvedClickPos);
            bool resolvedClickable = IsClickableInEitherSpace(resolvedClickPos, entityPath);

            SetLatestClickDebug(new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
                MechanicId: mechanicId ?? string.Empty,
                EntityPath: entityPath,
                Distance: entity.DistancePlayer,
                WorldScreenRaw: worldScreenRaw,
                WorldScreenAbsolute: worldScreenAbsolute,
                ResolvedClickPoint: resolvedClickPos,
                Resolved: resolved,
                CenterInWindow: centerInWindow,
                CenterClickable: centerClickable,
                ResolvedInWindow: resolvedInWindow,
                ResolvedClickable: resolvedClickable,
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        private bool IsInsideWindowInEitherSpace(Vector2 point)
        {
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            return ClickLabelSelectionMath.IsInsideWindowInEitherSpace(point, windowArea);
        }

        private bool ShouldSuppressPathfindingLabel(LabelOnGround label)
        {
            return ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(
                LabelSelection.ShouldSuppressLeverClick(label),
                UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(label));
        }

        private IReadOnlyList<LabelOnGround>? GetLabelsForOffscreenSelection()
            => GetVisibleOrCachedLabels();

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => GetVisibleOrCachedLabels();

        private IReadOnlyList<LabelOnGround>? GetVisibleOrCachedLabels()
        {
            try
            {
                var raw = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
                var visible = ClickLabelSelectionMath.ResolveVisibleLabelsWithoutForcedCopy(raw);
                if (visible != null)
                    return visible;
            }
            catch
            {
            }

            return cachedLabels?.Value;
        }
    }
}
