namespace ClickIt.Tests.Features.Observability.Performance;

[TestClass]
public class ExpiringSampleBufferTests
{
    private sealed class FakeClock
    {
        public long Now { get; set; }
    }

    [TestMethod]
    public void Stats_Empty_ReturnsZeros()
    {
        var buffer = new ExpiringSampleBuffer(nowProvider: () => 0);

        buffer.Stats.Should().Be((0d, 0d, 0d, 0L));
    }

    [TestMethod]
    public void Stats_AveragesMaxAndLast_OverLiveSamples()
    {
        var clock = new FakeClock();
        var buffer = new ExpiringSampleBuffer(nowProvider: () => clock.Now);
        clock.Now = 1000;
        buffer.Record(2);
        buffer.Record(6);
        buffer.Record(4);

        (double last, double average, double max, long count) = buffer.Stats;
        last.Should().Be(4);
        average.Should().Be(4);
        max.Should().Be(6);
        count.Should().Be(3);
    }

    [TestMethod]
    public void Samples_ExpireIndividually_AfterThirtySeconds()
    {
        var clock = new FakeClock();
        var buffer = new ExpiringSampleBuffer(nowProvider: () => clock.Now);

        clock.Now = 0;
        buffer.Record(100);
        clock.Now = 10_000;
        buffer.Record(50);
        clock.Now = 20_000;
        buffer.Record(25);

        // All three still live at 29s.
        clock.Now = 29_000;
        buffer.Stats.SampleCount.Should().Be(3);

        // Just past 30s the oldest sample (t=0) expires; the other two remain.
        clock.Now = 30_001;
        (_, double average, double max, long count) = buffer.Stats;
        count.Should().Be(2);
        average.Should().Be(37.5);
        max.Should().Be(50);

        // Past 40s only the t=20s sample remains.
        clock.Now = 40_001;
        buffer.Stats.SampleCount.Should().Be(1);

        // Past 50s everything has expired.
        clock.Now = 50_001;
        buffer.Stats.SampleCount.Should().Be(0);
    }

    [TestMethod]
    public void Stats_DrainToZero_WhenAllSamplesExpire()
    {
        var clock = new FakeClock();
        var buffer = new ExpiringSampleBuffer(nowProvider: () => clock.Now);

        clock.Now = 0;
        buffer.Record(100);
        clock.Now = 31_000;

        buffer.Stats.Should().Be((0d, 0d, 0d, 0L));
    }

    [TestMethod]
    public void Record_CountCap_DropsOldest()
    {
        var buffer = new ExpiringSampleBuffer(maxSamples: 3, nowProvider: () => 0);

        buffer.Record(1);
        buffer.Record(2);
        buffer.Record(3);
        buffer.Record(4);

        (double last, double average, double max, long count) = buffer.Stats;
        last.Should().Be(4);
        average.Should().Be(3);
        max.Should().Be(4);
        count.Should().Be(3);
    }

    [TestMethod]
    public void Clear_ResetsEverything()
    {
        var buffer = new ExpiringSampleBuffer(nowProvider: () => 0);

        buffer.Record(5);
        buffer.Clear();

        buffer.Stats.Should().Be((0d, 0d, 0d, 0L));
    }
}
