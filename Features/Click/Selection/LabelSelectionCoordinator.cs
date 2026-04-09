namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct LabelSelectionCoordinatorDependencies(
        GameController GameController,
        LabelSelectionScanEngine ScanEngine,
        ManualCursorLabelSelector ManualCursorLabelSelector,
        ManualCursorVisibleMechanicSelector ManualCursorVisibleMechanicSelector,
        SpecialLabelInteractionHandler SpecialLabelInteractionHandler,
        ManualCursorLabelInteractionHandler ManualCursorLabelInteractionHandler);

    internal sealed class LabelSelectionCoordinator(LabelSelectionCoordinatorDependencies dependencies)
    {
        private readonly LabelSelectionCoordinatorDependencies _dependencies = dependencies;

        public bool ShouldPreferShrineOverLabel(LabelOnGround? label, Entity? shrine)
            => _dependencies.ScanEngine.ShouldPreferShrineOverLabel(label, shrine);

        public LabelOnGround? ResolveNextLabelCandidate(IReadOnlyList<LabelOnGround>? allLabels)
            => _dependencies.ScanEngine.ResolveNextLabelCandidate(allLabels);

        public bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (_dependencies.GameController.Window == null)
                return false;

            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            SystemDrawingPoint cursor = Mouse.GetCursorPosition();
            Vector2 cursorAbsolute = new(cursor.X, cursor.Y);

            if (_dependencies.ManualCursorLabelInteractionHandler.TryClickPreferredAltarOption(cursorAbsolute, windowTopLeft))
                return true;

            if (_dependencies.ManualCursorLabelSelector.TryResolveCandidate(allLabels, cursorAbsolute, windowTopLeft, out LabelOnGround? hoveredLabel, out string? mechanicId))
                return _dependencies.ManualCursorLabelInteractionHandler.TryClickCandidate(hoveredLabel, mechanicId, cursorAbsolute, windowTopLeft, allLabels);

            return _dependencies.ManualCursorVisibleMechanicSelector.TryClick(cursorAbsolute, windowTopLeft);
        }

        public bool ShouldSkipOrHandleSpecialLabel(LabelOnGround nextLabel, Vector2 windowTopLeft)
            => _dependencies.SpecialLabelInteractionHandler.TryHandle(nextLabel, windowTopLeft);
    }
}