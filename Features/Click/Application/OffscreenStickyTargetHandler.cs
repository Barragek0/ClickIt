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
            if (target == null)
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            if (!TryIsActiveStickyTarget(target, out string stickyPath, out bool isTargetable))
            {
                ClearStickyOffscreenTarget();
                target = null;
                return false;
            }

            if (ShrineService.IsShrine(target) && !ShrineService.IsClickableShrineCandidate(target))
            {
                ClearStickyOffscreenTarget();
                target = null;
                return false;
            }

            bool isEldritchAltar = OffscreenPathingMath.IsEldritchAltarPath(stickyPath);
            if (OffscreenPathingMath.ShouldDropStickyTargetForUntargetableEldritchAltar(isEldritchAltar, isTargetable))
            {
                ClearStickyOffscreenTarget();
                target = null;
                return false;
            }

            return true;
        }

        private static bool TryIsActiveStickyTarget(Entity target, out string path, out bool isTargetable)
        {
            path = string.Empty;
            isTargetable = false;

            if (!DynamicAccess.TryReadBool(target, static t => t.IsValid, out bool isValid) || !isValid)
                return false;

            if (!DynamicAccess.TryReadBool(target, static t => t.IsHidden, out bool isHidden) || isHidden)
                return false;

            if (OffscreenPathingMath.IsEntityHiddenByMinimapIcon(target))
                return false;

            path = DynamicAccess.TryReadString(target, static t => t.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            isTargetable = DynamicAccess.TryReadBool(target, static t => t.IsTargetable, out bool resolvedIsTargetable)
                && resolvedIsTargetable;
            return true;
        }

        internal bool TryClickStickyTargetIfPossible(Entity stickyTarget, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (ShrineService.IsShrine(stickyTarget))
                return TryClickStickyShrine(stickyTarget);

            return TryClickStickyLabel(stickyTarget, windowTopLeft, allLabels);
        }

        private bool TryClickStickyShrine(Entity stickyTarget)
        {
            NumVector2 shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(stickyTarget.PosNum);
            Vector2 shrinePos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
            string path = stickyTarget.Path ?? string.Empty;
            if (!_dependencies.IsClickableInEitherSpace(shrinePos, path))
                return false;

            bool clickedShrine = _dependencies.LabelInteraction.PerformMechanicClick(shrinePos);
            if (!clickedShrine)
                return false;

            ClearStickyOffscreenTarget();
            _dependencies.ShrineService.InvalidateCache();
            return true;
        }

        private bool TryClickStickyLabel(Entity stickyTarget, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
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
            if (!clickedLabel)
                return false;

            ApplySuccessfulStickyLabelClick(stickyTarget.Path, mechanicId, stickyLabel);
            return true;
        }

        private void ApplySuccessfulStickyLabelClick(string? stickyTargetPath, string mechanicId, LabelOnGround stickyLabel)
            => SuccessfulInteractionAftermathApplier.Apply(
                new SuccessfulInteractionAftermath(
                    Reason: BuildStickyClickSuccessReason(stickyTargetPath),
                    ShouldClearStickyTarget: true,
                    PendingChestMechanicId: mechanicId,
                    PendingChestLabel: stickyLabel),
                _dependencies.HoldDebugTelemetryAfterSuccess,
                clearStickyTarget: ClearStickyOffscreenTarget,
                markPendingChestOpenConfirmation: _dependencies.ChestLootSettlement.MarkPendingChestOpenConfirmation);

        private static string BuildStickyClickSuccessReason(string? stickyTargetPath)
            => string.IsNullOrWhiteSpace(stickyTargetPath)
                ? "Sticky offscreen target click succeeded"
                : $"Sticky offscreen target click succeeded: {stickyTargetPath}";
    }
}