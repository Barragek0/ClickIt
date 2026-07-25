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

            string toggleLine = string.Join(", ",
            [
                $"hotkeyToggle:{settings.ClickHotkeyToggleMode.Value}",
                $"manualCursor:{settings.ClickOnManualUiHoverOnly.Value}",
                $"lazyMode:{settings.LazyMode.Value}",
                $"leftHanded:{settings.LeftHanded.Value}"
            ]);

            string coreClickLine = string.Join(", ",
            [
                $"radius:{settings.ClickDistance.Value}",
                $"freqTarget:{settings.ClickFrequencyTarget.Value}ms",
                $"verifyCursorInWindow:{settings.VerifyCursorInGameWindowBeforeClick.Value}",
                $"verifyUiHoverNonLazy:{settings.VerifyUIHoverWhenNotLazy.Value}",
                $"avoidOverlap:{settings.AvoidOverlappingLabelClickPoints.Value}"
            ]);

            string inputSafetyLine = string.Join(", ",
            [
                $"blockPanels:{settings.BlockOnOpenLeftRightPanel.Value}",
                $"toggleItems:{settings.ToggleItems.Value}",
                $"toggleItemsInterval:{settings.ToggleItemsIntervalMs.Value}ms",
                $"postToggleBlock:{settings.ToggleItemsPostToggleClickBlockMs.Value}ms"
            ]);

            string pathingLine = string.Join(", ",
            [
                $"walkOffscreen:{settings.WalkTowardOffscreenLabels.Value}",
                $"prioritizeOnscreen:{settings.PrioritizeOnscreenClickableMechanicsOverPathfinding.Value}",
                $"pathBudget:{settings.OffscreenPathfindingSearchBudget.Value}"
            ]);

            string chestSettleLine = string.Join(", ",
            [
                $"waitBasicChestDrops:{settings.PauseAfterOpeningBasicChests.Value}",
                $"waitLeagueChestDrops:{settings.PauseAfterOpeningLeagueChests.Value}",
                $"waitHeistChestDrops:{settings.PauseAfterOpeningHeistChests.Value}",
                $"allowNearbyDuringSettle:{settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value}",
                $"nearbySettleDist:{settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance.Value}"
            ]);

            return new ClickSettingsTelemetrySnapshot(
                SummaryLines:
                [
                    toggleLine,
                    coreClickLine,
                    inputSafetyLine,
                    pathingLine,
                    chestSettleLine
                ],
                InitialUltimatumClickEnabled: settings.IsInitialUltimatumClickEnabled(),
                OtherUltimatumClickEnabled: settings.IsOtherUltimatumClickEnabled());
        }
    }
}