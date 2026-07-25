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

        public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
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
                PublishSelectionLifecycleDebug("SelectionRequested", allLabels, start, end, $"start={startIndex} maxCount={maxCount}");

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

        private LabelOnGround? SelectNextLabelByPriority(IReadOnlyList<LabelOnGround> allLabels, int startIndex, int endExclusive, ClickSettings clickSettings)
        {
            int start = SystemMath.Max(0, startIndex);
            int end = SystemMath.Min(allLabels.Count, endExclusive);
            LabelSelectionResult selection = LabelSelectionEngine.SelectNextLabelByPriority(
                allLabels,
                start,
                end,
                clickSettings,
                label => _dependencies.TryBuildLabelCandidate(label, clickSettings, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason)
                    ? new LabelCandidateBuildResult(true, item, mechanicId, LabelCandidateRejectReason.None)
                    : new LabelCandidateBuildResult(false, item, mechanicId, rejectReason),
                GetCursorDistanceSquaredToLabel);

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

        private float GetCursorDistanceSquaredToLabel(LabelOnGround? label)
        {
            if (label == null || _dependencies.GameController?.Window == null)
                return float.MaxValue;

            if (!TryGetClickableLabelRectCenter(label, out Vector2 center))
                return float.MaxValue;

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

        private static bool TryGetClickableLabelRectCenter(LabelOnGround? label, out Vector2 center)
        {
            center = default;
            Element? element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            RectangleF rect = element.GetClientRect();
            center = rect.Center;
            return rect.Width > 0f && rect.Height > 0f;
        }
    }
}