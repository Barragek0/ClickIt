namespace ClickIt.Tests.Features.Observability
{
    [TestClass]
    public class DebugTelemetryProjectionTests
    {
        [TestMethod]
        public void Build_ProjectsClickTelemetry_FromSettingsInputs()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = false;
            settings.ClickFrequencyTarget.Value = 123;
            settings.LazyModeClickLimiting.Value = 456;
            settings.ClickHotkeyToggleMode.Value = true;
            settings.ClickInitialUltimatum.Value = true;
            settings.ClickUltimatumChoices.Value = false;

            DebugTelemetrySnapshot snapshot = DebugTelemetryProjection.Build(
                clickAutomationPort: null,
                labelFilterPort: null,
                pathfindingService: null,
                altarService: null,
                weightCalculator: null,
                renderingState: null,
                gameController: null,
                inputHandler: null,
                settings: settings,
                cachedLabels: null,
                errorHandler: null);

            snapshot.Click.ServiceAvailable.Should().BeFalse();
            snapshot.Click.FrequencyTarget.SettingsAvailable.Should().BeTrue();
            snapshot.Click.FrequencyTarget.ClickTargetMs.Should().Be(123);
            snapshot.Click.FrequencyTarget.LazyModeTargetMs.Should().Be(456);
            snapshot.Click.FrequencyTarget.ShowLazyModeTarget.Should().BeFalse();
            snapshot.Click.Settings.SummaryLines.Should().HaveCount(5);
            snapshot.Click.Settings.SummaryLines[0].Should().Contain("hotkeyToggle:True");
            snapshot.Click.Settings.InitialUltimatumClickEnabled.Should().BeTrue();
            snapshot.Click.Settings.OtherUltimatumClickEnabled.Should().BeFalse();
        }

        [TestMethod]
        public void Build_ProjectsCachedLabelAndErrorTelemetry_WhenSupportingOwnersAreAvailable()
        {
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => [null!], 50);
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            var errorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            errorHandler.LogError("first error");
            errorHandler.LogError("second error");

            DebugTelemetrySnapshot snapshot = DebugTelemetryProjection.Build(
                clickAutomationPort: null,
                labelFilterPort: null,
                pathfindingService: null,
                altarService: null,
                weightCalculator: null,
                renderingState: null,
                gameController: null,
                inputHandler: null,
                settings: null,
                cachedLabels: cachedLabels,
                errorHandler: errorHandler);

            snapshot.Status.GameControllerAvailable.Should().BeFalse();
            snapshot.Status.CachedLabelsAvailable.Should().BeTrue();
            snapshot.Status.CachedLabelCount.Should().Be(1);
            snapshot.Errors.ServiceAvailable.Should().BeTrue();
            snapshot.Errors.RecentErrors.Should().HaveCount(2);
            snapshot.Inventory.Inventory.HasData.Should().BeFalse();
            snapshot.HoveredItem.LabelsAvailable.Should().BeFalse();
        }

        [TestMethod]
        public void Build_ProjectsAltarTelemetry_WhenAltarServiceIsAvailable()
        {
            AltarService altarService = TestBuilders.BuildTelemetryAltarService(
                topUpsides: ["Top Upside"],
                topDownsides: ["Top Downside"],
                bottomUpsides: ["Bottom Upside"],
                bottomDownsides: ["Bottom Downside"],
                lastScanExarchLabels: 3,
                lastScanEaterLabels: 4,
                lastProcessedAltarType: "EaterOfWorlds");

            DebugTelemetrySnapshot snapshot = DebugTelemetryProjection.Build(
                clickAutomationPort: null,
                labelFilterPort: null,
                pathfindingService: null,
                altarService: altarService,
                weightCalculator: null,
                renderingState: null,
                gameController: null,
                inputHandler: null,
                settings: null,
                cachedLabels: null,
                errorHandler: null);

            snapshot.Altar.ServiceAvailable.Should().BeTrue();
            snapshot.Altar.ComponentCount.Should().Be(1);
            snapshot.Altar.Components.Should().HaveCount(1);
            snapshot.Altar.Components[0].Top.Upsides.Should().ContainSingle();
            snapshot.Altar.Components[0].Top.Upsides[0].Text.Should().Be("Top Upside");
            snapshot.Altar.Components[0].Bottom.Downsides[0].Text.Should().Be("Bottom Downside");
            snapshot.Altar.ServiceDebug.LastScanExarchLabels.Should().Be(3);
            snapshot.Altar.ServiceDebug.LastScanEaterLabels.Should().Be(4);
            snapshot.Altar.ServiceDebug.LastProcessedAltarType.Should().Be("EaterOfWorlds");
        }
    }
}