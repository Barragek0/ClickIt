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

        [TestMethod]
        public void ProcessScan_SkipsRescan_WhenLabelSetReferenceIsUnchanged()
        {
            var store = new AltarComponentStore();
            var debugInfo = new AltarServiceDebugInfo();
            var pipeline = new AltarScanPipeline(store, debugInfo, CreateFactory());

            // A TimeCache factory returning the SAME list instance models the read model's stable-instance behavior: unchanged visible label set -> same reference.
            var stableLabels = new List<LabelOnGround> { new() };
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => stableLabels, 50);

            pipeline.ProcessScan(cachedLabels, includeExarch: true, includeEater: true);
            // First scan: no altar labels -> store cleared and the reference is remembered.
            store.Add(TestBuilders.BuildPrimary()).Should().BeTrue();

            pipeline.ProcessScan(cachedLabels, includeExarch: true, includeEater: true);
            // Second scan: same label reference AND the store is populated -> the element walk is skipped, so the store is not cleared by an unchanged no-altar-label set.
            store.GetComponentsReadOnly().Should().NotBeEmpty(
                "an unchanged label set must not clear the warmed store");
        }

        [TestMethod]
        public void ProcessScan_RepopulatesStore_WhenClearedExternallyDespiteUnchangedLabels()
        {
            var store = new AltarComponentStore();
            var debugInfo = new AltarServiceDebugInfo();
            var pipeline = new AltarScanPipeline(store, debugInfo, CreateFactory());

            var stableLabels = new List<LabelOnGround> { new() };
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => stableLabels, 50);

            pipeline.ProcessScan(cachedLabels, includeExarch: true, includeEater: true);
            store.Add(TestBuilders.BuildPrimary()).Should().BeTrue();

            // External clear: the store is emptied while the label set is unchanged. The gate must NOT skip (store count == 0) so the store gets re-evaluated.
            store.Clear();
            pipeline.ProcessScan(cachedLabels, includeExarch: true, includeEater: true);
            // No altar labels in the stable set -> the re-run clears again (empty result), but the important part is the gate did not short-circuit past the altar-label evaluation.
            store.GetComponentsReadOnly().Should().BeEmpty();
            debugInfo.LastScanExarchLabels.Should().Be(0);
            debugInfo.LastScanEaterLabels.Should().Be(0);
        }

        [TestMethod]
        public void ProcessScan_SkipsRescan_WhenLastScanFoundNoAltarsAndLabelSetUnchanged()
        {
            var store = new AltarComponentStore();
            var debugInfo = new AltarServiceDebugInfo();
            var pipeline = new AltarScanPipeline(store, debugInfo, CreateFactory());

            var stableLabels = new List<LabelOnGround> { new() };
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => stableLabels, 50);

            pipeline.ProcessScan(cachedLabels, includeExarch: true, includeEater: true);
            debugInfo.LastScanExarchLabels.Should().Be(0);

            // Sentinel: a re-scan of the unchanged no-altar set would reset this via the label walk.
            debugInfo.LastScanExarchLabels = 99;
            pipeline.ProcessScan(cachedLabels, includeExarch: true, includeEater: true);

            debugInfo.LastScanExarchLabels.Should().Be(99,
                "an unchanged label set whose last scan found no altars must skip the element walk");
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
