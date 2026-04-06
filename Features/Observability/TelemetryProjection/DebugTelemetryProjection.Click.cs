namespace ClickIt.Features.Observability.TelemetryProjection
{
    internal static partial class DebugTelemetryProjection
    {
        private static ClickTelemetrySnapshot BuildClickTelemetry(
            ClickAutomationPort? clickAutomationPort,
            ClickAutomationSupport? clickAutomationSupport,
            LazyModeBlockerService? lazyModeBlockerService,
            GameController? gameController,
            InputHandler? inputHandler,
            ClickItSettings? settings)
        {
            if (clickAutomationPort == null && settings == null)
                return ClickTelemetrySnapshot.Empty;

            List<UltimatumOptionPreviewSnapshot> ultimatumPreview = [];
            if (clickAutomationPort != null
                && clickAutomationPort.TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
                && previews.Count > 0)
            {
                for (int i = 0; i < previews.Count; i++)
                {
                    UltimatumPanelOptionPreview preview = previews[i];
                    ultimatumPreview.Add(new UltimatumOptionPreviewSnapshot(
                        Rect: preview.Rect,
                        ModifierName: preview.ModifierName,
                        PriorityIndex: preview.PriorityIndex,
                        IsSelected: preview.IsSelected));
                }
            }

            return new ClickTelemetrySnapshot(
                ServiceAvailable: clickAutomationPort != null,
                Click: clickAutomationSupport?.GetLatestClickDebug() ?? ClickDebugSnapshot.Empty,
                ClickTrail: clickAutomationSupport?.GetLatestClickDebugTrail() ?? Array.Empty<string>(),
                RuntimeLog: clickAutomationSupport?.GetLatestRuntimeDebugLog() ?? RuntimeDebugLogSnapshot.Empty,
                RuntimeLogTrail: clickAutomationSupport?.GetLatestRuntimeDebugLogTrail() ?? Array.Empty<string>(),
                Ultimatum: clickAutomationSupport?.GetLatestUltimatumDebug() ?? UltimatumDebugSnapshot.Empty,
                UltimatumTrail: clickAutomationSupport?.GetLatestUltimatumDebugTrail() ?? Array.Empty<string>(),
                UltimatumOptionPreview: ultimatumPreview,
                FrequencyTarget: BuildClickFrequencyTargetTelemetry(settings, inputHandler, lazyModeBlockerService, gameController),
                Settings: ClickSettingsTelemetrySnapshot.FromSettings(settings));
        }

        private static ClickFrequencyTargetTelemetrySnapshot BuildClickFrequencyTargetTelemetry(
            ClickItSettings? settings,
            InputHandler? inputHandler,
            LazyModeBlockerService? lazyModeBlockerService,
            GameController? gameController)
        {
            if (settings == null)
                return ClickFrequencyTargetTelemetrySnapshot.Empty;

            IReadOnlyList<LabelOnGround>? visibleLabels = (IReadOnlyList<LabelOnGround>?)gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            bool hasRestrictedItems = PluginClickRuntimeStateEvaluator.ResolveHasLazyModeRestrictedItems(lazyModeBlockerService, visibleLabels);
            bool lazyModeDisableActive = settings.LazyMode.Value
                && PluginClickRuntimeStateEvaluator.ResolveLazyModeDisableActive(settings, inputHandler);
            PluginClickRuntimeStateSnapshot runtimeState = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: settings.LazyMode.Value,
                lazyModeDisableActive: lazyModeDisableActive,
                hasLazyModeRestrictedItems: hasRestrictedItems,
                isRitualActive: PluginClickRuntimeStateEvaluator.ResolveIsRitualActive(gameController),
                poeForeground: PluginClickRuntimeStateEvaluator.ResolvePoeForeground(gameController));

            return new ClickFrequencyTargetTelemetrySnapshot(
                SettingsAvailable: true,
                ClickTargetMs: settings.ClickFrequencyTarget.Value,
                LazyModeTargetMs: settings.LazyModeClickLimiting.Value,
                ShowLazyModeTarget: runtimeState.ShowLazyModeTarget);
        }
    }
}