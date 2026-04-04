namespace ClickIt.Features.Click.Application
{
    internal readonly record struct OffscreenStickyTargetHandlerDependencies(
        GameController GameController,
        ShrineService ShrineService,
        ClickRuntimeState RuntimeState,
        ClickLabelInteractionService LabelInteraction,
        ChestLootSettlementTracker ChestLootSettlement,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        ILabelInteractionPort LabelInteractionPort,
        Action<string> HoldDebugTelemetryAfterSuccess);

    internal sealed class OffscreenStickyTargetHandler(OffscreenStickyTargetHandlerDependencies dependencies)
    {
        private readonly OffscreenStickyTargetHandlerDependencies _dependencies = dependencies;

        internal void SetStickyOffscreenTarget(Entity target)
            => _dependencies.RuntimeState.StickyOffscreenTargetAddress = target.Address;

        internal void ClearStickyOffscreenTarget()
            => _dependencies.RuntimeState.StickyOffscreenTargetAddress = 0;

        internal bool IsStickyTarget(Entity? entity)
            => entity != null && OffscreenPathingMath.IsSameEntityAddress(_dependencies.RuntimeState.StickyOffscreenTargetAddress, entity.Address);

        internal bool TryResolveStickyOffscreenTarget(out Entity? target)
        {
            target = null;

            long stickyAddress = _dependencies.RuntimeState.StickyOffscreenTargetAddress;
            if (stickyAddress == 0)
                return false;

            target = EntityQueryService.FindEntityByAddress(_dependencies.GameController, stickyAddress);
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

                bool clickedShrine = _dependencies.LabelInteraction.PerformMechanicClick(shrinePos);
                if (clickedShrine)
                {
                    ClearStickyOffscreenTarget();
                    _dependencies.ShrineService.InvalidateCache();
                }

                return clickedShrine;
            }

            LabelOnGround? stickyLabel = OffscreenPathingMath.FindVisibleLabelForEntity(stickyTarget, allLabels);
            if (stickyLabel == null)
                return false;

            if (_dependencies.PathfindingLabelSuppression.ShouldSuppressPathfindingLabel(stickyLabel))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            string? mechanicId = _dependencies.LabelInteractionPort.GetMechanicIdForLabel(stickyLabel);
            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            (bool resolved, Vector2 clickPos) = _dependencies.LabelInteraction.TryResolveLabelClickPositionResult(
                stickyLabel,
                mechanicId,
                windowTopLeft,
                allLabels,
                stickyTarget.Path);
            if (!resolved)
                return false;

            bool clickedLabel = _dependencies.LabelInteraction.PerformResolvedLabelInteraction(clickPos, stickyLabel, mechanicId);
            if (clickedLabel)
            {
                string stickyReason = string.IsNullOrWhiteSpace(stickyTarget.Path)
                    ? "Sticky offscreen target click succeeded"
                    : $"Sticky offscreen target click succeeded: {stickyTarget.Path}";
                _dependencies.HoldDebugTelemetryAfterSuccess(stickyReason);
                _dependencies.ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, stickyLabel);
                ClearStickyOffscreenTarget();
            }

            return clickedLabel;
        }
    }
}