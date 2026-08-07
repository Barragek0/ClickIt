namespace ClickIt.Tests.Shared.Rendering
{
    [TestClass]
    public class OverlayRenderHostTests
    {
        private sealed class FakeOverlay : IOverlay
        {
            public FakeOverlay(RenderSection section, bool enabled = true)
            {
                Section = section;
                Enabled = enabled;
            }

            public string Name => "Fake";
            public RenderSection Section { get; }
            public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.None;
            public TimingChannel? RefreshTimingChannel => null;
            public ProcessingSection ProcessingSection => ProcessingSection.Unknown;
            public bool Enabled { get; }
            public int DrawCount { get; private set; }

            public bool IsEnabled(ClickItSettings settings) => Enabled;

            public void Refresh(OverlayRefreshContext ctx)
            {
            }

            public void Draw(OverlayRenderContext ctx) => DrawCount++;
        }

        private static OverlayRenderContext CreateContext()
            => new(
                new ClickItSettings(),
                GameController: null,
                Graphics: null,
                WindowArea: default,
                Labels: null,
                new DeferredTextQueue(),
                new DeferredFrameQueue(),
                new DeferredDrawQueue());

        [TestMethod]
        public void Render_DisabledOverlay_SkipsDrawAndRecordsNearZeroTiming()
        {
            var host = new OverlayRenderHost();
            var overlay = new FakeOverlay(RenderSection.AltarOverlay, enabled: false);
            host.Register(overlay);
            var perf = new PerformanceMonitor(new ClickItSettings());

            host.Render(CreateContext(), perf);

            overlay.DrawCount.Should().Be(0);
            (double lastMs, double _, double _, long sampleCount) = perf.GetRenderSectionStats(RenderSection.AltarOverlay);
            sampleCount.Should().Be(1);
            lastMs.Should().BeLessThan(10.0);
        }

        [TestMethod]
        public void Render_EnabledOverlay_CallsDrawAndRecordsTiming()
        {
            var host = new OverlayRenderHost();
            var overlay = new FakeOverlay(RenderSection.AltarOverlay);
            host.Register(overlay);
            var perf = new PerformanceMonitor(new ClickItSettings());

            host.Render(CreateContext(), perf);

            overlay.DrawCount.Should().Be(1);
            perf.GetRenderSectionStats(RenderSection.AltarOverlay).SampleCount.Should().Be(1);
        }

        [TestMethod]
        public void Render_MultipleOverlays_EachTimedUnderOwnSection()
        {
            var host = new OverlayRenderHost();
            var altar = new FakeOverlay(RenderSection.AltarOverlay);
            var blight = new FakeOverlay(RenderSection.BlightOverlay);
            host.Register(altar);
            host.Register(blight);
            var perf = new PerformanceMonitor(new ClickItSettings());

            host.Render(CreateContext(), perf);

            altar.DrawCount.Should().Be(1);
            blight.DrawCount.Should().Be(1);
            perf.GetRenderSectionStats(RenderSection.AltarOverlay).SampleCount.Should().Be(1);
            perf.GetRenderSectionStats(RenderSection.BlightOverlay).SampleCount.Should().Be(1);
        }

        [TestMethod]
        public void Render_TwoFrames_AccumulatesSamplesPerSection()
        {
            var host = new OverlayRenderHost();
            var overlay = new FakeOverlay(RenderSection.StrongboxOverlay);
            host.Register(overlay);
            var perf = new PerformanceMonitor(new ClickItSettings());

            host.Render(CreateContext(), perf);
            host.Render(CreateContext(), perf);

            overlay.DrawCount.Should().Be(2);
            perf.GetRenderSectionStats(RenderSection.StrongboxOverlay).SampleCount.Should().Be(2);
        }
    }
}
