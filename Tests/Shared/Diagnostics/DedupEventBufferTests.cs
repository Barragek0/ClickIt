namespace ClickIt.Tests.Shared.Diagnostics;

[TestClass]
public class DedupEventBufferTests
{
    [TestMethod]
    public void Add_StoresFirstEvent_WithTimestampPrefix()
    {
        var buffer = new DedupEventBuffer();
        buffer.Add("walking");

        buffer.Events.Count.Should().Be(1);
        buffer.Events[0].Should().EndWith("walking");
        buffer.Events[0].Should().Contain(":");
    }

    [TestMethod]
    public void Add_DedupsRepeatedMessage_WithCountSuffix()
    {
        var buffer = new DedupEventBuffer();
        buffer.Add("same");
        buffer.Add("same");

        buffer.Events.Count.Should().Be(1);
        buffer.Events[0].Should().EndWith("same (x2)");
    }

    [TestMethod]
    public void Add_DedupCount_AccumulatesBeyondTwo()
    {
        var buffer = new DedupEventBuffer();
        buffer.Add("same");
        buffer.Add("same");
        buffer.Add("same");

        buffer.Events.Count.Should().Be(1);
        buffer.Events[0].Should().EndWith("same (x3)");
    }

    [TestMethod]
    public void Add_KeepsDistinctEventsSeparate()
    {
        var buffer = new DedupEventBuffer();
        buffer.Add("a");
        buffer.Add("b");

        buffer.Events.Count.Should().Be(2);
        buffer.Events[0].Should().EndWith("a");
        buffer.Events[1].Should().EndWith("b");
    }

    [TestMethod]
    public void Events_ReturnsSnapshot_NotTheLiveList()
    {
        var buffer = new DedupEventBuffer();
        buffer.Add("first");

        IReadOnlyList<string> snapshot = buffer.Events;

        buffer.Add("second");

        snapshot.Count.Should().Be(1, "a previously read snapshot must not grow with the live buffer");
        snapshot[0].Should().EndWith("first");
        buffer.Events.Count.Should().Be(2);
    }

    [TestMethod]
    public void Add_DedupsAcrossInterleavedMessages()
    {
        var buffer = new DedupEventBuffer();
        buffer.Add("walk");
        buffer.Add("blocked");
        buffer.Add("walk");

        buffer.Events.Count.Should().Be(2);
        buffer.Events.Should().Contain(e => e.EndsWith("walk (x2)"));
        buffer.Events.Should().Contain(e => e.EndsWith("blocked"));
    }

    [TestMethod]
    public void Add_RemovesOldestEvent_WhenOverCap()
    {
        var buffer = new DedupEventBuffer(capacity: 8);
        for (int i = 0; i < 10; i++)
            buffer.Add($"stage-{i}");

        buffer.Events.Count.Should().Be(8);
        buffer.Events[0].Should().EndWith("stage-2");
        buffer.Events[^1].Should().EndWith("stage-9");
    }

    [TestMethod]
    public void Add_DedupRepeatedEvent_DoesNotOverflowCap()
    {
        var buffer = new DedupEventBuffer(capacity: 8);
        for (int i = 0; i < 100; i++)
            buffer.Add("hot");

        buffer.Events.Count.Should().Be(1);
        buffer.Events[0].Should().EndWith("hot (x100)");
    }
}
