namespace ClickIt.Features.Click
{
    public sealed partial class ClickAutomationPort
    {
        private IInteractionExecutionRuntime InteractionExecutionRuntime => _interactionExecutionRuntime ??= new InteractionExecutionRuntime(CreateInteractionExecutionRuntimeDependencies());

        private AltarAutomationService AltarAutomation => _altarAutomationService ??= new(CreateAltarAutomationServiceDependencies());

        private ClickDebugPublicationService ClickDebugPublisher => _clickDebugPublicationService ??= new(CreateClickDebugPublicationServiceDependencies());

        private GroundLabelEntityAddressProvider GroundLabelEntityAddresses => _groundLabelEntityAddressProvider ??= new(_gameController);

        private VisibleLabelSnapshotProvider VisibleLabelSnapshots => _visibleLabelSnapshotProvider ??= new(_gameController, _cachedLabels);

        private PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression => _pathfindingLabelSuppressionEvaluator ??= new(CreatePathfindingLabelSuppressionEvaluatorDependencies());

        private UltimatumAutomationService UltimatumAutomation => _ultimatumAutomationService ??= new(CreateUltimatumAutomationServiceDependencies());

        private ClickLabelInteractionService LabelInteraction => _labelInteractionService ??= new(CreateClickLabelInteractionServiceDependencies());

        private InteractionExecutionRuntimeDependencies CreateInteractionExecutionRuntimeDependencies()
            => new(
                _support.EnsureCursorInsideGameWindowForClick,
                point => _forceRefreshPointIsInClickableArea(point, string.Empty),
                _support.DebugLog,
                _lockedInteractionDispatcher.PerformClick,
                _lockedInteractionDispatcher.PerformHoldClick,
                _performanceMonitor.RecordClickInterval);

        private AltarAutomationServiceDependencies CreateAltarAutomationServiceDependencies()
            => new(
                _settings,
                _gameController,
                _altarService.GetAltarComponentsReadOnly,
                _altarService.RemoveAltarComponentsByElement,
                pc => _weightCalculator.CalculateAltarWeights(pc),
                (altar, weights, topModsRect, bottomModsRect, topModsTopLeft) => _altarChoiceEvaluator.DetermineChoiceElement(altar, weights, topModsRect, bottomModsRect),
                _support.IsClickableInEitherSpace,
                _support.EnsureCursorInsideGameWindowForClick,
                InteractionExecutionRuntime.Execute,
                _support.DebugLog,
                _errorHandler.LogError,
                _lockedInteractionDispatcher.ElementLock);

        private ClickDebugPublicationServiceDependencies CreateClickDebugPublicationServiceDependencies()
            => new(
                _gameController,
                _support.ShouldCaptureClickDebug,
                _support.PublishClickSnapshot,
                _support.IsClickableInEitherSpace,
                _support.IsInsideWindowInEitherSpace);

        private PathfindingLabelSuppressionEvaluatorDependencies CreatePathfindingLabelSuppressionEvaluatorDependencies()
            => new(
                _settings,
                _runtimeState);

        private UltimatumAutomationServiceDependencies CreateUltimatumAutomationServiceDependencies()
            => new(
                _settings,
                _gameController,
                _cachedLabels,
                _support.EnsureCursorInsideGameWindowForClick,
                _support.IsClickableInEitherSpace,
                messageFactory => _support.DebugLog(messageFactory()),
                (clickPos, clickElement) => _lockedInteractionDispatcher.PerformClick(clickPos, clickElement, _gameController),
                _performanceMonitor.RecordClickInterval,
                _support.ShouldCaptureUltimatumDebug,
                _support.PublishUltimatumEvent);

        private ClickLabelInteractionServiceDependencies CreateClickLabelInteractionServiceDependencies()
            => new(
                _settings,
                _gameController,
                _labelInteractionPort,
                (label, windowTopLeft, allLabels, isClickableArea) =>
                    (_labelClickPointResolver.TryCalculateClickPosition(label, windowTopLeft, allLabels, isClickableArea, out Vector2 clickPos), clickPos),
                _support.IsClickableInEitherSpace,
                _support.IsInsideWindowInEitherSpace,
                InteractionExecutionRuntime.Execute,
                _groundItemsVisible,
                messageFactory => _support.DebugLog(messageFactory()));

    }
}