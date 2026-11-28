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
        Action<Func<string>>? CraftingLog = null,
        InventoryInteractionPolicy? Inventory = null,
        Func<Vector2, string, bool>? IsClientPointClickable = null);

    internal sealed class ClickLabelInteractionService(ClickLabelInteractionServiceDependencies dependencies)
    {
        private readonly RecrystallisationDetector _recrystallisationDetector = new();
        private readonly HashSet<long> _essenceFirstCorruptionLabels = [];
        private readonly HashSet<long> _essenceSecondCorruptionLabels = [];
        private readonly Dictionary<long, int> _essenceFirstCorruptionAttempts = [];
        private readonly Dictionary<long, long> _essenceFirstCorruptionDeadlines = [];
        private readonly Lock _essenceCorruptionLock = new();
        private long? _essenceAreaHash;

        /// <summary>
        /// How long a sent corruption is given to show on the label before it is taken to have failed. The Vaal Orb
        /// click is not instant, so waiting a few ticks is expected; waiting forever is not, because the essence stays
        /// the mechanic's best candidate and every tick would be spent here with nothing clicked.
        /// </summary>
        private const int CorruptionSettleMs = 1500;

        /// <summary>How many times one essence's first corruption is sent before this owner gives up on it.</summary>
        private const int MostFirstCorruptionAttempts = 3;

        /// <summary>How long the missing-Vaal-Orbs warning stays on screen after the last corruption attempt.</summary>
        private const int VaalOrbWarningHoldMs = 3_000;
        private const long BlightChestDebugCaptureIntervalMs = 500;
        private readonly ClickLabelInteractionServiceDependencies _dependencies = dependencies;
        private long _lastBlightChestDebugCaptureTimestampMs;
        private long _lastActionRowDebugTimestampMs;
        private long _vaalOrbWarningUntil;

        internal void LogLabelAction(Func<string> message) => _dependencies.CraftingLog?.Invoke(message);

        /// <summary>
        /// The warning to draw over the player while essences cannot be corrupted for want of Vaal Orbs, or null.
        ///
        /// This mirrors the harvest currency warning: corruption is requested, the orb cannot be found in the inventory,
        /// and the player is told rather than left wondering why nothing is being clicked.
        /// </summary>
        internal string? GetVaalOrbWarning()
            => _dependencies.Settings.ClickEssences.Value
                && Environment.TickCount64 < Volatile.Read(ref _vaalOrbWarningUntil)
                ? "No Vaal Orbs to corrupt essences"
                : null;

        private void RaiseVaalOrbWarning()
            => Volatile.Write(ref _vaalOrbWarningUntil, Environment.TickCount64 + VaalOrbWarningHoldMs);

        /// <summary>
        /// Whether the inventory holds at least one Vaal Orb. Unreadable inventory counts as having one, so a missing
        /// read never raises a false warning.
        /// </summary>
        private bool HasVaalOrb()
        {
            if (_dependencies.Inventory is not { } inventory)
                return true;

            IReadOnlyList<Entity> items;
            try
            {
                items = inventory.ReadCurrentInventoryItems(_dependencies.GameController);
            }
            catch
            {
                return true;
            }

            if (items.Count == 0)
                return true;

            for (int i = 0; i < items.Count; i++)
            {
                if (DynamicAccess.TryReadString(items[i], DynamicAccessProfiles.Path, out string path)
                    && path.EndsWith(VaalOrbMetadataSuffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private const string VaalOrbMetadataSuffix = "/Currency/CurrencyCorrupt";

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
            Func<Element?, bool>? validateSettledHover = null,
            Vector2? clientClickPosition = null,
            Func<bool>? canDispatchClick = null)
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
                ValidateSettledHover: validateSettledHover,
                ClientClickPosition: clientClickPosition,
                CanDispatchClick: canDispatchClick));
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
            if (!string.Equals(mechanicId, MechanicIds.Essences, StringComparison.OrdinalIgnoreCase)
                || !_dependencies.Settings.ClickEssences.Value)
                return false;

            lock (_essenceCorruptionLock)
                return TryCorruptEssenceLocked(label, windowTopLeft);
        }

        internal bool CanSelectLabel(LabelOnGround label, string? mechanicId)
        {
            if (mechanicId == MechanicIds.Fates)
                return DynamicAccess.TryGetLabelItemOnGround(label, out Entity? card)
                    && DynamicAccess.TryReadBool(card, DynamicAccessProfiles.IsTargetable, out bool targetable) && targetable;

            if (!IsEssenceOpeningBlocked(label, mechanicId))
                return true;

            if (!_dependencies.Settings.ClickEssences.Value)
                return false;

            lock (_essenceCorruptionLock)
            {
                bool sameArea = AreaUiSnapshotReader.TryReadCurrentAreaHash(_dependencies.GameController, out long areaHash)
                    && _essenceAreaHash == areaHash;
                DynamicAccess.TryGetLabelItemOnGround(label, out Entity? entity);
                long id;
                try { id = entity?.Id ?? 0; }
                catch { return false; }
                if (id <= 0)
                    return false;
                if (sameArea && _essenceSecondCorruptionLabels.Contains(id))
                    return false;

                if (sameArea && _essenceFirstCorruptionLabels.Contains(id))
                {
                    if (!TryReadLabelCorrupted(label, out bool corrupted))
                        return true;
                    if (corrupted)
                        return IsRecrystallisationActive() && ShouldCorruptEssenceSecondTime(label);
                    if (IsCorruptionSettling(id))
                        return true;
                }

                if ((!sameArea || HasFirstCorruptionAttemptsLeft(id)) && CanStillCorruptFirstTime(label))
                {
                    if (HasVaalOrb())
                        return true;
                    RaiseVaalOrbWarning();
                }

                return false;
            }
        }

        internal bool IsEssenceOpeningBlocked(LabelOnGround label, string? mechanicId)
        {
            if (!string.Equals(mechanicId, MechanicIds.Essences, StringComparison.OrdinalIgnoreCase))
                return false;

            EntityEventHub hub = EntityEventHub.Instance;
            hub.EnsureSubscribed(_dependencies.GameController);
            if (hub.IceCaveFossils.Count == 0
                || !DynamicAccess.TryGetLabelItemOnGround(label, out Entity? essence)
                || !TryGetGridPosition(essence, out NumVector2 essencePosition))
                return false;

            const float radius = 200f;
            foreach (Entity fossil in hub.IceCaveFossils.Snapshot())
            {
                if (TryGetGridPosition(fossil, out NumVector2 fossilPosition)
                    && NumVector2.DistanceSquared(essencePosition, fossilPosition) <= radius * radius)
                    return true;
            }

            return false;
        }

        private static bool TryGetGridPosition(Entity? entity, out NumVector2 position)
        {
            position = default;
            if (!DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsValid, out bool valid) || !valid
                || !DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.GridPosNum, out object? raw)
                || raw is not NumVector2 grid || !float.IsFinite(grid.X) || !float.IsFinite(grid.Y))
                return false;

            position = grid;
            return true;
        }

        private bool TryCorruptEssenceLocked(LabelOnGround label, Vector2 windowTopLeft)
        {
            if (!AreaUiSnapshotReader.TryReadCurrentAreaHash(_dependencies.GameController, out long areaHash))
                return true;
            if (_essenceAreaHash != areaHash)
            {
                _essenceFirstCorruptionLabels.Clear();
                _essenceSecondCorruptionLabels.Clear();
                _essenceFirstCorruptionAttempts.Clear();
                _essenceFirstCorruptionDeadlines.Clear();
                _essenceAreaHash = areaHash;
            }

            long id;
            try { id = label.ItemOnGround?.Id ?? 0; }
            catch { id = 0; }
            if (id <= 0)
                return true;

            if (_essenceSecondCorruptionLabels.Contains(id))
                return false;

            if (_essenceFirstCorruptionLabels.Contains(id))
            {
                // An unreadable label cannot establish whether the previous corruption succeeded.
                if (!TryReadLabelCorrupted(label, out bool corrupted))
                    return true;

                if (!corrupted)
                {
                    // The corruption was sent but the label does not read "Corrupted" yet. The Vaal Orb click takes a
                    // moment to land, so hold the tick while it settles; after that the click is taken to have failed
                    // and retried a bounded number of times. Without this bound the essence stays the mechanic's best
                    // candidate and every tick is spent here with nothing clicked.
                    if (IsCorruptionSettling(id))
                        return true;

                    return TryFirstCorruption(id);
                }

                if (IsRecrystallisationActive() && ShouldCorruptEssenceSecondTime(label))
                {
                    if (TryClickVaalOrb(label, windowTopLeft, () => _dependencies.Settings.ClickEssences.Value
                        && IsRecrystallisationActive() && ShouldCorruptEssenceSecondTime(label)))
                        _essenceSecondCorruptionLabels.Add(id);
                    return true;
                }

                _essenceSecondCorruptionLabels.Add(id);
                return false;
            }

            return TryFirstCorruption(id);

            bool TryFirstCorruption(long essenceId)
            {
                if (!CanStillCorruptFirstTime(label) || !HasFirstCorruptionAttemptsLeft(essenceId))
                    return false;

                if (!HasVaalOrb())
                {
                    RaiseVaalOrbWarning();
                    return false;
                }

                if (!TryClickVaalOrb(label, windowTopLeft, () => CanStillCorruptFirstTime(label)))
                    return true;

                _essenceFirstCorruptionAttempts[essenceId] =
                    _essenceFirstCorruptionAttempts.GetValueOrDefault(essenceId) + 1;
                _essenceFirstCorruptionDeadlines[essenceId] = Environment.TickCount64 + CorruptionSettleMs;
                _essenceFirstCorruptionLabels.Add(essenceId);
                return true;
            }
        }

        private bool IsCorruptionSettling(long id)
            => _essenceFirstCorruptionDeadlines.TryGetValue(id, out long deadline)
                && Environment.TickCount64 < deadline;

        private bool HasFirstCorruptionAttemptsLeft(long id)
            => _essenceFirstCorruptionAttempts.GetValueOrDefault(id) < MostFirstCorruptionAttempts;

        private bool CanStillCorruptFirstTime(LabelOnGround label)
            => _dependencies.Settings.ClickEssences.Value
                && _dependencies.LabelInteractionPort.ShouldCorruptEssence(label);

        /// <summary>
        /// Whether the label reads "Corrupted". False when the label element could not be read, so an unreadable
        /// boundary never grants another restriction-free click.
        /// </summary>
        private static bool TryReadLabelCorrupted(LabelOnGround label, out bool corrupted)
        {
            corrupted = false;
            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                || rawLabel is not Element element)
                return false;

            corrupted = LabelElementSearch.ElementContainsAnyStrings(element, ["Corrupted"]);
            return true;
        }

        private bool TryClickVaalOrb(LabelOnGround label, Vector2 windowTopLeft, Func<bool> canStillClick)
            => TryClickLabelAction(label, windowTopLeft, LabelActionButtons.VaalOrb, alternateRowIndex: 2,
                canStillClick: canStillClick) == LabelActionResult.Clicked;

        private bool ShouldCorruptEssenceSecondTime(LabelOnGround label)
        {
            if (!LabelElementSearch.TryGetLabelAdapter(label, out IElementAdapter? adapter) || adapter == null)
                return false;

            // The table selection ignores "Corrupted", since this path deliberately revisits a corrupted essence.
            // The Deafening and special-essence safeguards are shared with the first corruption, so both paths apply
            // the same rule rather than keeping a second copy that can drift.
            return EssenceService.IsSelectedForCorruption(_dependencies.Settings, adapter)
                && !EssenceService.IsBlockedBySafeguard(_dependencies.Settings, adapter);
        }

        private bool IsRecrystallisationActive()
        {
            object? ingameData = null;
            if (DynamicAccess.TryGetDynamicValue(_dependencies.GameController, DynamicAccessProfiles.IngameState, out object? state))
                DynamicAccess.TryGetDynamicValue(state, DynamicAccessProfiles.Data, out ingameData);
            return _recrystallisationDetector.IsActive(ingameData);
        }

        internal enum LabelActionResult { Clicked, NoMatch, Unavailable, Blocked }

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
                if (alternateResult == LabelActionResult.Clicked || result == LabelActionResult.NoMatch
                    || (alternateResult == LabelActionResult.Blocked && result != LabelActionResult.Clicked))
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
                bool blocked = false;
                for (int index = 0; index < count; index++)
                {
                    if (Environment.TickCount64 >= searchDeadline || canStillClick?.Invoke() == false)
                        return LabelActionResult.Unavailable;

                    if (LabelActionButtons.ReadButtonElement(root, row, index) is not Element { Address: not 0 } button)
                        continue;

                    if (!TryGetActionButtonBounds(button, out RectangleF rect))
                    {
                        unreadable = true;
                        continue;
                    }

                    if (!LabelClickPointSearch.TryResolveVisibleClickablePoint(rect, rect.Center, [],
                        candidate => _dependencies.IsInsideWindowInEitherSpace(candidate + windowTopLeft)
                            && _dependencies.IsClickableInEitherSpace(candidate + windowTopLeft, path), out Vector2 clientPoint))
                    {
                        blocked = true;
                        continue;
                    }
                    if (LabelActionButtons.ReadVisibleButton(root, row, index) is not Element)
                    {
                        unreadable = true;
                        continue;
                    }
                    Vector2 point = clientPoint + windowTopLeft;

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
                            bool retriedHover = false;
                            while (true)
                            {
                                if (GameInputSafety.ShouldBlockInput(_dependencies.GameController, _dependencies.Settings))
                                {
                                    outcome = "game is paused";
                                    return false;
                                }

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
                                    SystemDrawingPoint cursor = Mouse.GetCursorPosition();
                                    if (!retriedHover && !Mouse.DisableNativeInput && Environment.TickCount64 + 200 < searchDeadline
                                        && !GameInputSafety.ShouldBlockInput(_dependencies.GameController, _dependencies.Settings)
                                        && LabelClickPointSearch.IsPointInsideRect(new Vector2(cursor.X, cursor.Y) - windowTopLeft, currentRect)
                                        && (canStillClick?.Invoke() ?? true)
                                        && TryFindTooltipResetPoint(currentRect, windowTopLeft, _dependencies.IsInsideWindowInEitherSpace, out Vector2 resetPoint))
                                    {
                                        retriedHover = true;
                                        if (GameInputSafety.ShouldBlockInput(_dependencies.GameController, _dependencies.Settings))
                                        {
                                            outcome = "game is paused";
                                            return false;
                                        }
                                        Input.SetCursorPos(new NumVector2(resetPoint.X, resetPoint.Y));
                                        ClickPipelineTiming.Sleep(40);
                                        SystemDrawingPoint movedCursor = Mouse.GetCursorPosition();
                                        if (_dependencies.GameController.Window?.IsForeground() != true || canStillClick?.Invoke() == false
                                            || Math.Abs(movedCursor.X - resetPoint.X) > 2f || Math.Abs(movedCursor.Y - resetPoint.Y) > 2f
                                            || LabelActionButtons.ReadVisibleButton(LabelActionButtons.ReadVisibleLabelRoot(label), row, index)
                                                is not Element refreshed || refreshed.Address != address
                                            || !TryGetActionButtonBounds(refreshed, out RectangleF refreshedRect)
                                            || !LabelClickPointSearch.IsPointInsideRect(point - windowTopLeft, refreshedRect))
                                        {
                                            outcome = "icon or interaction changed during tooltip retry";
                                            return false;
                                        }
                                        if (GameInputSafety.ShouldBlockInput(_dependencies.GameController, _dependencies.Settings))
                                        {
                                            outcome = "game is paused";
                                            return false;
                                        }
                                        Input.SetCursorPos(new NumVector2(point.X, point.Y));
                                        ClickPipelineTiming.Sleep(60);
                                        deadline = Environment.TickCount64 + 120;
                                        hover = state.UIHoverElement;
                                        continue;
                                    }
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

                return blocked ? LabelActionResult.Blocked : unreadable ? LabelActionResult.Unavailable : LabelActionResult.NoMatch;
            }
        }

        internal static bool TryFindTooltipResetPoint(RectangleF rect, Vector2 windowTopLeft,
            Func<Vector2, bool> insideWindow, out Vector2 point)
        {
            Vector2[] candidates =
            [
                new(rect.Left - 12f, rect.Center.Y), new(rect.Right + 12f, rect.Center.Y),
                new(rect.Center.X, rect.Top - 12f), new(rect.Center.X, rect.Bottom + 12f)
            ];
            foreach (Vector2 candidate in candidates)
            {
                Vector2 absolute = candidate + windowTopLeft;
                if (insideWindow(absolute))
                {
                    point = absolute;
                    return true;
                }
            }
            point = default;
            return false;
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
            out bool readyToOpenUpgradedTree, out bool needsApproach, bool allowWhenHotkeyInactive = false)
        {
            readyToOpenUpgradedTree = false;
            needsApproach = false;
            return string.Equals(mechanicId, MechanicIds.Harvest, StringComparison.OrdinalIgnoreCase)
                && _dependencies.HarvestUpgrade?.TryHandle(label, windowTopLeft, _dependencies.GameController,
                    this, allowWhenHotkeyInactive, out readyToOpenUpgradedTree, out needsApproach) == true;
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

        internal bool PerformMechanicClick(Vector2 clickPos, IntervalKind interval = IntervalKind.Click, Func<Element?, bool>? validateSettledHover = null, Vector2? clientClickPosition = null)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, false,
                interval: interval, validateSettledHover: validateSettledHover, clientClickPosition: clientClickPosition);

        internal bool PerformMechanicInteraction(Vector2 clickPos, bool useHoldClick, Vector2? clientClickPosition = null)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, useHoldClick, clientClickPosition: clientClickPosition);

        internal bool PerformManualCursorInteraction(Vector2 clickPos, bool useHoldClick, Vector2? clientClickPosition = null,
            LabelOnGround? label = null, string? mechanicId = null)
            => ExecuteInteraction(clickPos, null, _dependencies.GameController, useHoldClick, allowWhenHotkeyInactive: true,
                avoidCursorMove: true, clientClickPosition: clientClickPosition,
                canDispatchClick: CreateGroundAzuriteDispatchCheck(label, mechanicId));

        internal bool PerformResolvedLabelInteraction(Vector2 clickPos, LabelOnGround label, string? mechanicId,
            bool allowWhenHotkeyInactive = false, Vector2? clientClickPosition = null)
        {
            if (IsEssenceOpeningBlocked(label, mechanicId))
                return false;

            CaptureBlightChestDebug(label, mechanicId, "click", requireChestMechanic: true);
            return ExecuteInteraction(
                clickPos,
                DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                    ? rawLabel as Element
                    : null,
                _dependencies.GameController,
                SettlersMechanicPolicy.RequiresHoldClick(mechanicId),
                forceUiHoverVerification: OffscreenPathingMath.ShouldForceUiHoverVerificationForLabel(label),
                allowWhenHotkeyInactive: allowWhenHotkeyInactive,
                clientClickPosition: clientClickPosition,
                canDispatchClick: CreateGroundAzuriteDispatchCheck(label, mechanicId));
        }

        private Func<bool>? CreateGroundAzuriteDispatchCheck(LabelOnGround? label, string? mechanicId)
        {
            // A cached label can lose its mechanic ID when force changes between selection and click.
            if (label == null || (mechanicId != MechanicIds.DelveAzuriteOnGround
                && ((!string.IsNullOrWhiteSpace(mechanicId) && mechanicId != MechanicIds.Items)
                    || !MechanicIds.IsDelveAzuriteShardPath(ResolveLabelItemPath(label, explicitPath: null)))))
                return null;

            System.Numerics.Vector3? initialPosition = null;
            return () =>
            {
                if (_dependencies.Settings.ClickAzuriteOnGround?.Value != true
                    || !DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item))
                    return false;

                if (!MechanicClassifier.TryReadGroundAzuriteForce(item, out int force))
                {
                    _dependencies.DebugLog(static () => "[GroundAzurite] Click skipped: Magnetic.Force unavailable.");
                    return false;
                }
                if (force != 0)
                {
                    _dependencies.DebugLog(() => $"[GroundAzurite] Click skipped: Magnetic.Force={force}.");
                    return false;
                }

                // World X/Y can still be changing when the reported force has dropped to zero.
                if (DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.PosNum, out object? rawPosition)
                    && rawPosition is System.Numerics.Vector3 position
                    && float.IsFinite(position.X) && float.IsFinite(position.Y))
                {
                    if (initialPosition is { } previous && (previous.X != position.X || previous.Y != position.Y))
                    {
                        _dependencies.DebugLog(static () => "[GroundAzurite] Click skipped: shard moved while Magnetic.Force=0.");
                        return false;
                    }
                    initialPosition ??= position;
                }
                return true;
            };
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
                point => (_dependencies.IsClientPointClickable ?? _dependencies.IsClickableInEitherSpace)(point, path));
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
