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
            perf.CompleteRenderSectionFrame();

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
            perf.CompleteRenderSectionFrame();

            overlay.DrawCount.Should().Be(1);
            perf.GetRenderSectionStats(RenderSection.AltarOverlay).SampleCount.Should().Be(1);
        }

        [TestMethod]
        public void Render_StampsCurrentSectionOnQueues_DuringEachOverlayDraw()
        {
            var host = new OverlayRenderHost();
            host.Register(new EnqueueingOverlay(RenderSection.BlightOverlay, DeferredDrawKind.Line, blight: true));
            host.Register(new EnqueueingOverlay(RenderSection.AltarOverlay, DeferredDrawKind.Line, blight: false));
            var perf = new PerformanceMonitor(new ClickItSettings());
            OverlayRenderContext ctx = CreateContext();

            host.Render(ctx, perf);

            // Flush the draw queue the way PluginRenderHost does: per-section attribution records
            // each feature's own draw cost into its render section.
            var reported = new List<(RenderSection Section, double Ms)>();
            ctx.DrawQueue.Flush((Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics)), (section, ms) => reported.Add((section, ms)));

            reported.Should().Contain(entry => entry.Section == RenderSection.BlightOverlay);
            reported.Should().Contain(entry => entry.Section == RenderSection.AltarOverlay);
        }

        private sealed class EnqueueingOverlay : IOverlay
        {
            private readonly bool _blight;

            public EnqueueingOverlay(RenderSection section, DeferredDrawKind kind, bool blight)
            {
                Section = section;
                _blight = blight;
            }

            public string Name => "Enqueueing";
            public RenderSection Section { get; }
            public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.None;
            public TimingChannel? RefreshTimingChannel => null;
            public ProcessingSection ProcessingSection => ProcessingSection.Unknown;

            public bool IsEnabled(ClickItSettings settings) => true;

            public void Refresh(OverlayRefreshContext ctx)
            {
            }

            public void Draw(OverlayRenderContext ctx)
            {
                if (_blight)
                    ctx.DrawQueue.EnqueueLine(new NumVector2(1, 2), new NumVector2(3, 4), 2, Color.Red);
                else
                    ctx.DrawQueue.EnqueueLine(new NumVector2(5, 6), new NumVector2(7, 8), 2, Color.Blue);
            }
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
            perf.CompleteRenderSectionFrame();

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
            perf.CompleteRenderSectionFrame();
            host.Render(CreateContext(), perf);
            perf.CompleteRenderSectionFrame();

            overlay.DrawCount.Should().Be(2);
            perf.GetRenderSectionStats(RenderSection.StrongboxOverlay).SampleCount.Should().Be(2);
        }
    }
}
