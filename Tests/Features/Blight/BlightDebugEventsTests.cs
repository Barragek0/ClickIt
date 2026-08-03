using ClickIt.Features.Blight.Debug;

namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightDebugEventsTests
{
    [TestMethod]
    public void Add_StoresFirstStage_WithTimestampPrefix()
    {
        var events = new BlightDebugEvents();
        events.Add("WALK → walking");

        events.Stages.Count.Should().Be(1);
        events.Stages[0].Should().EndWith("WALK → walking");
        events.Stages[0].Should().Contain(":");
    }

    [TestMethod]
    public void Add_DedupsRepeatedMessage_WithCountSuffix()
    {
        var events = new BlightDebugEvents();
        events.Add("same");
        events.Add("same");

        events.Stages.Count.Should().Be(1);
        events.Stages[0].Should().EndWith("same (x2)");
    }

    [TestMethod]
    public void Add_DedupCount_AccumulatesBeyondTwo()
    {
        var events = new BlightDebugEvents();
        events.Add("same");
        events.Add("same");
        events.Add("same");

        events.Stages.Count.Should().Be(1);
        events.Stages[0].Should().EndWith("same (x3)");
    }

    [TestMethod]
    public void Add_KeepsDistinctStagesSeparate()
    {
        var events = new BlightDebugEvents();
        events.Add("a");
        events.Add("b");

        events.Stages.Count.Should().Be(2);
        events.Stages[0].Should().EndWith("a");
        events.Stages[1].Should().EndWith("b");
    }

    [TestMethod]
    public void Add_RemovesOldestStage_WhenOverCap()
    {
        var events = new BlightDebugEvents();
        for (int i = 0; i < 130; i++)
            events.Add($"stage-{i}");

        events.Stages.Count.Should().Be(128);
        events.Stages[0].Should().EndWith("stage-2");
        events.Stages[^1].Should().EndWith("stage-129");
    }
}
