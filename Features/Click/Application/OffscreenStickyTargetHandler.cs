namespace ClickIt.Features.Click.Application
{
    internal readonly record struct OffscreenStickyTargetHandlerDependencies(
        GameController GameController,
        ShrineService ShrineService,
        Func<long> GetStickyOffscreenTargetAddress,
        Action<long> SetStickyOffscreenTargetAddress,
        Func<long, Entity?> FindEntityByAddress,
        Func<Vector2, bool> PerformPathingClick,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<LabelOnGround, bool> ShouldSuppressPathfindingLabel,
        Func<LabelOnGround, string?> GetMechanicIdForLabel,
        Func<LabelOnGround, string?, Vector2, IReadOnlyList<LabelOnGround>?, string?, (bool Success, Vector2 ClickPos)> TryResolveLabelClickPosition,
        Func<Vector2, LabelOnGround, string, bool> ExecuteStickyLabelInteraction,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Action<string?, LabelOnGround> MarkPendingChestOpenConfirmation,
        Action InvalidateShrineCache);

    internal sealed class OffscreenStickyTargetHandler(OffscreenStickyTargetHandlerDependencies dependencies)
    {
        private readonly OffscreenStickyTargetHandlerDependencies _dependencies = dependencies;

        internal void SetStickyOffscreenTarget(Entity target)
            => _dependencies.SetStickyOffscreenTargetAddress(target.Address);

        internal void ClearStickyOffscreenTarget()
            => _dependencies.SetStickyOffscreenTargetAddress(0);

        internal bool IsStickyTarget(Entity? entity)
            => entity != null && OffscreenPathingMath.IsSameEntityAddress(_dependencies.GetStickyOffscreenTargetAddress(), entity.Address);

        internal bool TryResolveStickyOffscreenTarget(out Entity? target)
        {
            target = null;

            long stickyAddress = _dependencies.GetStickyOffscreenTargetAddress();
            if (stickyAddress == 0)
                return false;

            target = _dependencies.FindEntityByAddress(stickyAddress);
            if (target == null || !target.IsValid || target.IsHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(target))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            if (ShrineService.IsShrine(target) && !ShrineService.IsClickableShrineCandidate(target))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            string stickyPath = target.Path ?? string.Empty;
            bool isEldritchAltar = OffscreenPathingMath.IsEldritchAltarPath(stickyPath);
            if (OffscreenPathingMath.ShouldDropStickyTargetForUntargetableEldritchAltar(isEldritchAltar, target.IsTargetable))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            return true;
        }

        internal bool TryClickStickyTargetIfPossible(Entity stickyTarget, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (ShrineService.IsShrine(stickyTarget))
            {
                var shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(stickyTarget.PosNum);
                Vector2 shrinePos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
                string path = stickyTarget.Path ?? string.Empty;
                if (!_dependencies.IsClickableInEitherSpace(shrinePos, path))
                    return false;

                bool clickedShrine = _dependencies.PerformPathingClick(shrinePos);
                if (clickedShrine)
                {
                    ClearStickyOffscreenTarget();
                    _dependencies.InvalidateShrineCache();
                }

                return clickedShrine;
            }

            LabelOnGround? stickyLabel = OffscreenPathingMath.FindVisibleLabelForEntity(stickyTarget, allLabels);
            if (stickyLabel == null)
                return false;

            if (_dependencies.ShouldSuppressPathfindingLabel(stickyLabel))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            string? mechanicId = _dependencies.GetMechanicIdForLabel(stickyLabel);
            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            (bool resolved, Vector2 clickPos) = _dependencies.TryResolveLabelClickPosition(
                stickyLabel,
                mechanicId,
                windowTopLeft,
                allLabels,
                stickyTarget.Path);
            if (!resolved)
                return false;

            bool clickedLabel = _dependencies.ExecuteStickyLabelInteraction(clickPos, stickyLabel, mechanicId);
            if (clickedLabel)
            {
                string stickyReason = string.IsNullOrWhiteSpace(stickyTarget.Path)
                    ? "Sticky offscreen target click succeeded"
                    : $"Sticky offscreen target click succeeded: {stickyTarget.Path}";
                _dependencies.HoldDebugTelemetryAfterSuccess(stickyReason);
                _dependencies.MarkPendingChestOpenConfirmation(mechanicId, stickyLabel);
                ClearStickyOffscreenTarget();
            }

            return clickedLabel;
        }
    }
}