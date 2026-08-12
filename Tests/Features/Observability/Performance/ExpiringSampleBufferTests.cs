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
    public void Samples_ExpireIndividually_AfterTenSeconds()
    {
        var clock = new FakeClock();
        var buffer = new ExpiringSampleBuffer(nowProvider: () => clock.Now);

        clock.Now = 0;
        buffer.Record(100);
        clock.Now = 3_333;
        buffer.Record(50);
        clock.Now = 6_666;
        buffer.Record(25);

        // All three still live at 9.9s.
        clock.Now = 9_999;
        buffer.Stats.SampleCount.Should().Be(3);

        // Just past 10s the oldest sample (t=0) expires; the other two remain.
        clock.Now = 10_001;
        (_, double average, double max, long count) = buffer.Stats;
        count.Should().Be(2);
        average.Should().Be(37.5);
        max.Should().Be(50);

        // Past 13.3s only the t=6.6s sample remains.
        clock.Now = 13_334;
        buffer.Stats.SampleCount.Should().Be(1);

        // Past 16.6s everything has expired.
        clock.Now = 16_667;
        buffer.Stats.SampleCount.Should().Be(0);
    }

    [TestMethod]
    public void Stats_DrainToZero_WhenAllSamplesExpire()
    {
        var clock = new FakeClock();
        var buffer = new ExpiringSampleBuffer(nowProvider: () => clock.Now);

        clock.Now = 0;
        buffer.Record(100);
        clock.Now = 11_000;

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

    [TestMethod]
    public void Average_CoversOnlyTheMostRecentFiftySamples()
    {
        var buffer = new ExpiringSampleBuffer(nowProvider: () => 0);

        for (int i = 0; i < 60; i++)
            buffer.Record(1.0);
        buffer.Record(2.0);

        (double last, double average, double max, long count) = buffer.Stats;
        last.Should().Be(2.0);
        average.Should().BeApproximately(1.02, 0.0001, "49×1.0 + 1×2.0 over the last 50 samples");
        max.Should().Be(2.0, "max still covers the full live window");
        count.Should().Be(61, "sample count is the full live window");
    }
}
