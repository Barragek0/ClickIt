namespace ClickIt.Features.Observability
{
    internal sealed record ClickSettingsTelemetrySnapshot(
        IReadOnlyList<string> SummaryLines,
        bool InitialUltimatumClickEnabled,
        bool OtherUltimatumClickEnabled)
    {
        private static readonly IReadOnlyList<string> EmptySummary = [];

        public static readonly ClickSettingsTelemetrySnapshot Empty = new(
            SummaryLines: EmptySummary,
            InitialUltimatumClickEnabled: false,
            OtherUltimatumClickEnabled: false);

        public static ClickSettingsTelemetrySnapshot FromSettings(ClickItSettings? settings)
        {
            settings ??= new ClickItSettings();

            string line1 = string.Join("  ", new[]
            {
                $"Toggle:HT={F(settings.ClickHotkeyToggleMode.Value)} MC={F(settings.ClickOnManualUiHoverOnly.Value)} LM={F(settings.LazyMode.Value)} LH={F(settings.LeftHanded.Value)}",
                $"Click:R={settings.ClickDistance.Value} f={settings.ClickFrequencyTarget.Value}ms vW={F(settings.VerifyCursorInGameWindowBeforeClick.Value)} vH={F(settings.VerifyUIHoverWhenNotLazy.Value)} aO={F(settings.AvoidOverlappingLabelClickPoints.Value)}",
                $"Safety:bP={F(settings.BlockOnOpenLeftRightPanel.Value)} tI={F(settings.ToggleItems.Value)} ti={settings.ToggleItemsIntervalMs.Value} pT={settings.ToggleItemsPostToggleClickBlockMs.Value}"
            });

            string line2 = string.Join("  ", new[]
            {
                $"Walk:wO={F(settings.WalkTowardOffscreenLabels.Value)} pO={F(settings.PrioritizeOnscreenClickableMechanicsOverPathfinding.Value)} pB={settings.OffscreenPathfindingSearchBudget.Value}",
                $"Chest:bC={F(settings.PauseAfterOpeningBasicChests.Value)} lC={F(settings.PauseAfterOpeningLeagueChests.Value)} hC={F(settings.PauseAfterOpeningHeistChests.Value)} aN={F(settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value)} nD={settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance.Value}"
            });

            return new ClickSettingsTelemetrySnapshot(
                SummaryLines: [line1, line2],
                InitialUltimatumClickEnabled: settings.IsInitialUltimatumClickEnabled(),
                OtherUltimatumClickEnabled: settings.IsOtherUltimatumClickEnabled());
        }

        private static string F(bool v) => v ? "T" : "F";
    }
}