namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginDebugTelemetryServiceTests
    {
        [TestMethod]
        public void GetSnapshot_ReturnsEmpty_WhenNoPortsAreAvailable()
        {
            var service = new PluginDebugTelemetryService(
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null);

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Should().Be(DebugTelemetrySnapshot.Empty);
        }

        [TestMethod]
        public void GetSnapshot_UsesFrozenSnapshot_WithoutReevaluatingProviders()
        {
            bool shouldThrow = false;
            var service = new PluginDebugTelemetryService(
                () => shouldThrow ? throw new InvalidOperationException("click provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("label provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("path provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("altar provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("weight provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("rendering provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("game controller provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("input provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("settings provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("cached labels provider should not run while frozen") : null,
                () => shouldThrow ? throw new InvalidOperationException("error provider should not run while frozen") : null);

            service.FreezeSnapshot("hold", holdDurationMs: 100);
            shouldThrow = true;

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Should().Be(DebugTelemetrySnapshot.Empty);
            service.TryGetFreezeState(out long remainingMs, out string reason).Should().BeTrue();
            remainingMs.Should().BePositive();
            reason.Should().Be("hold");
        }

        [TestMethod]
        public void Clear_RemovesFrozenSnapshot_AndAllowsFreshProviderEvaluation()
        {
            bool shouldThrow = false;
            var service = new PluginDebugTelemetryService(
                () => shouldThrow ? throw new InvalidOperationException("click provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("label provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("path provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("altar provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("weight provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("rendering provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("game controller provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("input provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("settings provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("cached labels provider should run after clear") : null,
                () => shouldThrow ? throw new InvalidOperationException("error provider should run after clear") : null);

            service.FreezeSnapshot("hold", holdDurationMs: 100);
            shouldThrow = true;

            service.Clear();

            FluentActions.Invoking(service.GetSnapshot)
                .Should().Throw<InvalidOperationException>();

            service.TryGetFreezeState(out long remainingMs, out string reason).Should().BeFalse();
            remainingMs.Should().Be(0);
            reason.Should().BeEmpty();
        }

        [TestMethod]
        public void GetSnapshot_ProjectsCachedLabelsAndRecentErrors_WhenSupportingProvidersAreAvailable()
        {
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => [null!], 50);
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            var errorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            errorHandler.LogError("first error");
            errorHandler.LogError("second error");

            var service = new PluginDebugTelemetryService(
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => cachedLabels,
                () => errorHandler);

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Status.GameControllerAvailable.Should().BeFalse();
            snapshot.Status.CachedLabelsAvailable.Should().BeTrue();
            snapshot.Status.CachedLabelCount.Should().Be(1);
            snapshot.Errors.ServiceAvailable.Should().BeTrue();
            snapshot.Errors.RecentErrors.Should().HaveCount(2);
        }

        [TestMethod]
        public void GetSnapshot_ProjectsClickFrequencyTargetState_FromSettingsAndRuntimeInputs()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = false;
            settings.ClickFrequencyTarget.Value = 123;
            settings.LazyModeClickLimiting.Value = 456;

            var service = new PluginDebugTelemetryService(
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => settings,
                () => null,
                () => null);

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Click.ServiceAvailable.Should().BeFalse();
            snapshot.Click.FrequencyTarget.SettingsAvailable.Should().BeTrue();
            snapshot.Click.FrequencyTarget.ClickTargetMs.Should().Be(123);
            snapshot.Click.FrequencyTarget.LazyModeTargetMs.Should().Be(456);
            snapshot.Click.FrequencyTarget.ShowLazyModeTarget.Should().BeFalse();
            snapshot.Click.FrequencyTarget.TargetIntervalMs.Should().Be(123);
        }

        [TestMethod]
        public void GetSnapshot_ProjectsClickAndUltimatumSettings_FromSettingsProvider()
        {
            var settings = new ClickItSettings();
            settings.ClickHotkeyToggleMode.Value = true;
            settings.ClickDistance.Value = 91;
            settings.ClickFrequencyTarget.Value = 222;
            settings.ClickInitialUltimatum.Value = true;
            settings.ClickUltimatumChoices.Value = false;

            var service = new PluginDebugTelemetryService(
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => settings,
                () => null,
                () => null);

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Click.Settings.SummaryLines.Should().HaveCount(5);
            snapshot.Click.Settings.SummaryLines[0].Should().Contain("hotkeyToggle:True");
            snapshot.Click.Settings.SummaryLines[1].Should().Contain("radius:91");
            snapshot.Click.Settings.SummaryLines[1].Should().Contain("freqTarget:222ms");
            snapshot.Click.Settings.InitialUltimatumClickEnabled.Should().BeTrue();
            snapshot.Click.Settings.OtherUltimatumClickEnabled.Should().BeFalse();
        }

        [TestMethod]
        public void GetSnapshot_ProjectsRenderingQueueDepths_WhenRenderingStateAvailable()
        {
            var rendering = new PluginContext().Rendering;
            rendering.DeferredTextQueue = new DeferredTextQueue();
            rendering.DeferredFrameQueue = new DeferredFrameQueue();
            rendering.DeferredTextQueue.Enqueue("queued", new Vector2(1, 2), Color.White, 14);
            rendering.DeferredFrameQueue.Enqueue(new RectangleF(1, 2, 3, 4), Color.White, 1);

            var service = new PluginDebugTelemetryService(
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => rendering,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null);

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Rendering.ServiceAvailable.Should().BeTrue();
            snapshot.Rendering.PendingTextCount.Should().Be(1);
            snapshot.Rendering.PendingFrameCount.Should().Be(1);
        }

        [TestMethod]
        public void GetSnapshot_ProjectsAltarTelemetry_WhenAltarServiceAvailable()
        {
            var altarService = TestBuilders.BuildTelemetryAltarService(
                topUpsides: ["Top Upside"],
                topDownsides: ["Top Downside"],
                bottomUpsides: ["Bottom Upside"],
                bottomDownsides: ["Bottom Downside"],
                lastScanExarchLabels: 3,
                lastScanEaterLabels: 4,
                lastProcessedAltarType: "EaterOfWorlds");

            var service = new PluginDebugTelemetryService(
                () => null,
                () => null,
                () => null,
                () => altarService,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null);

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Altar.ServiceAvailable.Should().BeTrue();
            snapshot.Altar.ComponentCount.Should().Be(1);
            snapshot.Altar.Components.Should().HaveCount(1);
            snapshot.Altar.Components[0].Top.Upsides.Should().ContainSingle();
            snapshot.Altar.Components[0].Top.Upsides[0].Text.Should().Be("Top Upside");
            snapshot.Altar.Components[0].Bottom.Downsides[0].Text.Should().Be("Bottom Downside");
            snapshot.Altar.ServiceDebug.LastScanExarchLabels.Should().Be(3);
            snapshot.Altar.ServiceDebug.LastScanEaterLabels.Should().Be(4);
            snapshot.Altar.ServiceDebug.LastProcessedAltarType.Should().Be("EaterOfWorlds");
            snapshot.HoveredItem.LabelsAvailable.Should().BeFalse();
        }

        [TestMethod]
        public void FreezeSnapshot_PreservesProjectedAltarTelemetry_WhileFrozen()
        {
            AltarService currentAltarService = TestBuilders.BuildTelemetryAltarService(
                topUpsides: ["Frozen Top"],
                topDownsides: ["Frozen Down"],
                bottomUpsides: [],
                bottomDownsides: [],
                lastScanExarchLabels: 1,
                lastScanEaterLabels: 2,
                lastProcessedAltarType: "SearingExarch");

            var service = new PluginDebugTelemetryService(
                () => null,
                () => null,
                () => null,
                () => currentAltarService,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null,
                () => null);

            service.FreezeSnapshot("hold", holdDurationMs: 100);

            currentAltarService = TestBuilders.BuildTelemetryAltarService(
                topUpsides: ["Live Top"],
                topDownsides: ["Live Down"],
                bottomUpsides: [],
                bottomDownsides: [],
                lastScanExarchLabels: 9,
                lastScanEaterLabels: 8,
                lastProcessedAltarType: "EaterOfWorlds");

            DebugTelemetrySnapshot snapshot = service.GetSnapshot();

            snapshot.Altar.Components[0].Top.Upsides[0].Text.Should().Be("Frozen Top");
            snapshot.Altar.ServiceDebug.LastScanExarchLabels.Should().Be(1);
            snapshot.Altar.ServiceDebug.LastScanEaterLabels.Should().Be(2);
            snapshot.Altar.ServiceDebug.LastProcessedAltarType.Should().Be("SearingExarch");
        }
    }
}