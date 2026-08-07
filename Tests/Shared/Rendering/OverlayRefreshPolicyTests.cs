namespace ClickIt.Tests.Shared.Rendering
{
    [TestClass]
    public class OverlayRefreshPolicyTests
    {
        [TestMethod]
        public void None_HasNoInterval()
        {
            OverlayRefreshPolicy.None.Mode.Should().Be(OverlayRefreshMode.None);
            OverlayRefreshPolicy.None.IntervalMs.Should().Be(0);
        }

        [TestMethod]
        public void Throttled_SetsModeAndInterval()
        {
            var policy = OverlayRefreshPolicy.Throttled(200);

            policy.Mode.Should().Be(OverlayRefreshMode.Throttled);
            policy.IntervalMs.Should().Be(200);
        }

        [TestMethod]
        public void DirtyTracked_SetsModeAndInterval()
        {
            var policy = OverlayRefreshPolicy.DirtyTracked(50);

            policy.Mode.Should().Be(OverlayRefreshMode.DirtyTracked);
            policy.IntervalMs.Should().Be(50);
        }
    }
}
