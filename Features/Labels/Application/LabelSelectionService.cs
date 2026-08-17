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
        Func<LabelOnGround?, string?> GetMechanicIdForLabelCore);

    internal sealed class LabelSelectionService(LabelSelectionServiceDependencies dependencies) : ILabelSelectionService
    {
        private readonly LabelSelectionServiceDependencies _dependencies = dependencies;

        // Selection cache (single-threaded click coroutine): the full label scan only re-runs when the label-list reference changes, a new range is queried, or the cached result goes stale. A null result must not be pinned for the life of a stable label set - validity, distance, and lock state change without the label-list reference changing (a pinned null deadlocks item pickup), so entries are time-bounded like the other click caches.
        private IReadOnlyList<LabelOnGround>? _selectionCacheLabelsRef;
        private readonly Dictionary<(int Start, int MaxCount), (LabelOnGround? Selected, long AtMs)> _selectionCacheByRange = new();
        private const long SelectionCacheWindowMs = 250;

        // Per-label build cache: the selection scan re-runs whenever the 50ms label-list reference changes (and on every suppression-fallback range), and each candidate build costs ~13 DLR reads on the obfuscated game types (the dominant Acquire allocation). The build result is static per entity, so it is cached on the label ADDRESS (stable across snapshots even when wrapper instances differ) with a long window. Distance and label rect are re-read fresh on every scan, so out-of-range rejection and ranking stay live as the player moves. The interaction path re-validates the chosen label before clicking.
        private readonly record struct CachedLabelScanEntry(
            LabelCandidateBuildResult Candidate,
            float Distance,
            RectangleF Rect,
            bool HasRect);
        private readonly Dictionary<long, LabelCandidateBuildResult> _buildCache = [];
        private long _buildCacheWindowStartMs;
        private const long BuildCacheWindowMs = 1000;

        // Per-label rect cache: the on-screen rect element-tree walk is the dominant per-label DLR cost of the scan, but a label's rect only changes as the player moves, so a short window cuts the walk to ~once per window. The interaction path re-reads the chosen label's rect fresh before clicking, so a briefly stale ranking rect is safe.
        private readonly record struct CachedLabelRectEntry(RectangleF Rect, bool HasRect);
        private readonly Dictionary<long, CachedLabelRectEntry> _rectCache = [];
        private long _rectCacheWindowStartMs;
        private const long RectCacheWindowMs = 250;

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
                    && now - cached.AtMs < SelectionCacheWindowMs)
                {
                    // A gated selection re-validates the cached label against the predicate: a lock/unlock or overlap change within the cache window must not pin a now-suppressed label (the old per-suppression re-query bypassed the cache by using fresh range keys; the single-pass gated scan must re-run only when the cached result is no longer acceptable).
                    if (isAcceptable == null || cached.Selected == null || isAcceptable(cached.Selected, default))
                        return cached.Selected;
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

            if (captureDebug)
            {
                string harvestDebug = clickSettings.HarvestLabelSelectionBlocked
                    ? " harvPref=BLOCKED"
                    : string.Empty;
                PublishSelectionLifecycleDebug("SelectionRequested", allLabels, start, end,
                    $"start={startIndex} maxCount={maxCount}{harvestDebug}");
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

        private CachedLabelScanEntry GetOrBuildScanEntry(LabelOnGround label, ClickSettings clickSettings)
        {
            long address = label.Address;
            long now = Environment.TickCount64;
            if (now - _buildCacheWindowStartMs >= BuildCacheWindowMs)
            {
                _buildCache.Clear();
                _buildCacheWindowStartMs = now;
            }

            if (!_buildCache.TryGetValue(address, out LabelCandidateBuildResult candidate))
            {
                candidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason)
                    ? new LabelCandidateBuildResult(true, item, mechanicId, LabelCandidateRejectReason.None)
                    : new LabelCandidateBuildResult(false, item, mechanicId, rejectReason);
                _buildCache[address] = candidate;
            }

            // A NullItem rejection is usually a transient item read failure (entity streaming) and must not pin the label as unclickable for the rest of the build-cache window.
            if (!candidate.Success && candidate.RejectReason == LabelCandidateRejectReason.NullItem)
            {
                candidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? freshItem, out string? freshMechanicId, out LabelCandidateRejectReason freshRejectReason)
                    ? new LabelCandidateBuildResult(true, freshItem, freshMechanicId, LabelCandidateRejectReason.None)
                    : new LabelCandidateBuildResult(false, freshItem, freshMechanicId, freshRejectReason);
                _buildCache[address] = candidate;
            }

            // Mutable per-tick state (targetable/hidden/strongbox lock) must not be pinned by the build cache: re-read fresh so an unlock or targetable change advances the scan immediately instead of serving a stale rejection for the rest of the window.
            if (!candidate.Success
                && (candidate.RejectReason == LabelCandidateRejectReason.Untargetable
                    || candidate.RejectReason == LabelCandidateRejectReason.LockedChest))
            {
                candidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? freshItem, out string? freshMechanicId, out LabelCandidateRejectReason freshRejectReason)
                    ? new LabelCandidateBuildResult(true, freshItem, freshMechanicId, LabelCandidateRejectReason.None)
                    : new LabelCandidateBuildResult(false, freshItem, freshMechanicId, freshRejectReason);
                _buildCache[address] = candidate;
            }

            // Fresh per scan: the on-screen rect and distance change as the player moves, so they are read live instead of being cached alongside the (static) build result.
            Entity? buildItem = candidate.Item;
            float distance = buildItem != null
                && DynamicAccess.TryReadFloat(buildItem, DynamicAccessProfiles.DistancePlayer, out float resolvedDistance)
                ? resolvedDistance
                : float.MaxValue;

            // An OutOfDistance rejection is distance-dependent and must not stay cached while the player closes in: re-check the fresh distance and rebuild (clears the rejection) when the label is now within range.
            if (candidate.RejectReason == LabelCandidateRejectReason.OutOfDistance
                && distance <= clickSettings.ClickDistance)
            {
                candidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out buildItem, out string? freshMechanicId, out LabelCandidateRejectReason freshRejectReason)
                    ? new LabelCandidateBuildResult(true, buildItem, freshMechanicId, LabelCandidateRejectReason.None)
                    : new LabelCandidateBuildResult(false, buildItem, freshMechanicId, freshRejectReason);
                _buildCache[address] = candidate;
                distance = buildItem != null
                    && DynamicAccess.TryReadFloat(buildItem, DynamicAccessProfiles.DistancePlayer, out float reResolvedDistance)
                    ? reResolvedDistance
                    : distance;
            }

            bool hasRect = TryGetCachedLabelRect(label, out RectangleF rect);

            return new CachedLabelScanEntry(candidate, distance, rect, hasRect);
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

        private CursorRankContext ResolveCursorRankContext()
        {
            if (_dependencies.GameController?.Window == null)
                return default;

            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            SystemDrawingPoint cursor = Mouse.GetCursorPosition();
            Vector2 cursorAbsolute = new(cursor.X, cursor.Y);
            return new CursorRankContext(cursorAbsolute, cursorAbsolute - windowTopLeft, true);
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
            CursorRankContext cursorContext = ResolveCursorRankContext();
            LabelSelectionResult selection = LabelSelectionEngine.SelectNextLabelByPriority(
                allLabels,
                start,
                end,
                clickSettings,
                label =>
                {
                    CachedLabelScanEntry entry = GetOrBuildScanEntry(label, clickSettings);
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
