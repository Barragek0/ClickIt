namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarServiceDebugInfoTests
    {
        [TestMethod]
        public void RecordProcessedComponent_TracksLastTypeAndAddVsDuplicate()
        {
            var debugInfo = new AltarServiceDebugInfo();

            debugInfo.RecordProcessedComponent(AltarType.SearingExarch, wasAdded: true);
            debugInfo.RecordProcessedComponent(AltarType.EaterOfWorlds, wasAdded: false);

            debugInfo.ComponentsProcessed.Should().Be(2);
            debugInfo.ComponentsAdded.Should().Be(1);
            debugInfo.ComponentsDuplicated.Should().Be(1);
            debugInfo.LastProcessedAltarType.Should().Be(nameof(AltarType.EaterOfWorlds));
        }

        [TestMethod]
        public void RecordUnmatchedMod_TrimsHistoryAndAvoidsDuplicates()
        {
            var debugInfo = new AltarServiceDebugInfo();

            debugInfo.RecordUnmatchedMod("a1", "N");
            debugInfo.RecordUnmatchedMod("b2", "N");
            debugInfo.RecordUnmatchedMod("c3", "N");
            debugInfo.RecordUnmatchedMod("d4", "N");
            debugInfo.RecordUnmatchedMod("e5", "N");
            debugInfo.RecordUnmatchedMod("f6", "N");
            debugInfo.RecordUnmatchedMod("f6", "N");

            debugInfo.ModsUnmatched.Should().Be(7);
            debugInfo.RecentUnmatchedMods.Should().HaveCount(5);
            debugInfo.RecentUnmatchedMods[0].Should().StartWith("b ");
            debugInfo.RecentUnmatchedMods[4].Should().StartWith("f ");
        }

        [TestMethod]
        public void AddDebugStage_CollapsesRepeatedMessages_IntoDedupedEntry()
        {
            var debugInfo = new AltarServiceDebugInfo();

            debugInfo.AddDebugStage("AltarScan: 2 altar label(s) found");
            debugInfo.AddDebugStage("AltarScan: 2 altar label(s) found");
            debugInfo.AddDebugStage("AltarDecision: TopWeightHigher top=50 bottom=10 chose=Top");

            debugInfo.RecentStages.Should().HaveCount(2);
            debugInfo.RecentStages[0].Should().Contain("AltarScan: 2 altar label(s) found");
            debugInfo.RecentStages[0].Should().Contain("(x2)");
            debugInfo.RecentStages[1].Should().Contain("AltarDecision");
        }
    }
}