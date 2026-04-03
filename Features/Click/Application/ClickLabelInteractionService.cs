using SharpDX;

namespace ClickIt.Features.Click.Application
{
    internal readonly record struct ClickLabelInteractionServiceDependencies(
        ClickItSettings Settings,
        GameController GameController,
        InputHandler InputHandler,
        ILabelInteractionPort LabelInteractionPort,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        Func<InteractionExecutionRequest, bool> ExecuteInteraction,
        Func<bool> GroundItemsVisible,
        Action<Func<string>> DebugLog);

    internal sealed class ClickLabelInteractionService(ClickLabelInteractionServiceDependencies dependencies)
    {
        private readonly ClickLabelInteractionServiceDependencies _dependencies = dependencies;

        internal bool ExecuteInteraction(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool useHoldClick,
            int holdDurationMs = 0,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false,
            string? outsideWindowLogMessage = null)
        {
            return _dependencies.ExecuteInteraction(new InteractionExecutionRequest(
                ClickPosition: clickPos,
                ExpectedElement: expectedElement,
                Controller: controller,
                UseHoldClick: useHoldClick,
                HoldDurationMs: holdDurationMs,
                ForceUiHoverVerification: forceUiHoverVerification,
                AllowWhenHotkeyInactive: allowWhenHotkeyInactive,
                AvoidCursorMove: avoidCursorMove,
                OutsideWindowLogMessage: outsideWindowLogMessage
                    ?? (useHoldClick
                        ? "[PerformLabelHoldClick] Skipping hold click - cursor outside PoE window"
                        : "[PerformLabelClick] Skipping label click - cursor outside PoE window")));
        }

        internal float? TryGetCursorDistanceSquaredToEntity(Entity? entity, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            if (entity == null || !entity.IsValid)
                return null;

            try
            {
                var worldScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
                return ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, worldScreenAbsolute, windowTopLeft);
            }
            catch
            {
                return null;
            }
        }

        internal bool TryCorruptEssence(LabelOnGround label, Vector2 windowTopLeft)
        {
            if (_dependencies.Settings.ClickEssences && _dependencies.LabelInteractionPort.ShouldCorruptEssence(label))
            {
                Vector2? corruptionPos = EssenceService.GetCorruptionClickPosition(label, windowTopLeft);
                if (corruptionPos.HasValue)
                {
                    string labelPath = label.ItemOnGround?.Path ?? string.Empty;
                    bool corruptionPointInWindow = _dependencies.IsInsideWindowInEitherSpace(corruptionPos.Value);
                    bool corruptionPointClickable = _dependencies.IsClickableInEitherSpace(corruptionPos.Value, labelPath);
                    if (!ClickLabelSelectionMath.ShouldAttemptSpecialEssenceCorruption(corruptionPointInWindow, corruptionPointClickable))
                    {
                        _dependencies.DebugLog(() => "[ProcessRegularClick] Essence corruption point not actionable yet; allowing regular click/pathing flow");
                        return false;
                    }

                    _dependencies.DebugLog(() => $"[ProcessRegularClick] Corruption click at {corruptionPos.Value}");
                    return _dependencies.ExecuteInteraction(new InteractionExecutionRequest(
                        ClickPosition: corruptionPos.Value,
                        ExpectedElement: null,
                        Controller: _dependencies.GameController,
                        UseHoldClick: false,
                        HoldDurationMs: 0,
                        ForceUiHoverVerification: false,
                        AllowWhenHotkeyInactive: false,
                        AvoidCursorMove: false,
                        OutsideWindowLogMessage: "[TryCorruptEssence] Skipping corruption click - cursor outside PoE window"));
                }
            }

            return false;
        }

        internal bool PerformLabelClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
            => ExecuteInteraction(clickPos, expectedElement, controller, false, 0, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

        internal bool PerformLabelHoldClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            int holdDurationMs,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
            => ExecuteInteraction(clickPos, expectedElement, controller, true, holdDurationMs, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

        internal bool PerformTrackedLabelClick(Vector2 clickPos, LabelOnGround? label, bool forceUiHoverVerification)
            => PerformLabelClick(clickPos, label?.Label, _dependencies.GameController, forceUiHoverVerification);

        internal bool PerformMechanicClick(Vector2 clickPos)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, false);

        internal bool PerformMechanicInteraction(Vector2 clickPos, bool useHoldClick)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, useHoldClick);

        internal bool PerformManualCursorInteraction(Vector2 clickPos, bool useHoldClick)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, useHoldClick, allowWhenHotkeyInactive: true, avoidCursorMove: true);

        internal bool PerformResolvedLabelInteraction(Vector2 clickPos, LabelOnGround label, string? mechanicId)
            => ExecuteInteraction(
                clickPos,
                label.Label,
                _dependencies.GameController,
                SettlersMechanicPolicy.RequiresHoldClick(mechanicId),
                forceUiHoverVerification: OffscreenPathingMath.ShouldForceUiHoverVerificationForLabel(label));

        internal bool TryResolveLabelClickPosition(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            out Vector2 clickPos,
            string? explicitPath = null)
        {
            string path = explicitPath ?? label.ItemOnGround?.Path ?? string.Empty;

            if (_dependencies.InputHandler.TryCalculateClickPosition(
                label,
                windowTopLeft,
                allLabels,
                point => _dependencies.IsClickableInEitherSpace(point, path),
                out clickPos))
            {
                return true;
            }

            if (!LabelClickPointResolutionPolicy.ShouldRetryWithoutClickableArea(mechanicId))
                return false;

            if (!LabelClickPointResolutionPolicy.ShouldAllowSettlersRelaxedFallback(
                label.ItemOnGround != null,
                IsItemWorldProjectionInWindow(label.ItemOnGround, windowTopLeft)))
            {
                return false;
            }

            return _dependencies.InputHandler.TryCalculateClickPosition(
                label,
                windowTopLeft,
                allLabels,
                isClickableArea: null,
                out clickPos);
        }

        internal (bool Success, Vector2 ClickPos) TryResolveLabelClickPositionResult(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels)
        {
            bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos);
            return (success, clickPos);
        }

        internal (bool Success, Vector2 ClickPos) TryResolveLabelClickPositionResult(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            string? explicitPath)
        {
            bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos, explicitPath);
            return (success, clickPos);
        }

        internal string BuildNoLabelDebugSummary(IReadOnlyList<LabelOnGround>? allLabels)
        {
            int labelCount = allLabels?.Count ?? 0;
            string sourceSummary = BuildLabelSourceDebugSummary(allLabels);
            if (labelCount <= 0)
                return $"{sourceSummary} | selection:r:0-0 t:0";

            var summary = _dependencies.LabelInteractionPort.GetSelectionDebugSummary(allLabels, 0, labelCount);
            return $"{sourceSummary} | selection:{summary.ToCompactString()}";
        }

        internal string BuildLabelRangeRejectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int start, int endExclusive, int examined)
        {
            int maxCount = Math.Max(0, endExclusive - start);
            var summary = _dependencies.LabelInteractionPort.GetSelectionDebugSummary(allLabels, start, maxCount);
            return $"range:{start}-{endExclusive} examined:{examined} | {summary.ToCompactString()}";
        }

        internal string BuildLabelSourceDebugSummary(IReadOnlyList<LabelOnGround>? cachedLabelSnapshot)
        {
            int cachedCount = cachedLabelSnapshot?.Count ?? 0;
            int visibleCount = 0;
            try
            {
                visibleCount = _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count ?? 0;
            }
            catch
            {
                visibleCount = 0;
            }

            bool groundVisible = _dependencies.GroundItemsVisible();
            return $"visible:{visibleCount} cached:{cachedCount} groundVisible:{groundVisible}";
        }

        private bool IsItemWorldProjectionInWindow(Entity? item, Vector2 windowTopLeft)
        {
            if (item == null)
                return false;

            var worldScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(item.PosNum);
            Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
            return _dependencies.IsInsideWindowInEitherSpace(worldScreenAbsolute);
        }
    }
}