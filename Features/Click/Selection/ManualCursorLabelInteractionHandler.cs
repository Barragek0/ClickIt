namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct ManualCursorLabelInteractionHandlerDependencies(
        ClickItSettings Settings,
        AltarAutomationService AltarAutomation,
        ClickLabelInteractionService LabelInteraction,
        ChestLootSettlementTracker ChestLootSettlement,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        PathfindingService PathfindingService,
        UltimatumAutomationService UltimatumAutomation);

    internal sealed class ManualCursorLabelInteractionHandler(ManualCursorLabelInteractionHandlerDependencies dependencies)
    {
        private readonly ManualCursorLabelInteractionHandlerDependencies _dependencies = dependencies;

        internal bool TryClickPreferredAltarOption(Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => _dependencies.AltarAutomation.TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft);

        internal bool TryClickCandidate(
            LabelOnGround hoveredLabel,
            string? mechanicId,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (hoveredLabel == null)
                return false;

            if (ManualCursorSelectionMath.ShouldAttemptManualCursorAltarClick(
                ClickLabelSelectionMath.IsAltarLabel(hoveredLabel),
                _dependencies.AltarAutomation.HasClickableAltars()))
            {
                return TryClickPreferredAltarOption(cursorAbsolute, windowTopLeft);
            }

            if (_dependencies.LabelInteraction.TryCorruptEssence(hoveredLabel, windowTopLeft))
                return true;

            if (_dependencies.Settings.IsInitialUltimatumClickEnabled() && UltimatumLabelMath.IsUltimatumLabel(hoveredLabel))
                return _dependencies.UltimatumAutomation.TryClickPreferredModifier(hoveredLabel, windowTopLeft);

            (bool resolved, Vector2 clickPos) = _dependencies.LabelInteraction.TryResolveLabelClickPositionResult(hoveredLabel, mechanicId, windowTopLeft, allLabels);
            if (!resolved)
                return false;

            bool clicked = _dependencies.LabelInteraction.PerformManualCursorInteraction(clickPos, SettlersMechanicPolicy.RequiresHoldClick(mechanicId));
            if (!clicked)
                return false;

            SuccessfulInteractionAftermathApplier.Apply(
                new SuccessfulInteractionAftermath(
                    Reason: "Successful manual cursor interaction",
                    ShouldClearPath: _dependencies.Settings.WalkTowardOffscreenLabels.Value,
                    PendingChestMechanicId: mechanicId,
                    PendingChestLabel: hoveredLabel,
                    ShouldRecordLeverClick: true),
                static _ => { },
                clearPath: _dependencies.PathfindingService.ClearLatestPath,
                markPendingChestOpenConfirmation: _dependencies.ChestLootSettlement.MarkPendingChestOpenConfirmation,
                recordLeverClick: _dependencies.PathfindingLabelSuppression.RecordLeverClick);

            return true;
        }
    }
}