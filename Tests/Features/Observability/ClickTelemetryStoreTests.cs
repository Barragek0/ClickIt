namespace ClickIt.Tests.Features.Observability
{
    [TestClass]
    public class ClickTelemetryStoreTests
    {
        [TestMethod]
        public void PublishUltimatumEvent_ProjectsSettingsAndEventFields_IntoLatestSnapshot()
        {
            var settings = new ClickItSettings();
            settings.ClickInitialUltimatum.Value = false;
            settings.ClickUltimatumChoices.Value = true;

            var store = new ClickTelemetryStore(settings);

            store.PublishUltimatumEvent(new UltimatumDebugEvent(
                Stage: "PanelSkip",
                Source: "PanelUi",
                IsPanelVisible: false,
                IsGruelingGauntletActive: true)
            {
                HasSaturatedChoice = true,
                SaturatedModifier = "Ruin",
                ShouldTakeReward = true,
                Action = "Skip",
                CandidateCount = 4,
                SaturatedCandidateCount = 2,
                BestModifier = "Ruin IV",
                BestPriority = 7,
                ClickedChoice = false,
                ClickedConfirm = true,
                ClickedTakeRewards = false,
                Notes = "store projection"
            });

            UltimatumDebugSnapshot snapshot = store.GetLatestUltimatumDebug();

            snapshot.HasData.Should().BeTrue();
            snapshot.Stage.Should().Be("PanelSkip");
            snapshot.Source.Should().Be("PanelUi");
            snapshot.IsInitialUltimatumEnabled.Should().BeFalse();
            snapshot.IsOtherUltimatumEnabled.Should().BeTrue();
            snapshot.IsPanelVisible.Should().BeFalse();
            snapshot.IsGruelingGauntletActive.Should().BeTrue();
            snapshot.HasSaturatedChoice.Should().BeTrue();
            snapshot.SaturatedModifier.Should().Be("Ruin");
            snapshot.ShouldTakeReward.Should().BeTrue();
            snapshot.Action.Should().Be("Skip");
            snapshot.CandidateCount.Should().Be(4);
            snapshot.SaturatedCandidateCount.Should().Be(2);
            snapshot.BestModifier.Should().Be("Ruin IV");
            snapshot.BestPriority.Should().Be(7);
            snapshot.ClickedConfirm.Should().BeTrue();
            snapshot.Notes.Should().Be("store projection");
            snapshot.Sequence.Should().BeGreaterThan(0);
            snapshot.TimestampMs.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void PublishUltimatumSnapshot_StoresProvidedSnapshot_AndAddsTrailEntry()
        {
            var store = new ClickTelemetryStore(new ClickItSettings());
            var snapshot = new UltimatumDebugSnapshot(
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
                CandidateCount: 3,
                SaturatedCandidateCount: 0,
                BestModifier: "Ruin II",
                BestPriority: 5,
                ClickedChoice: true,
                ClickedConfirm: true,
                ClickedTakeRewards: false,
                Notes: "snapshot path",
                Sequence: 0,
                TimestampMs: 123);

            store.PublishUltimatumSnapshot(snapshot);

            UltimatumDebugSnapshot latest = store.GetLatestUltimatumDebug();
            IReadOnlyList<string> trail = store.GetLatestUltimatumDebugTrail();

            latest.Stage.Should().Be("PanelHandled");
            latest.Action.Should().Be("Confirm");
            latest.BestModifier.Should().Be("Ruin II");
            latest.Sequence.Should().BeGreaterThan(0);
            trail.Should().ContainSingle();
            trail[0].Should().Contain("PanelHandled");
            trail[0].Should().Contain("snapshot path");
        }
    }
}