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
                clickAutomationSupport: null,
                labelDebugService: null,
                lazyModeBlockerService: null,
                inventoryProbeService: null,
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
                clickAutomationSupport: null,
                labelDebugService: null,
                lazyModeBlockerService: null,
                inventoryProbeService: null,
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
        public void Build_ProjectsRecentErrors_EvenWhenDebugLoggingIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = false;
            var errorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            errorHandler.LogError("background error");

            DebugTelemetrySnapshot snapshot = DebugTelemetryProjection.Build(
                clickAutomationPort: null,
                clickAutomationSupport: null,
                labelDebugService: null,
                lazyModeBlockerService: null,
                inventoryProbeService: null,
                pathfindingService: null,
                altarService: null,
                weightCalculator: null,
                renderingState: null,
                gameController: null,
                inputHandler: null,
                settings: null,
                cachedLabels: null,
                errorHandler: errorHandler);

            snapshot.Errors.ServiceAvailable.Should().BeTrue();
            snapshot.Errors.RecentErrors.Should().ContainSingle();
            snapshot.Errors.RecentErrors[0].Should().Contain("background error");
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
                clickAutomationSupport: null,
                labelDebugService: null,
                lazyModeBlockerService: null,
                inventoryProbeService: null,
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

        [TestMethod]
        public void Build_ProjectsClickSupportTelemetry_WhenSupportHasPublishedSnapshots()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.DebugShowClicking.Value = true;
            settings.DebugShowUltimatum.Value = true;
            settings.ClickFrequencyTarget.Value = 123;
            settings.LazyModeClickLimiting.Value = 456;

            ClickAutomationSupport support = CreateSupport(settings);
            support.PublishClickSnapshot(new ClickDebugSnapshot(
                HasData: true,
                Stage: "Clicked",
                MechanicId: "strongbox",
                EntityPath: "Metadata/TestTarget",
                Distance: 12f,
                WorldScreenRaw: new Vector2(1f, 2f),
                WorldScreenAbsolute: new Vector2(3f, 4f),
                ResolvedClickPoint: new Vector2(5f, 6f),
                Resolved: true,
                CenterInWindow: true,
                CenterClickable: true,
                ResolvedInWindow: true,
                ResolvedClickable: true,
                Notes: "click note",
                Sequence: 7,
                TimestampMs: 8));
            support.PublishRuntimeLog("runtime note");
            support.PublishUltimatumSnapshot(new UltimatumDebugSnapshot(
                HasData: true,
                Stage: "PanelHandled",
                Source: "PanelUi",
                IsInitialUltimatumEnabled: true,
                IsOtherUltimatumEnabled: true,
                IsPanelVisible: true,
                IsGruelingGauntletActive: false,
                HasSaturatedChoice: false,
                SaturatedModifier: string.Empty,
                ShouldTakeReward: false,
                Action: "Confirm",
                CandidateCount: 2,
                SaturatedCandidateCount: 0,
                BestModifier: "Ruin",
                BestPriority: 3,
                ClickedChoice: true,
                ClickedConfirm: true,
                ClickedTakeRewards: false,
                Notes: "ultimatum note",
                Sequence: 9,
                TimestampMs: 10));

            DebugTelemetrySnapshot snapshot = DebugTelemetryProjection.Build(
                clickAutomationPort: null,
                clickAutomationSupport: support,
                labelDebugService: null,
                lazyModeBlockerService: null,
                inventoryProbeService: null,
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
            snapshot.Click.Click.HasData.Should().BeTrue();
            snapshot.Click.Click.Stage.Should().Be("Clicked");
            snapshot.Click.ClickTrail.Should().ContainSingle().Which.Should().Contain("Clicked");
            snapshot.Click.RuntimeLog.HasData.Should().BeTrue();
            snapshot.Click.RuntimeLog.Message.Should().Be("runtime note");
            snapshot.Click.RuntimeLogTrail.Should().ContainSingle().Which.Should().Contain("runtime note");
            snapshot.Click.Ultimatum.HasData.Should().BeTrue();
            snapshot.Click.Ultimatum.Stage.Should().Be("PanelHandled");
            snapshot.Click.UltimatumTrail.Should().ContainSingle().Which.Should().Contain("PanelHandled");
            snapshot.Click.UltimatumOptionPreview.Should().BeEmpty();
            snapshot.Click.FrequencyTarget.ClickTargetMs.Should().Be(123);
            snapshot.Click.FrequencyTarget.LazyModeTargetMs.Should().Be(456);
        }

        [TestMethod]
        public void Build_DoesNotProjectClickSupportTelemetry_WhenSettingsAndServiceAreMissing()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.DebugShowClicking.Value = true;

            ClickAutomationSupport support = CreateSupport(settings);
            support.PublishClickSnapshot(new ClickDebugSnapshot(
                HasData: true,
                Stage: "Clicked",
                MechanicId: string.Empty,
                EntityPath: string.Empty,
                Distance: 0f,
                WorldScreenRaw: default,
                WorldScreenAbsolute: default,
                ResolvedClickPoint: default,
                Resolved: false,
                CenterInWindow: false,
                CenterClickable: false,
                ResolvedInWindow: false,
                ResolvedClickable: false,
                Notes: "should stay hidden",
                Sequence: 1,
                TimestampMs: 2));

            DebugTelemetrySnapshot snapshot = DebugTelemetryProjection.Build(
                clickAutomationPort: null,
                clickAutomationSupport: support,
                labelDebugService: null,
                lazyModeBlockerService: null,
                inventoryProbeService: null,
                pathfindingService: null,
                altarService: null,
                weightCalculator: null,
                renderingState: null,
                gameController: null,
                inputHandler: null,
                settings: null,
                cachedLabels: null,
                errorHandler: null);

            snapshot.Click.Should().BeSameAs(ClickTelemetrySnapshot.Empty);
        }

        private static ClickAutomationSupport CreateSupport(ClickItSettings settings)
        {
            return new ClickAutomationSupport(new ClickAutomationSupportDependencies(
                Settings: settings,
                TelemetryStore: new ClickTelemetryStore(settings),
                GetWindowRectangle: static () => new RectangleF(0f, 0f, 100f, 100f),
                GetCursorPosition: static () => new Vector2(0f, 0f),
                PointIsInClickableArea: static (_, _) => false,
                LogMessage: static _ => { },
                FreezeDebugTelemetrySnapshot: null));
        }
    }
}