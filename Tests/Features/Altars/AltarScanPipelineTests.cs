namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarScanPipelineTests
    {
        [TestMethod]
        public void ProcessScan_ClearsStore_WhenNoVisibleAltarLabels()
        {
            var store = new AltarComponentStore();
            var component = TestBuilders.BuildPrimary();
            store.Add(component).Should().BeTrue();

            var debugInfo = new AltarServiceDebugInfo();
            var pipeline = new AltarScanPipeline(
                store,
                debugInfo,
                CreateFactory());
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => [], 50);

            pipeline.ProcessScan(cachedLabels, includeExarch: true, includeEater: true);

            store.GetComponentsReadOnly().Should().BeEmpty();
            debugInfo.LastScanExarchLabels.Should().Be(0);
            debugInfo.LastScanEaterLabels.Should().Be(0);
        }

        [TestMethod]
        public void ProcessLabels_RemovesInvalidCachedComponents_BeforeAddingNewOnes()
        {
            var store = new AltarComponentStore();
            var invalidCached = new PrimaryAltarComponent(
                AltarType.SearingExarch,
                null!,
                new AltarButton(null),
                new SecondaryAltarComponent(null, [], []),
                new AltarButton(null));
            store.Add(invalidCached).Should().BeTrue();

            var pipeline = new AltarScanPipeline(store, new AltarServiceDebugInfo(), CreateFactory());

            pipeline.ProcessLabels([]);

            store.GetComponentsReadOnly().Should().BeEmpty();
        }

        private static AltarComponentFactory CreateFactory()
        {
            return new AltarComponentFactory(
                new AltarMatcher(),
                _ => { },
                _ => { },
                (_, _) => { });
        }
    }
}