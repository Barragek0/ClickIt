namespace ClickIt.Features.Click.Application
{
    internal readonly record struct ClickLabelInteractionServiceDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ILabelInteractionPort LabelInteractionPort,
        Func<LabelOnGround, Vector2, IReadOnlyList<LabelOnGround>?, Func<Vector2, bool>?, (bool Success, Vector2 ClickPos)> TryResolveClickPosition,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        Func<InteractionExecutionRequest, bool> ExecuteInteraction,
        Func<bool> GroundItemsVisible,
        Action<Func<string>> DebugLog,
        ElementTreeInspector? BlightChestDebug = null,
        Action<string>? LogError = null,
        HarvestEncounterUpgrade? HarvestUpgrade = null,
        Action<Func<string>>? CraftingLog = null);

    internal sealed class ClickLabelInteractionService(ClickLabelInteractionServiceDependencies dependencies)
    {
        private const long BlightChestDebugCaptureIntervalMs = 500;
        private readonly ClickLabelInteractionServiceDependencies _dependencies = dependencies;
        private long _lastBlightChestDebugCaptureTimestampMs;
        private long _lastActionRowDebugTimestampMs;

        internal void LogLabelAction(Func<string> message) => _dependencies.CraftingLog?.Invoke(message);

        internal bool ExecuteInteraction(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool useHoldClick,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false,
            string? outsideWindowLogMessage = null,
            IntervalKind interval = IntervalKind.Click,
            Func<Element?, bool>? validateSettledHover = null)
        {
            return _dependencies.ExecuteInteraction(new InteractionExecutionRequest(
                ClickPosition: clickPos,
                ExpectedElement: expectedElement,
                Controller: controller,
                UseHoldClick: useHoldClick,
                ForceUiHoverVerification: forceUiHoverVerification,
                AllowWhenHotkeyInactive: allowWhenHotkeyInactive,
                AvoidCursorMove: avoidCursorMove,
                OutsideWindowLogMessage: outsideWindowLogMessage
                    ?? (useHoldClick
                        ? "[PerformLabelHoldClick] Skipping hold click - cursor outside PoE window"
                        : "[PerformLabelClick] Skipping label click - cursor outside PoE window"),
                Interval: interval,
                ValidateSettledHover: validateSettledHover));
        }

        internal float? TryGetCursorDistanceSquaredToEntity(Entity? entity, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            if (!DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsValid, out bool isValid) || !isValid)
                return null;

            try
            {
                if (!TryProjectEntityAbsolutePosition(entity, windowTopLeft, out Vector2 worldScreenAbsolute))
                    return null;

                return ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, worldScreenAbsolute, windowTopLeft);
            }
            catch
            {
                return null;
            }
        }

        internal bool TryCorruptEssence(LabelOnGround label, string? mechanicId, Vector2 windowTopLeft)
        {
            return string.Equals(mechanicId, MechanicIds.Essences, StringComparison.OrdinalIgnoreCase)
                && _dependencies.Settings.ClickEssences.Value
                && _dependencies.LabelInteractionPort.ShouldCorruptEssence(label)
                && TryClickLabelAction(label, windowTopLeft, LabelActionButtons.VaalOrb,
                    alternateRowIndex: 2,
                    canStillClick: () => _dependencies.Settings.ClickEssences.Value
                        && _dependencies.LabelInteractionPort.ShouldCorruptEssence(label)) == LabelActionResult.Clicked;
        }

        internal enum LabelActionResult { Clicked, NoMatch, Unavailable }

        // A tooltip can be empty until the cursor enters its icon. Use the guarded
        // dispatcher to move and settle the cursor, and only authorize input on a match.
        internal LabelActionResult TryClickLabelAction(LabelOnGround label, Vector2 windowTopLeft, string tooltipName,
            int rowIndex = LabelActionButtons.DefaultRowIndex, int? alternateRowIndex = null,
            bool allowWhenHotkeyInactive = false, Func<bool>? canStillClick = null)
        {
            object? root = LabelActionButtons.ReadVisibleLabelRoot(label);
            string path = ResolveLabelItemPath(label, explicitPath: null);
            long searchDeadline = Environment.TickCount64 + 1_000;
            LabelActionResult result = TryRow(rowIndex);
            if (result != LabelActionResult.Clicked && alternateRowIndex is { } alternate)
            {
                LabelActionResult alternateResult = TryRow(alternate);
                if (alternateResult == LabelActionResult.Clicked || result == LabelActionResult.NoMatch)
                    result = alternateResult;
            }

            if (result != LabelActionResult.Clicked)
            {
                long now = Environment.TickCount64;
                if (_dependencies.Settings.DebugMode.Value && now - _lastActionRowDebugTimestampMs >= 500)
                {
                    _lastActionRowDebugTimestampMs = now;
                    LogLabelAction(() => $"[Label action] '{tooltipName}' search: {result}. {LabelActionButtons.DescribeRow(root, rowIndex)}");
                }
            }
            return result;

            LabelActionResult TryRow(int row)
            {
                int count = LabelActionButtons.CountButtons(root, row);
                bool unreadable = count == 0;
                for (int index = 0; index < count; index++)
                {
                    if (Environment.TickCount64 >= searchDeadline || canStillClick?.Invoke() == false)
                        return LabelActionResult.Unavailable;

                    if (LabelActionButtons.ReadVisibleButton(root, row, index) is not Element { Address: not 0 } button)
                        continue;

                    if (!TryGetActionButtonBounds(button, out RectangleF rect))
                    {
                        unreadable = true;
                        continue;
                    }

                    Vector2 point = rect.Center + windowTopLeft;
                    if (!_dependencies.IsInsideWindowInEitherSpace(point) || !_dependencies.IsClickableInEitherSpace(point, path))
                    {
                        unreadable = true;
                        continue;
                    }

                    long address = button.Address;
                    string? observedTitle = null;
                    string outcome = "input rejected before hover check";
                    bool sent = ExecuteInteraction(point, button, _dependencies.GameController, useHoldClick: false,
                        allowWhenHotkeyInactive: allowWhenHotkeyInactive,
                        validateSettledHover: hover =>
                        {
                            // The dispatcher already gave hover a frame to settle. Allow a
                            // little longer for the tooltip's child tree to populate.
                            long deadline = Environment.TickCount64 + 120;
                            while (true)
                            {
                                var state = _dependencies.GameController.IngameState;
                                if (state == null || _dependencies.GameController.Window?.IsForeground() != true)
                                {
                                    outcome = "game state unavailable or window lost focus";
                                    return false;
                                }
                                if (!LabelActionButtons.IsHoverOnButton(hover, address))
                                {
                                    outcome = "hover did not reach the icon";
                                    if (Environment.TickCount64 >= deadline)
                                        return false;
                                    ClickPipelineTiming.Sleep(10);
                                    hover = state.UIHoverElement;
                                    continue;
                                }
                                if (LabelActionButtons.ReadVisibleButton(LabelActionButtons.ReadVisibleLabelRoot(label), row, index)
                                        is not Element current || current.Address != address
                                    || !TryGetActionButtonBounds(current, out RectangleF currentRect)
                                    || !LabelClickPointSearch.IsPointInsideRect(point - windowTopLeft, currentRect))
                                {
                                    outcome = "icon moved or changed";
                                    return false;
                                }

                                observedTitle = LabelActionButtons.ReadTitle(current);
                                if (observedTitle != null)
                                {
                                    if (!string.Equals(observedTitle, tooltipName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        outcome = "different currency";
                                        return false;
                                    }
                                    outcome = "matching title, input guard rejected";
                                    SystemDrawingPoint cursor = Mouse.GetCursorPosition();
                                    return LabelClickPointSearch.IsPointInsideRect(new Vector2(cursor.X, cursor.Y) - windowTopLeft, currentRect)
                                        && _dependencies.IsClickableInEitherSpace(point, path)
                                        && (canStillClick?.Invoke() ?? true);
                                }
                                if (Environment.TickCount64 >= deadline)
                                {
                                    outcome = "tooltip did not populate before timeout";
                                    return false;
                                }

                                ClickPipelineTiming.Sleep(10);
                                hover = state.UIHoverElement;
                            }
                        });
                    LogLabelAction(() => $"[Label action] Row {row} button {index}: expected '{tooltipName}', read '{observedTitle ?? "<unreadable>"}' ({(sent ? "sent" : outcome)})");
                    if (sent)
                        return LabelActionResult.Clicked;

                    // An expected button that failed validation must not be replaced
                    // with a different action.
                    if (string.Equals(observedTitle, tooltipName, StringComparison.OrdinalIgnoreCase))
                        return LabelActionResult.Unavailable;
                    unreadable |= observedTitle == null;
                }

                return unreadable ? LabelActionResult.Unavailable : LabelActionResult.NoMatch;
            }
        }

        private static bool TryGetActionButtonBounds(Element button, out RectangleF rect)
        {
            try
            {
                rect = button.GetClientRect();
                return rect.Width > 0 && rect.Height > 0 && float.IsFinite(rect.Center.X) && float.IsFinite(rect.Center.Y);
            }
            catch
            {
                rect = default;
                return false;
            }
        }

        internal bool TryHandleHarvestUpgrade(LabelOnGround label, string? mechanicId, Vector2 windowTopLeft,
            out bool readyToOpenUpgradedTree, bool allowWhenHotkeyInactive = false)
        {
            readyToOpenUpgradedTree = false;
            return string.Equals(mechanicId, MechanicIds.Harvest, StringComparison.OrdinalIgnoreCase)
                && _dependencies.HarvestUpgrade?.TryHandle(label, windowTopLeft, _dependencies.GameController,
                    this, allowWhenHotkeyInactive, out readyToOpenUpgradedTree) == true;
        }

        internal bool PerformLabelClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
            => ExecuteInteraction(clickPos, expectedElement, controller, false, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

        internal bool PerformTrackedLabelClick(Vector2 clickPos, LabelOnGround? label, bool forceUiHoverVerification)
        {
            CaptureBlightChestDebug(label, mechanicId: null, reason: "reclick", requireChestMechanic: false);
            return PerformLabelClick(
                clickPos,
                DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                    ? rawLabel as Element
                    : null,
                _dependencies.GameController,
                forceUiHoverVerification);
        }

        internal bool PerformMechanicClick(Vector2 clickPos, IntervalKind interval = IntervalKind.Click, Func<Element?, bool>? validateSettledHover = null)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, false,
                interval: interval, validateSettledHover: validateSettledHover);

        internal bool PerformMechanicInteraction(Vector2 clickPos, bool useHoldClick)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, useHoldClick);

        internal bool PerformManualCursorInteraction(Vector2 clickPos, bool useHoldClick)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, useHoldClick, allowWhenHotkeyInactive: true, avoidCursorMove: true);

        internal bool PerformResolvedLabelInteraction(Vector2 clickPos, LabelOnGround label, string? mechanicId,
            bool allowWhenHotkeyInactive = false)
        {
            CaptureBlightChestDebug(label, mechanicId, "click", requireChestMechanic: true);
            return ExecuteInteraction(
                clickPos,
                DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                    ? rawLabel as Element
                    : null,
                _dependencies.GameController,
                SettlersMechanicPolicy.RequiresHoldClick(mechanicId),
                forceUiHoverVerification: OffscreenPathingMath.ShouldForceUiHoverVerificationForLabel(label),
                allowWhenHotkeyInactive: allowWhenHotkeyInactive);
        }

        private void CaptureBlightChestDebug(LabelOnGround? label, string? mechanicId, string reason, bool requireChestMechanic)
        {
            ElementTreeInspector? inspector = _dependencies.BlightChestDebug;
            if (inspector == null)
                return;

            if (requireChestMechanic
                && !BlightChestDebug.IsBlightChestMechanic(mechanicId)
                && !string.Equals(mechanicId, MechanicIds.LeagueChests, StringComparison.OrdinalIgnoreCase))
                return;

            DynamicAccess.TryGetLabelItemOnGround(label, out Entity? entity);
            if (entity == null)
                return;

            string path = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            if (!BlightChestDebug.IsBlightChestPath(path))
                return;

            string detail = mechanicId == null ? path : $"{path} ({mechanicId})";

            // The ElementTree capture walks the whole chest tree (hundreds of nodes, memory reads, string snapshots) — rate-limit it so rapid chest clicks/reclicks don't hammer the GC.
            long now = Environment.TickCount64;
            if (now - _lastBlightChestDebugCaptureTimestampMs < BlightChestDebugCaptureIntervalMs)
                return;
            _lastBlightChestDebugCaptureTimestampMs = now;

            // Debug-only capture over the obfuscated game assembly: a failure here must never abort the click, so the capture is fail-closed at its own boundary (swallowed + logged) while the interaction proceeds.
            try
            {
                inspector.Capture(label, entity, reason, detail);
            }
            catch (Exception ex)
            {
                _dependencies.DebugLog(() => $"[BlightChestDebug] Capture failed: {ex.Message}");
                _dependencies.LogError?.Invoke($"[BlightChestDebug] Capture failed: {ex}");
            }
        }

        internal bool TryResolveLabelClickPosition(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            out Vector2 clickPos,
            string? explicitPath = null)
        {
            DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item);
            string path = ResolveLabelItemPath(label, explicitPath);

            (bool clickResolvable, Vector2 resolvedClickPos) = _dependencies.TryResolveClickPosition(
                label,
                windowTopLeft,
                allLabels,
                point => _dependencies.IsClickableInEitherSpace(point, path));
            if (clickResolvable)
            {
                clickPos = resolvedClickPos;
                return true;
            }

            if (!LabelClickPointResolutionPolicy.ShouldRetryWithoutClickableArea(mechanicId))
            {
                clickPos = default;
                return false;
            }

            if (!LabelClickPointResolutionPolicy.ShouldAllowSettlersRelaxedFallback(
                item != null,
                IsItemWorldProjectionInWindow(item, windowTopLeft)))
            {
                clickPos = default;
                return false;
            }

            (clickResolvable, resolvedClickPos) = _dependencies.TryResolveClickPosition(
                label,
                windowTopLeft,
                allLabels,
                null);
            clickPos = resolvedClickPos;
            return clickResolvable;
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

            SelectionDebugSummary summary = _dependencies.LabelInteractionPort.GetSelectionDebugSummary(allLabels, 0, labelCount);
            return $"{sourceSummary} | {summary.ToShortString()}";
        }

        internal string BuildLabelRangeRejectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int start, int endExclusive, int examined)
        {
            int maxCount = SystemMath.Max(0, endExclusive - start);
            SelectionDebugSummary summary = _dependencies.LabelInteractionPort.GetSelectionDebugSummary(allLabels, start, maxCount);
            return $"range:{start}-{endExclusive} ex:{examined} | {summary.ToShortString()}";
        }

        internal string BuildLabelSourceDebugSummary(IReadOnlyList<LabelOnGround>? cachedLabelSnapshot)
        {
            int cachedCount = cachedLabelSnapshot?.Count ?? 0;
            int visibleCount;
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
            if (!TryProjectEntityAbsolutePosition(item, windowTopLeft, out Vector2 worldScreenAbsolute))
                return false;

            return _dependencies.IsInsideWindowInEitherSpace(worldScreenAbsolute);
        }

        private static string ResolveLabelItemPath(LabelOnGround? label, string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return explicitPath;

            DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item);
            return DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
                ? path
                : string.Empty;
        }

        private bool TryProjectEntityAbsolutePosition(Entity? entity, Vector2 windowTopLeft, out Vector2 absolutePosition)
        {
            if (!EntityScreenProjection.TryProjectEntityScreen(_dependencies.GameController, entity, out Vector2 screenPosition))
            {
                absolutePosition = default;
                return false;
            }

            absolutePosition = new(screenPosition.X + windowTopLeft.X, screenPosition.Y + windowTopLeft.Y);
            return true;
        }
    }
}
