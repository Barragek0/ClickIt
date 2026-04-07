namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct ManualCursorLabelSelectorDependencies(
        GameController GameController,
        ILabelInteractionPort LabelInteractionPort,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        LabelClickPointResolver LabelClickPointResolver);

    internal readonly record struct ManualCursorEvaluatedCandidate(
        LabelOnGround? Label,
        string? MechanicId,
        bool IsSuppressed,
        bool CursorInsideLabelRect,
        float LabelRectScore,
        bool CursorNearGroundProjection,
        float GroundProjectionScore);

    internal sealed class ManualCursorLabelSelector(ManualCursorLabelSelectorDependencies dependencies)
    {
        private readonly ManualCursorLabelSelectorDependencies _dependencies = dependencies;

        internal bool TryResolveCandidate(
            IReadOnlyList<LabelOnGround>? allLabels,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LabelOnGround? selectedLabel,
            out string? selectedMechanicId)
        {
            selectedLabel = null;
            selectedMechanicId = null;

            if (allLabels == null || allLabels.Count == 0)
                return false;

            float bestScore = float.MaxValue;
            for (int i = 0; i < allLabels.Count; i++)
            {
                if (!TryEvaluateCandidate(allLabels[i], allLabels, cursorAbsolute, windowTopLeft, out ManualCursorEvaluatedCandidate evaluatedCandidate))
                    continue;

                TryPromoteCandidate(
                    evaluatedCandidate,
                    ref bestScore,
                    ref selectedLabel,
                    ref selectedMechanicId);
            }

            return selectedLabel != null && !string.IsNullOrWhiteSpace(selectedMechanicId);
        }

        /**
        Keeps the repo-owned manual-cursor ranking rules testable without fabricating brittle ExileCore label geometry, item, and camera graphs just to reach the score and tie-break logic.
        */
        internal static bool TryResolveEvaluatedCandidates(
            IReadOnlyList<ManualCursorEvaluatedCandidate>? candidates,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LabelOnGround? selectedLabel,
            out string? selectedMechanicId)
        {
            selectedLabel = null;
            selectedMechanicId = null;

            if (candidates == null || candidates.Count == 0)
                return false;

            float bestScore = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                TryPromoteCandidate(
                    candidates[i],
                    ref bestScore,
                    ref selectedLabel,
                    ref selectedMechanicId);
            }

            return selectedLabel != null && !string.IsNullOrWhiteSpace(selectedMechanicId);
        }

        private static void TryPromoteCandidate(
            ManualCursorEvaluatedCandidate candidate,
            ref float bestScore,
            ref LabelOnGround? selectedLabel,
            ref string? selectedMechanicId)
        {
            if (candidate.Label == null
                || candidate.IsSuppressed
                || string.IsNullOrWhiteSpace(candidate.MechanicId)
                || !ManualCursorSelectionMath.ShouldTreatManualCursorAsHoveringCandidate(candidate.CursorInsideLabelRect, candidate.CursorNearGroundProjection))
            {
                return;
            }

            float score = ResolveCandidateScore(candidate);

            if (score >= bestScore)
                return;

            bestScore = score;
            selectedLabel = candidate.Label;
            selectedMechanicId = candidate.MechanicId;
        }

        private bool TryEvaluateCandidate(
            LabelOnGround? candidate,
            IReadOnlyList<LabelOnGround> allLabels,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            out ManualCursorEvaluatedCandidate evaluatedCandidate)
        {
            evaluatedCandidate = default;
            if (candidate == null || IsCandidateSuppressed(candidate, allLabels))
                return false;

            string? mechanicId = _dependencies.LabelInteractionPort.GetMechanicIdForLabel(candidate);
            if (string.IsNullOrWhiteSpace(mechanicId))
                return false;

            Entity? candidateEntity = candidate.ItemOnGround;
            bool cursorInsideLabelRect = TryResolveCursorInsideLabelRect(candidate, cursorAbsolute, windowTopLeft, out float labelRectScore);
            bool cursorNearGroundProjection = TryResolveCursorNearGroundProjection(candidateEntity, cursorAbsolute, windowTopLeft, out float groundProjectionScore);
            if (!ManualCursorSelectionMath.ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect, cursorNearGroundProjection))
                return false;

            evaluatedCandidate = new ManualCursorEvaluatedCandidate(
                candidate,
                mechanicId,
                IsSuppressed: false,
                cursorInsideLabelRect,
                labelRectScore,
                cursorNearGroundProjection,
                groundProjectionScore);
            return true;
        }

        private bool IsCandidateSuppressed(LabelOnGround candidate, IReadOnlyList<LabelOnGround> allLabels)
            => _dependencies.PathfindingLabelSuppression.ShouldSuppressLeverClick(candidate)
                || UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(candidate)
                || _dependencies.LabelClickPointResolver.IsLabelFullyOverlapped(candidate, allLabels);

        private static float ResolveCandidateScore(ManualCursorEvaluatedCandidate candidate)
        {
            float score = float.MaxValue;
            if (candidate.CursorInsideLabelRect)
                score = candidate.LabelRectScore;

            if (candidate.CursorNearGroundProjection)
                score = Math.Min(score, candidate.GroundProjectionScore);

            return score;
        }

        private static bool TryResolveCursorInsideLabelRect(
            LabelOnGround candidate,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            out float labelRectScore)
        {
            labelRectScore = float.MaxValue;
            if (!LabelGeometry.TryGetLabelRect(candidate, out RectangleF rect))
                return false;

            bool cursorInsideLabelRect = ManualCursorSelectionMath.IsPointInsideRectInEitherSpace(rect, cursorAbsolute, windowTopLeft);
            if (!cursorInsideLabelRect)
                return false;

            labelRectScore = ManualCursorSelectionMath.GetManualCursorLabelHitScore(rect, cursorAbsolute, windowTopLeft);
            return true;
        }

        private bool TryResolveCursorNearGroundProjection(
            Entity? candidateEntity,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            out float groundProjectionScore)
        {
            groundProjectionScore = float.MaxValue;
            bool shouldUseGroundProjection = ManualCursorSelectionMath.ShouldUseManualGroundProjectionForCandidate(
                hasBackingEntity: candidateEntity != null,
                isWorldItem: candidateEntity?.Type == EntityType.WorldItem);
            if (!shouldUseGroundProjection || !TryGetGroundProjectionPoint(candidateEntity, windowTopLeft, out Vector2 projectedGroundPoint))
                return false;

            bool cursorNearGroundProjection = ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(
                cursorAbsolute,
                projectedGroundPoint,
                windowTopLeft,
                ManualCursorSelectionMath.GroundProjectionSnapDistancePx);
            if (!cursorNearGroundProjection)
                return false;

            groundProjectionScore = ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(
                cursorAbsolute,
                projectedGroundPoint,
                windowTopLeft);
            return true;
        }

        private bool TryGetGroundProjectionPoint(Entity? item, Vector2 windowTopLeft, out Vector2 projectedPoint)
        {
            projectedPoint = default;
            if (item == null || !item.IsValid)
                return false;

            try
            {
                var worldScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(item.PosNum);
                projectedPoint = new Vector2(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
                return float.IsFinite(projectedPoint.X) && float.IsFinite(projectedPoint.Y);
            }
            catch
            {
                return false;
            }
        }
    }
}