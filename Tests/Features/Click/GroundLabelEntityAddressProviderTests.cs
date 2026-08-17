namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class GroundLabelEntityAddressProviderTests
    {
        private static GroundLabelEntityAddressProvider CreateProvider(Func<IList<LabelOnGround>?> getLabels)
            => new(getLabels);

        private static LabelOnGround CreateLabel(Entity item, long address)
            => ClickPipelineScenarioFactory.CreateLabel(
                new RectangleF(100f, 100f, 60f, 20f),
                item,
                address);

        private static Entity CreateItem(long address)
            => EntityProbeFactory.Create(path: "Metadata/MiscellaneousObjects/WorldItem", address: address);

        [TestMethod]
        public void Collect_ReturnsEntityAddresses_ForLabelsWithItems()
        {
            List<LabelOnGround> labels =
            [
                CreateLabel(CreateItem(0x111), 0x2001),
                CreateLabel(CreateItem(0x222), 0x2002),
                CreateLabel(CreateItem(0x333), 0x2003),
            ];
            GroundLabelEntityAddressProvider provider = CreateProvider(() => labels);

            IReadOnlySet<long> addresses = provider.Collect();

            addresses.Should().BeEquivalentTo(new HashSet<long> { 0x111, 0x222, 0x333 });
        }

        [TestMethod]
        public void Collect_ReturnsEmpty_WhenNoLabels()
        {
            GroundLabelEntityAddressProvider provider = CreateProvider(() => null);

            provider.Collect().Should().BeEmpty();
        }

        [TestMethod]
        public void Collect_ReturnsEmpty_WhenLabelsHaveNoItems()
        {
            List<LabelOnGround> labels = [new LabelOnGround()];
            GroundLabelEntityAddressProvider provider = CreateProvider(() => labels);

            provider.Collect().Should().BeEmpty();
        }

        [TestMethod]
        public void Collect_CachesByLabelCount_WithinWindow()
        {
            List<LabelOnGround> labels = [CreateLabel(CreateItem(0x111), 0x2001)];
            GroundLabelEntityAddressProvider provider = CreateProvider(() => labels);

            provider.Collect().Should().BeEquivalentTo(new HashSet<long> { 0x111 });

            labels[0] = CreateLabel(CreateItem(0x999), 0x2009);

            provider.Collect().Should().BeEquivalentTo(new HashSet<long> { 0x111 }, "the same label count within the cache window must serve the cached set");
        }
    }
}
