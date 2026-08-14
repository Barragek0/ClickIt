namespace ClickIt.Tests.Features.Click.Runtime;

[TestClass]
public class ClickPipelineTimingTests
{
    [TestMethod]
    public void ReadSleepTimeMs_ReturnsAccumulatedSleep_WithoutConsuming()
    {
        // ReadSleepTimeMs is the probe used by per-stage performance measurements to subtract the safety
        // sleeps that happened inside a stage; it must NOT reset the accumulator, so the host's
        // ConsumeSleepTimeMs() still accounts for the full per-tick total afterwards.
        ClickPipelineTiming.ResetSleepTime();

        ClickPipelineTiming.Sleep(15);
        ClickPipelineTiming.Sleep(5);

        double read = ClickPipelineTiming.ReadSleepTimeMs();
        read.Should().BeApproximately(20.0, 0.5, "the probe returns the accumulated sleep without resetting");

        double afterRead = ClickPipelineTiming.ReadSleepTimeMs();
        afterRead.Should().BeApproximately(20.0, 0.5, "reading again does not consume");

        double consumed = ClickPipelineTiming.ConsumeSleepTimeMs();
        consumed.Should().BeApproximately(20.0, 0.5, "the host still consumes the full total after probes");
        ClickPipelineTiming.ReadSleepTimeMs().Should().BeApproximately(0.0, 0.01, "consume resets for the next tick");
    }

    [TestMethod]
    public void Sleep_DoesNotAccumulateZeroOrNegativeDelays()
    {
        ClickPipelineTiming.ResetSleepTime();

        ClickPipelineTiming.Sleep(0);
        ClickPipelineTiming.Sleep(-3);

        ClickPipelineTiming.ReadSleepTimeMs().Should().Be(0, "non-positive delays must not inflate the sleep total");
    }
}
