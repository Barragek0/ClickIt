namespace ClickIt.Features.Labels.Application
{
    internal delegate bool LabelCandidateBuilder(
        LabelOnGround label,
        ClickSettings clickSettings,
        out Entity? item,
        out string? mechanicId,
        out LabelCandidateRejectReason rejectReason);

    internal readonly record struct LabelSelectionServiceDependencies(
        GameController? GameController,
        Func<IReadOnlyList<LabelOnGround>?, ClickSettings> CreateClickSettings,
        Func<bool> ShouldCaptureLabelDebug,
        Action<LabelDebugEvent> PublishLabelDebugStage,
        LabelCandidateBuilder TryBuildLabelCandidate,
        Func<LabelOnGround?, string?> GetMechanicIdForLabelCore)
    {
        public Func<RectangleF>? GetViewport { get; init; }
    }

    internal sealed class LabelSelectionService(LabelSelectionServiceDependencies dependencies) : ILabelSelectionService
    {
        private readonly LabelSelectionServiceDependencies _dependencies = dependencies;

        // Selection cache (single-threaded click coroutine): the full label scan only re-runs when the label-list reference changes, a new range is queried, or the cached result goes stale. A null result must not be pinned for the life of a stable label set because screen position, clickability, and lock state can change without the label-list reference changing.
        private IReadOnlyList<LabelOnGround>? _selectionCacheLabelsRef;
        private readonly Dictionary<(int Start, int MaxCount), (LabelOnGround? Selected, long AtMs)> _selectionCacheByRange = new();
        private const long SelectionCacheWindowMs = 250;
        private const long EmptySelectionCacheWindowMs = 50;

        // Per-label build cache: each candidate build costs ~13 DLR reads on the obfuscated game types, so static mechanic resolution is cached on the label ADDRESS (stable across snapshots even when wrapper instances differ). Distance and label geometry are re-read fresh on every scan; the interaction path re-validates the chosen label before clicking.
        private readonly record struct CachedLabelScanEntry(
            LabelCandidateBuildResult Candidate,
            float Distance,
            RectangleF Rect,
            bool HasRect);
        private readonly Dictionary<long, (LabelCandidateBuildResult Result, long AtMs)> _buildCache = [];
        private const long BuildCacheWindowMs = 1000;

        // Per-label rect cache: the on-screen rect element-tree walk is the dominant per-label DLR cost of the scan, but a label's rect only changes as the player moves, so a short window cuts the walk to ~once per window. The interaction path re-reads the chosen label's rect fresh before clicking, so a briefly stale ranking rect is safe.
        private readonly record struct CachedLabelRectEntry(RectangleF Rect, bool HasRect);
        private readonly Dictionary<long, CachedLabelRectEntry> _rectCache = [];
        private long _rectCacheWindowStartMs;
        private const long RectCacheWindowMs = 50;
        private ClickSettings _activeClickSettings;

        public LabelOnGround? GetNextLabelToClick(
            IReadOnlyList<LabelOnGround>? allLabels,
            int startIndex,
            int maxCount,
            Func<LabelOnGround, LabelCandidateBuildResult, bool>? isAcceptable)
        {
            long now = Environment.TickCount64;

            if (ReferenceEquals(allLabels, _selectionCacheLabelsRef))
            {
                if (_selectionCacheByRange.TryGetValue((startIndex, maxCount), out (LabelOnGround? Selected, long AtMs) cached)
                    && now - cached.AtMs >= 0
                    && now - cached.AtMs < (cached.Selected == null ? EmptySelectionCacheWindowMs : SelectionCacheWindowMs))
                {
                    // Revalidate a cached selection with its current mechanic and point. The predicate includes screen geometry and blocked UI areas, so a default candidate cannot safely answer it.
                    if (cached.Selected == null)
                        return cached.Selected;

                    ClickSettings cachedClickSettings = _dependencies.CreateClickSettings(allLabels);
                    _activeClickSettings = cachedClickSettings;
                    if (isAcceptable == null && !cachedClickSettings.ClickDistanceEnabled)
                        return cached.Selected;

                    CachedLabelScanEntry cachedEntry = GetOrBuildScanEntry(cached.Selected, cachedClickSettings, GetWindowArea());
                    if (cachedEntry.Candidate.MechanicId == MechanicIds.DelveCrawler)
                        _buildCache.Clear();
                    else if (cachedEntry.Candidate.Success
                        && (!cachedClickSettings.ClickDistanceEnabled
                            || cachedEntry.Distance <= cachedClickSettings.ClickDistance))
                    {
                        bool stillEligible = _dependencies.TryBuildLabelCandidate(cached.Selected, cachedClickSettings,
                            out Entity? item, out string? mechanicId, out _);
                        if (stillEligible && mechanicId == cachedEntry.Candidate.MechanicId
                            && (isAcceptable == null || isAcceptable(cached.Selected, BuildResult(true, item, mechanicId, LabelCandidateRejectReason.None))))
                            return cached.Selected;
                        _buildCache.Remove(cached.Selected.Address);
                    }
                }
            }
            else
            {
                _selectionCacheLabelsRef = allLabels;
                _selectionCacheByRange.Clear();
            }

            LabelOnGround? selected = SelectCore(allLabels, startIndex, maxCount, isAcceptable);
            _selectionCacheByRange[(startIndex, maxCount)] = (selected, now);

            return selected;
        }

        private LabelOnGround? SelectCore(
            IReadOnlyList<LabelOnGround>? allLabels,
            int startIndex,
            int maxCount,
            Func<LabelOnGround, LabelCandidateBuildResult, bool>? isAcceptable)
        {
            bool captureDebug = _dependencies.ShouldCaptureLabelDebug();

            if (allLabels == null || allLabels.Count == 0)
            {
                if (captureDebug)
                    PublishSelectionLifecycleDebug("NoLabels", allLabels, 0, 0, "GetNextLabelToClick received an empty label collection");
                return null;
            }

            int start = SystemMath.Max(0, startIndex);
            int end = SystemMath.Min(allLabels.Count, startIndex + SystemMath.Max(0, maxCount));
            ClickSettings clickSettings = _dependencies.CreateClickSettings(allLabels);
            _activeClickSettings = clickSettings;

            if (captureDebug)
            {
                PublishSelectionLifecycleDebug("SelectionRequested", allLabels, start, end,
                    $"start={startIndex} maxCount={maxCount}");
            }

            LabelOnGround? selected = SelectNextLabelByPriority(allLabels, start, end, clickSettings, isAcceptable);
            if (captureDebug)
            {
                if (selected == null)
                {
                    PublishSelectionLifecycleDebug("SelectionReturnedNone", allLabels, start, end, "No label selected");
                }
                else
                {
                    Entity? selectedItem = selected.ItemOnGround;
                    string? selectedMechanic = selectedItem != null
                        ? _dependencies.GetMechanicIdForLabelCore(selected)
                        : null;

                    _dependencies.PublishLabelDebugStage(new LabelDebugEvent("SelectionReturned", start, end, allLabels.Count)
                    {
                        ConsideredCandidates = 0,
                        NullOrDistanceRejected = 0,
                        UntargetableRejected = 0,
                        NoMechanicRejected = 0,
                        IgnoredByDistanceCandidates = 0,
                        SelectedMechanicId = selectedMechanic,
                        SelectedEntityPath = selectedItem?.Path,
                        SelectedDistance = selectedItem?.DistancePlayer ?? 0f,
                        Notes = "Selected label returned to click service"
                    });
                }
            }

            return selected;
        }

        public string? GetMechanicIdForLabel(LabelOnGround? label)
            => _dependencies.GetMechanicIdForLabelCore(label);

        private void PublishSelectionLifecycleDebug(string stage, IReadOnlyList<LabelOnGround>? allLabels, int start, int end, string notes)
        {
            _dependencies.PublishLabelDebugStage(new LabelDebugEvent(stage, start, end, allLabels?.Count ?? 0)
            {
                ConsideredCandidates = 0,
                NullOrDistanceRejected = 0,
                UntargetableRejected = 0,
                NoMechanicRejected = 0,
                IgnoredByDistanceCandidates = 0,
                SelectedMechanicId = string.Empty,
                SelectedEntityPath = string.Empty,
                SelectedDistance = 0f,
                Notes = notes
            });
        }

        private static LabelCandidateBuildResult BuildResult(bool success, Entity? item, string? mechanicId, LabelCandidateRejectReason rejectReason)
        {
            string? entityPath = item != null && DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath) ? resolvedPath : null;
            return new LabelCandidateBuildResult(success, item, mechanicId, entityPath, rejectReason);
        }

        private CachedLabelScanEntry GetOrBuildScanEntry(LabelOnGround label, ClickSettings clickSettings, RectangleF windowArea)
        {
            long address = label.Address;
            long now = Environment.TickCount64;
            bool hasRect = TryGetCachedLabelRect(label, out RectangleF rect);
            if ((_dependencies.GameController != null || _dependencies.GetViewport != null)
                && (windowArea.Width <= 0 || windowArea.Height <= 0 || !hasRect || !LabelGeometry.IsRectOnScreen(rect, windowArea)))
            {
                return new CachedLabelScanEntry(
                    new LabelCandidateBuildResult(false, null, null, null, LabelCandidateRejectReason.NotVisible),
                    float.MaxValue,
                    rect,
                    hasRect);
            }

            // Per-entry TTL: only rebuild entries that have expired, preventing a burst of rebuilds when a global window expires on a dense field.
            if (_buildCache.TryGetValue(address, out (LabelCandidateBuildResult Result, long AtMs) cached)
                && now - cached.AtMs < BuildCacheWindowMs)
            {
                LabelCandidateBuildResult candidate = cached.Result;
                // Mutable/transient per-tick state (targetable/hidden/strongbox lock, item streaming) must not be pinned by the build cache: re-read fresh so an unlock, targetable change, or item read failure advances the scan immediately.
                if (!candidate.Success
                    && (candidate.RejectReason == LabelCandidateRejectReason.Untargetable
                        || candidate.RejectReason == LabelCandidateRejectReason.LockedChest
                        || candidate.RejectReason == LabelCandidateRejectReason.NullItem))
                {
                    candidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? freshItem, out string? freshMechanicId, out LabelCandidateRejectReason freshRejectReason)
                        ? BuildResult(true, freshItem, freshMechanicId, LabelCandidateRejectReason.None)
                        : BuildResult(false, freshItem, freshMechanicId, freshRejectReason);
                    _buildCache[address] = (candidate, now);
                }

                Entity? buildItem = candidate.Item;
                return new CachedLabelScanEntry(candidate, ReadDistance(buildItem), rect, hasRect);
            }

            // Cache miss or expired: build fresh.
            LabelCandidateBuildResult freshCandidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason)
                ? BuildResult(true, item, mechanicId, LabelCandidateRejectReason.None)
                : BuildResult(false, item, mechanicId, rejectReason);
            _buildCache[address] = (freshCandidate, now);

            // A NullItem rejection is usually a transient item read failure and must not pin the label as unclickable for the rest of the window.
            if (!freshCandidate.Success && freshCandidate.RejectReason == LabelCandidateRejectReason.NullItem)
            {
                freshCandidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? freshItem, out string? freshMechanicId, out LabelCandidateRejectReason freshRejectReason)
                    ? BuildResult(true, freshItem, freshMechanicId, LabelCandidateRejectReason.None)
                    : BuildResult(false, freshItem, freshMechanicId, freshRejectReason);
                _buildCache[address] = (freshCandidate, now);
            }

            // Mutable per-tick state (targetable/hidden/strongbox lock) must not be pinned by the build cache.
            if (!freshCandidate.Success
                && (freshCandidate.RejectReason == LabelCandidateRejectReason.Untargetable
                    || freshCandidate.RejectReason == LabelCandidateRejectReason.LockedChest))
            {
                freshCandidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? freshItem2, out string? freshMechanicId2, out LabelCandidateRejectReason freshRejectReason2)
                    ? BuildResult(true, freshItem2, freshMechanicId2, LabelCandidateRejectReason.None)
                    : BuildResult(false, freshItem2, freshMechanicId2, freshRejectReason2);
                _buildCache[address] = (freshCandidate, now);
            }

            Entity? buildItem2 = freshCandidate.Item;
            return new CachedLabelScanEntry(freshCandidate, ReadDistance(buildItem2), rect, hasRect);
        }

        private RectangleF GetWindowArea()
        {
            try
            {
                return _dependencies.GetViewport?.Invoke()
                    ?? GameWindowRectResolver.Resolve(_dependencies.GameController);
            }
            catch
            {
                return default;
            }
        }

        private static float ReadDistance(Entity? item)
            => item != null && DynamicAccess.TryReadFloat(item, DynamicAccessProfiles.DistancePlayer, out float distance)
                ? distance
                : float.MaxValue;

        public bool IsWithinConfiguredClickDistance(Entity? item)
        {
            return !_activeClickSettings.ClickDistanceEnabled
                || ReadDistance(item) <= _activeClickSettings.ClickDistance;
        }

        private bool TryGetCachedLabelRect(LabelOnGround label, out RectangleF rect)
        {
            long address = label.Address;
            long now = Environment.TickCount64;
            if (now - _rectCacheWindowStartMs >= RectCacheWindowMs)
            {
                _rectCache.Clear();
                _rectCacheWindowStartMs = now;
            }

            if (_rectCache.TryGetValue(address, out CachedLabelRectEntry cached))
            {
                rect = cached.Rect;
                return cached.HasRect;
            }

            bool hasRect = LabelGeometry.TryGetLabelRect(label, out rect);
            _rectCache[address] = new CachedLabelRectEntry(rect, hasRect);
            return hasRect;
        }

        // Cursor + window rect are stable within a single scan; resolving them once per scan instead of per label avoids a GetCursorPos syscall per scanned label.
        private readonly record struct CursorRankContext(Vector2 Absolute, Vector2 Client, bool HasWindow);

        private CursorRankContext ResolveCursorRankContext(out RectangleF windowArea)
        {
            windowArea = GetWindowArea();
            if (!(windowArea.Width > 0 && windowArea.Height > 0))
                return default;

            try
            {
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                SystemDrawingPoint cursor = Mouse.GetCursorPosition();
                Vector2 cursorAbsolute = new(cursor.X, cursor.Y);
                return new CursorRankContext(cursorAbsolute, cursorAbsolute - windowTopLeft, true);
            }
            catch
            {
                return default;
            }
        }

        private float ComputeCursorDistance(RectangleF rect, bool hasRect, in CursorRankContext context)
        {
            if (!hasRect || !context.HasWindow)
                return float.MaxValue;

            Vector2 center = rect.Center;
            float absDx = context.Absolute.X - center.X;
            float absDy = context.Absolute.Y - center.Y;
            float absoluteDistanceSq = (absDx * absDx) + (absDy * absDy);

            float clientDx = context.Client.X - center.X;
            float clientDy = context.Client.Y - center.Y;
            float clientDistanceSq = (clientDx * clientDx) + (clientDy * clientDy);

            return SystemMath.Min(absoluteDistanceSq, clientDistanceSq);
        }

        private LabelOnGround? SelectNextLabelByPriority(
            IReadOnlyList<LabelOnGround> allLabels,
            int startIndex,
            int endExclusive,
            ClickSettings clickSettings,
            Func<LabelOnGround, LabelCandidateBuildResult, bool>? isAcceptable)
        {
            int start = SystemMath.Max(0, startIndex);
            int end = SystemMath.Min(allLabels.Count, endExclusive);
            // One GetOrBuildScanEntry per label (candidate + rank together) halves the per-label live DLR reads (distance + label rect) that dominate the LabelScan stage.
            CursorRankContext cursorContext = ResolveCursorRankContext(out RectangleF windowArea);
            LabelSelectionResult selection = LabelSelectionEngine.SelectNextLabelByPriority(
                allLabels,
                start,
                end,
                clickSettings,
                label =>
                {
                    CachedLabelScanEntry entry = GetOrBuildScanEntry(label, clickSettings, windowArea);
                    return new LabelScanEntry(
                        entry.Candidate,
                        new LabelRankInput(entry.Distance, ComputeCursorDistance(entry.Rect, entry.HasRect, cursorContext)));
                },
                isAcceptable);

            LabelOnGround? selected = selection.SelectedCandidate;
            if (_dependencies.ShouldCaptureLabelDebug())
            {
                Entity? selectedEntity = selected?.ItemOnGround;
                string? selectedMechanicId = selectedEntity != null
                    ? selection.SelectedMechanicId
                    : string.Empty;

                _dependencies.PublishLabelDebugStage(new LabelDebugEvent(
                    selected == null ? "SelectionScanNone" : "SelectionScanSelected",
                    start,
                    end,
                    allLabels.Count)
                {
                    ConsideredCandidates = selection.Stats.ConsideredCandidates,
                    NullOrDistanceRejected = selection.Stats.NullOrDistanceRejected,
                    UntargetableRejected = selection.Stats.UntargetableRejected,
                    NoMechanicRejected = selection.Stats.NoMechanicRejected,
                    IgnoredByDistanceCandidates = selection.Stats.IgnoredByDistanceCandidates,
                    SelectedMechanicId = selectedMechanicId,
                    SelectedEntityPath = selectedEntity?.Path,
                    SelectedDistance = selectedEntity?.DistancePlayer ?? 0f,
                    Notes = $"c:{selection.Stats.ConsideredCandidates} nd:{selection.Stats.NullOrDistanceRejected} u:{selection.Stats.UntargetableRejected} nm:{selection.Stats.NoMechanicRejected} ig:{selection.Stats.IgnoredByDistanceCandidates}"
                });
            }

            return selected;
        }
    }
}
