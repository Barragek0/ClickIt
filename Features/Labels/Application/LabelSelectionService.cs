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

        // Selection cache (single-threaded click coroutine): the full label scan only re-runs when
        // the label-list reference changes, a new range is queried, or the cached result goes stale.
        // A null result must not be pinned for the life of a stable label set - validity, distance,
        // and lock state change without the label-list reference changing (a pinned null deadlocks
        // item pickup), so entries are time-bounded like the other click caches.
        private IReadOnlyList<LabelOnGround>? _selectionCacheLabelsRef;
        private readonly Dictionary<(int Start, int MaxCount), (LabelOnGround? Selected, long AtMs)> _selectionCacheByRange = new();
        private const long SelectionCacheWindowMs = 250;

        // Per-label build cache: the selection scan re-runs whenever the 50ms label-list reference
        // changes (and on every suppression-fallback range), and each candidate build costs ~13 DLR
        // reads on the obfuscated game types (the dominant Acquire allocation). The build result is
        // static per entity, so it is cached on the label ADDRESS (stable across snapshots even when
        // wrapper instances differ) with a long window. Distance and label rect are re-read fresh on
        // every scan, so out-of-range rejection and ranking stay live as the player moves. The
        // interaction path re-validates the chosen label before clicking.
        private readonly record struct CachedLabelScanEntry(
            LabelCandidateBuildResult Candidate,
            float Distance,
            RectangleF Rect,
            bool HasRect);
        private readonly Dictionary<long, LabelCandidateBuildResult> _buildCache = [];
        private long _buildCacheWindowStartMs;
        private const long BuildCacheWindowMs = 1000;

        public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
        {
            long now = Environment.TickCount64;
            if (ReferenceEquals(allLabels, _selectionCacheLabelsRef))
            {
                if (_selectionCacheByRange.TryGetValue((startIndex, maxCount), out (LabelOnGround? Selected, long AtMs) cached)
                    && now - cached.AtMs < SelectionCacheWindowMs)
                {
                    return cached.Selected;
                }
            }
            else
            {
                _selectionCacheLabelsRef = allLabels;
                _selectionCacheByRange.Clear();
            }

            LabelOnGround? selected = SelectCore(allLabels, startIndex, maxCount);
            _selectionCacheByRange[(startIndex, maxCount)] = (selected, now);
            return selected;
        }

        private LabelOnGround? SelectCore(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
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

            LabelOnGround? selected = SelectNextLabelByPriority(allLabels, start, end, clickSettings);
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

        private LabelCandidateBuildResult BuildLabelCandidateCached(LabelOnGround label, ClickSettings clickSettings)
            => GetOrBuildScanEntry(label, clickSettings).Candidate;

        private LabelRankInput ResolveRankInputCached(LabelOnGround label, ClickSettings clickSettings)
        {
            CachedLabelScanEntry entry = GetOrBuildScanEntry(label, clickSettings);
            return new LabelRankInput(entry.Distance, ComputeCursorDistance(entry.Rect, entry.HasRect));
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

            // A NullItem rejection is usually a transient item read failure (entity streaming) and
            // must not pin the label as unclickable for the rest of the build-cache window.
            if (!candidate.Success && candidate.RejectReason == LabelCandidateRejectReason.NullItem)
            {
                candidate = _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? freshItem, out string? freshMechanicId, out LabelCandidateRejectReason freshRejectReason)
                    ? new LabelCandidateBuildResult(true, freshItem, freshMechanicId, LabelCandidateRejectReason.None)
                    : new LabelCandidateBuildResult(false, freshItem, freshMechanicId, freshRejectReason);
                _buildCache[address] = candidate;
            }

            // Fresh per scan: the on-screen rect and distance change as the player moves, so they
            // are read live instead of being cached alongside the (static) build result.
            Entity? buildItem = candidate.Item;
            float distance = buildItem != null
                && DynamicAccess.TryReadFloat(buildItem, DynamicAccessProfiles.DistancePlayer, out float resolvedDistance)
                ? resolvedDistance
                : float.MaxValue;

            // An OutOfDistance rejection is distance-dependent and must not stay cached while the
            // player closes in: re-check the fresh distance and rebuild (clears the rejection) when
            // the label is now within range.
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

            bool hasRect = LabelGeometry.TryGetLabelRect(label, out RectangleF rect);

            return new CachedLabelScanEntry(candidate, distance, rect, hasRect);
        }

        private float ComputeCursorDistance(RectangleF rect, bool hasRect)
        {
            if (!hasRect || _dependencies.GameController?.Window == null)
                return float.MaxValue;

            Vector2 center = rect.Center;
            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            SystemDrawingPoint cursor = Mouse.GetCursorPosition();
            Vector2 cursorAbsolute = new(cursor.X, cursor.Y);
            Vector2 cursorClient = cursorAbsolute - windowTopLeft;

            float absDx = cursorAbsolute.X - center.X;
            float absDy = cursorAbsolute.Y - center.Y;
            float absoluteDistanceSq = (absDx * absDx) + (absDy * absDy);

            float clientDx = cursorClient.X - center.X;
            float clientDy = cursorClient.Y - center.Y;
            float clientDistanceSq = (clientDx * clientDx) + (clientDy * clientDy);

            return SystemMath.Min(absoluteDistanceSq, clientDistanceSq);
        }

        private LabelOnGround? SelectNextLabelByPriority(IReadOnlyList<LabelOnGround> allLabels, int startIndex, int endExclusive, ClickSettings clickSettings)
        {
            int start = SystemMath.Max(0, startIndex);
            int end = SystemMath.Min(allLabels.Count, endExclusive);
            LabelSelectionResult selection = LabelSelectionEngine.SelectNextLabelByPriority(
                allLabels,
                start,
                end,
                clickSettings,
                label => BuildLabelCandidateCached(label, clickSettings),
                label => ResolveRankInputCached(label, clickSettings));

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