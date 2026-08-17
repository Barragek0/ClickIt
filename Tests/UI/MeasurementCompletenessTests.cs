namespace ClickIt.Tests.UI
{
    [TestClass]
    public class MeasurementCompletenessTests
    {
        // Freeze-rule guard: every ProcessingSection/TimingChannel/IntervalKind must render in the debug tables, the overlay, and the dump.
        [TestMethod]
        public void ProcessingSection_Count_MatchesRenderedAndDlrSurfaces()
        {
            int valueCount = Enum.GetValues<ProcessingSection>().Length;

            valueCount.Should().Be(13, "ProcessingSection is Unknown + 12 sections (Altar/Blight/Click/Flare/Harvest/Label/Pathfinding/Strongbox/Ultimatum/AreaBlockedUi/ManualUiHover/GameStateDump)");
            DynamicAccess.DlrSectionCount.Should().Be(valueCount, "DynamicAccess sizes its per-section DLR counters from the ProcessingSection cardinality");
        }

        [TestMethod]
        public void TimingChannel_Count_IsRendered()
        {
            Enum.GetValues<TimingChannel>().Length.Should().Be(8, "TimingChannel is Unknown + 7 channels (Click/Altar/Flare/Render/Blight/Ultimatum/LabelOverlay)");
        }

        [TestMethod]
        public void IntervalKind_Count_IsRendered()
        {
            Enum.GetValues<IntervalKind>().Length.Should().Be(7, "IntervalKind is Click/Walk/Blight/Label/ClickIt.Features.ClickIt.Features.Area.Blocked/Ultimatum/Flare");
        }
    }
}
