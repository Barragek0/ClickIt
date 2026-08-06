namespace ClickIt.Tests.UI;

// Guards the render/coroutine table row-visibility rule: a row appears only when the
// section/coroutine did measurable work in the current window (last or average > 0).
[TestClass]
public class ImGuiDebugOverlayTimingRowTests
{
    [TestMethod]
    public void ShouldShowTimingRow_HidesWhenLastAndAverageAreZero()
    {
        ImGuiDebugOverlay.ShouldShowTimingRow(new TimingMetricsSnapshot(0, 0, 0, 1)).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldShowTimingRow_HidesWhenMaxSpikeAlreadyRolledOut()
    {
        ImGuiDebugOverlay.ShouldShowTimingRow(new TimingMetricsSnapshot(0, 0, 2.3, 60)).Should().BeFalse(
            "a rolled-out max spike alone must not keep the row visible");
    }

    [TestMethod]
    public void ShouldShowTimingRow_ShowsWhenLastExceedsZero()
    {
        ImGuiDebugOverlay.ShouldShowTimingRow(new TimingMetricsSnapshot(1.5, 0, 1.5, 1)).Should().BeTrue();
    }

    [TestMethod]
    public void ShouldShowTimingRow_ShowsWhenAverageExceedsZero()
    {
        ImGuiDebugOverlay.ShouldShowTimingRow(new TimingMetricsSnapshot(0, 0.25, 0.5, 60)).Should().BeTrue();
    }

    [TestMethod]
    public void ShouldShowTimingRow_ShowsWhenLastAndAverageExceedZero()
    {
        ImGuiDebugOverlay.ShouldShowTimingRow(new TimingMetricsSnapshot(0.1, 0.1, 0.2, 60)).Should().BeTrue();
    }
}
