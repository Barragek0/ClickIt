namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct LostShipmentTargetSelectorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        Action<string> DebugLog,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        Func<Vector2, string, bool> IsClickableInEitherSpace);

    internal sealed class LostShipmentTargetSelector(LostShipmentTargetSelectorDependencies dependencies)
    {
        private readonly LostShipmentTargetSelectorDependencies _dependencies = dependencies;

        internal LostShipmentCandidate? ResolveNextLostShipmentCandidate(IReadOnlyList<LabelOnGround>? labelsOverride = null)
        {
            if (!_dependencies.Settings.ClickLostShipmentCrates.Value)
                return null;

            try
            {
                LostShipmentCandidate? best = null;
                RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                Vector2 cursorAbsolute = ManualCursorSelectionMath.GetCursorAbsolutePosition();

                if (labelsOverride != null)
                {
                    ScanLabelsForBestCandidate(labelsOverride, ref best, cursorAbsolute, windowTopLeft);
                    return best;
                }

                var labels = _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
                if (labels == null || labels.Count == 0)
                    return null;

                ScanLabelsForBestCandidate(labels, ref best, cursorAbsolute, windowTopLeft);
                return best;
            }
            catch (Exception ex)
            {
                _dependencies.DebugLog($"[ResolveNextLostShipmentCandidate] Failed to scan hidden labels: {ex.Message}");
                return null;
            }
        }

        private void ScanLabelsForBestCandidate(
            IEnumerable<LabelOnGround> labels,
            ref LostShipmentCandidate? best,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft)
        {
            foreach (LabelOnGround label in labels)
            {
                if (!TryCreateLostShipmentCandidate(label, windowTopLeft, out LostShipmentCandidate candidate))
                    continue;

                _ = MechanicCandidateResolver.TryPromoteLostShipmentCandidate(ref best, candidate, cursorAbsolute, windowTopLeft);
            }
        }

        private bool TryCreateLostShipmentCandidate(LabelOnGround? label, Vector2 windowTopLeft, out LostShipmentCandidate candidate)
        {
            candidate = default;

            Entity? entity = label?.ItemOnGround;
            if (label == null || entity == null)
                return false;

            if (VisibleMechanicSelectionPolicy.ShouldSkipLostShipmentEntity(entity.IsValid, entity.DistancePlayer, _dependencies.Settings.ClickDistance.Value, entity.IsOpened))
                return false;

            string path = entity.Path ?? string.Empty;
            if (!VisibleMechanicSelectionPolicy.IsLostShipmentEntity(path, entity.RenderName))
                return false;

            if (!TryResolveLostShipmentClickPosition(entity, path, windowTopLeft, out Vector2 clickPos))
                return false;

            candidate = new LostShipmentCandidate(entity, clickPos);
            return true;
        }

        private bool TryResolveLostShipmentClickPosition(Entity entity, string path, Vector2 windowTopLeft, out Vector2 clickPos)
        {
            return VisibleMechanicClickablePointResolver.TryResolveEntityClickablePoint(
                _dependencies.GameController,
                entity,
                path,
                windowTopLeft,
                _dependencies.IsInsideWindowInEitherSpace,
                _dependencies.IsClickableInEitherSpace,
                out clickPos,
                out _,
                out _);
        }
    }
}