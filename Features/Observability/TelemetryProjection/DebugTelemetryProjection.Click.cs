namespace ClickIt.Features.Observability.TelemetryProjection
{
    internal static partial class DebugTelemetryProjection
    {
        private static ClickTelemetrySnapshot BuildClickTelemetry(
            ClickAutomationPort? clickAutomationPort,
            LabelFilterPort? labelFilterPort,
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
                Click: clickAutomationPort?.GetLatestClickDebug() ?? ClickDebugSnapshot.Empty,
                ClickTrail: clickAutomationPort?.GetLatestClickDebugTrail() ?? Array.Empty<string>(),
                RuntimeLog: clickAutomationPort?.GetLatestRuntimeDebugLog() ?? RuntimeDebugLogSnapshot.Empty,
                RuntimeLogTrail: clickAutomationPort?.GetLatestRuntimeDebugLogTrail() ?? Array.Empty<string>(),
                Ultimatum: clickAutomationPort?.GetLatestUltimatumDebug() ?? UltimatumDebugSnapshot.Empty,
                UltimatumTrail: clickAutomationPort?.GetLatestUltimatumDebugTrail() ?? Array.Empty<string>(),
                UltimatumOptionPreview: ultimatumPreview,
                FrequencyTarget: BuildClickFrequencyTargetTelemetry(settings, inputHandler, labelFilterPort, gameController),
                Settings: ClickSettingsTelemetrySnapshot.FromSettings(settings));
        }

        private static ClickFrequencyTargetTelemetrySnapshot BuildClickFrequencyTargetTelemetry(
            ClickItSettings? settings,
            InputHandler? inputHandler,
            LabelFilterPort? labelFilterPort,
            GameController? gameController)
        {
            if (settings == null)
                return ClickFrequencyTargetTelemetrySnapshot.Empty;

            IReadOnlyList<LabelOnGround>? visibleLabels = (IReadOnlyList<LabelOnGround>?)gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            bool hasRestrictedItems = PluginClickRuntimeStateEvaluator.ResolveHasLazyModeRestrictedItems(labelFilterPort, visibleLabels);
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