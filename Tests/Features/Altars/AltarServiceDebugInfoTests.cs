namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarServiceDebugInfoTests
    {
        [TestMethod]
        public void ResetForScan_ClearsCountersAndUpdatesTimestamp()
        {
            var debugInfo = new AltarServiceDebugInfo
            {
                ElementsFound = 5,
                ComponentsProcessed = 4,
                ComponentsAdded = 3,
                ComponentsDuplicated = 2,
                ModsMatched = 6,
                ModsUnmatched = 7,
                LastError = "error",
                RecentUnmatchedMods = ["one"]
            };
            DateTime scanTime = new(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);

            debugInfo.ResetForScan(scanTime);

            debugInfo.LastScanTime.Should().Be(scanTime);
            debugInfo.ElementsFound.Should().Be(0);
            debugInfo.ComponentsProcessed.Should().Be(0);
            debugInfo.ComponentsAdded.Should().Be(0);
            debugInfo.ComponentsDuplicated.Should().Be(0);
            debugInfo.ModsMatched.Should().Be(0);
            debugInfo.ModsUnmatched.Should().Be(0);
            debugInfo.LastError.Should().BeEmpty();
            debugInfo.RecentUnmatchedMods.Should().BeEmpty();
        }

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
    }
}