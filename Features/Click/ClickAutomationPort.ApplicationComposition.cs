namespace ClickIt.Features.Click
{
    public sealed partial class ClickAutomationPort
    {
        private IInteractionExecutionRuntime InteractionExecutionRuntime => field ??= new InteractionExecutionRuntime(CreateInteractionExecutionRuntimeDependencies());

        private AltarAutomationService AltarAutomation => field ??= new(CreateAltarAutomationServiceDependencies());

        private ClickDebugPublicationService ClickDebugPublisher => field ??= new(CreateClickDebugPublicationServiceDependencies());

        private GroundLabelEntityAddressProvider GroundLabelEntityAddresses => field ??= new(_gameController);

        private VisibleLabelSnapshotProvider VisibleLabelSnapshots => field ??= new(_gameController, _cachedLabels);

        private PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression => field ??= new(CreatePathfindingLabelSuppressionEvaluatorDependencies());

        private UltimatumAutomationService UltimatumAutomation => field ??= new(CreateUltimatumAutomationServiceDependencies());

        private ClickLabelInteractionService LabelInteraction => field ??= new(CreateClickLabelInteractionServiceDependencies());

        private InteractionExecutionRuntimeDependencies CreateInteractionExecutionRuntimeDependencies()
            => new(
                ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
                point => _forceRefreshPointIsInClickableArea(point, string.Empty),
                ClickAutomationSupport.DebugLog,
                LockedInteractionDispatcher.PerformClick,
                LockedInteractionDispatcher.PerformHoldClick,
                _performanceMonitor.RecordClickInterval);

        private AltarAutomationServiceDependencies CreateAltarAutomationServiceDependencies()
            => new(
                _settings,
                _gameController,
                _altarService.GetAltarComponentsReadOnly,
                _altarService.RemoveAltarComponentsByElement,
                _weightCalculator.CalculateAltarWeights,
                (altar, weights, topModsRect, bottomModsRect, topModsTopLeft) => _altarChoiceEvaluator.DetermineChoiceElement(altar, weights, topModsRect, bottomModsRect),
                ClickAutomationSupport.IsClickableInEitherSpace,
                ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
                InteractionExecutionRuntime.Execute,
                ClickAutomationSupport.DebugLog,
                _errorHandler.LogError,
                LockedInteractionDispatcher.ElementLock);

        private ClickDebugPublicationServiceDependencies CreateClickDebugPublicationServiceDependencies()
            => new(
                _gameController,
                ClickAutomationSupport.ShouldCaptureClickDebug,
                ClickAutomationSupport.PublishClickSnapshot,
                ClickAutomationSupport.IsClickableInEitherSpace,
                ClickAutomationSupport.IsInsideWindowInEitherSpace);

        private PathfindingLabelSuppressionEvaluatorDependencies CreatePathfindingLabelSuppressionEvaluatorDependencies()
            => new(
                _settings,
                _runtimeState);

        private UltimatumAutomationServiceDependencies CreateUltimatumAutomationServiceDependencies()
            => new(
                _settings,
                _gameController,
                _cachedLabels,
                ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
                ClickAutomationSupport.IsClickableInEitherSpace,
                messageFactory => ClickAutomationSupport.DebugLog(messageFactory()),
                (clickPos, clickElement) => LockedInteractionDispatcher.PerformClick(clickPos, clickElement, _gameController),
                _performanceMonitor.RecordClickInterval,
                ClickAutomationSupport.ShouldCaptureUltimatumDebug,
                ClickAutomationSupport.PublishUltimatumEvent);

        private ClickLabelInteractionServiceDependencies CreateClickLabelInteractionServiceDependencies()
            => new(
                _settings,
                _gameController,
                _labelInteractionPort,
                (label, windowTopLeft, allLabels, isClickableArea) =>
                    (_labelClickPointResolver.TryCalculateClickPosition(label, windowTopLeft, allLabels, isClickableArea, out Vector2 clickPos), clickPos),
                ClickAutomationSupport.IsClickableInEitherSpace,
                ClickAutomationSupport.IsInsideWindowInEitherSpace,
                InteractionExecutionRuntime.Execute,
                _groundItemsVisible,
                messageFactory => ClickAutomationSupport.DebugLog(messageFactory()));

    }
}