namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct ManualCursorLabelSelectorDependencies(
        GameController GameController,
        ILabelInteractionPort LabelInteractionPort,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        LabelClickPointResolver LabelClickPointResolver);

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
                LabelOnGround? candidate = allLabels[i];
                if (candidate == null)
                    continue;

                if (_dependencies.PathfindingLabelSuppression.ShouldSuppressLeverClick(candidate)
                    || UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(candidate)
                    || _dependencies.LabelClickPointResolver.IsLabelFullyOverlapped(candidate, allLabels))
                {
                    continue;
                }

                string? mechanicId = _dependencies.LabelInteractionPort.GetMechanicIdForLabel(candidate);
                if (string.IsNullOrWhiteSpace(mechanicId))
                    continue;

                Entity? candidateEntity = candidate.ItemOnGround;
                bool shouldUseGroundProjection = ManualCursorSelectionMath.ShouldUseManualGroundProjectionForCandidate(
                    hasBackingEntity: candidateEntity != null,
                    isWorldItem: candidateEntity?.Type == EntityType.WorldItem);
                Vector2 projectedGroundPoint = default;

                bool hasLabelRect = LabelGeometry.TryGetLabelRect(candidate, out RectangleF rect);
                bool cursorInsideLabelRect = hasLabelRect && ManualCursorSelectionMath.IsPointInsideRectInEitherSpace(rect, cursorAbsolute, windowTopLeft);
                bool cursorNearGroundProjection = shouldUseGroundProjection
                    && TryGetGroundProjectionPoint(candidateEntity, windowTopLeft, out projectedGroundPoint)
                    && ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, projectedGroundPoint, windowTopLeft, ManualCursorSelectionMath.GroundProjectionSnapDistancePx);

                if (!ManualCursorSelectionMath.ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect, cursorNearGroundProjection))
                    continue;

                float score = float.MaxValue;
                if (cursorInsideLabelRect)
                {
                    score = ManualCursorSelectionMath.GetManualCursorLabelHitScore(rect, cursorAbsolute, windowTopLeft);
                }

                if (cursorNearGroundProjection)
                {
                    float objectScore = ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, projectedGroundPoint, windowTopLeft);
                    score = Math.Min(score, objectScore);
                }

                if (score >= bestScore)
                    continue;

                bestScore = score;
                selectedLabel = candidate;
                selectedMechanicId = mechanicId;
            }

            return selectedLabel != null && !string.IsNullOrWhiteSpace(selectedMechanicId);
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