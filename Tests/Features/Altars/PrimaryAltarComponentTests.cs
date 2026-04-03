namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class PrimaryAltarComponentTests
    {
        [TestMethod]
        public void IsValidCached_ReturnsFalseWhenElementsNull()
        {
            var primary = TestBuilders.BuildPrimary();
            primary.IsValidCached().Should().BeFalse();
        }

        [TestMethod]
        public void GetCachedWeights_CachesAndReturnsWeights()
        {
            var primary = TestBuilders.BuildPrimary();
            int callCount = 0;
            AltarWeights Calculator(PrimaryAltarComponent p)
            {
                callCount++;
                var w = new AltarWeights();
                w.TopUpsideWeight = 10;
                w.TopDownsideWeight = 2;
                return w;
            }

            var first = primary.GetCachedWeights(Calculator);
            var second = primary.GetCachedWeights(Calculator);
            first.Should().NotBeNull();
            second.Should().NotBeNull();
            // Because of caching, calculator should only have been called once
            callCount.Should().Be(1);
        }

        [TestMethod]
        public void GetTopModsRect_ThrowsWhenElementNull()
        {
            var primary = TestBuilders.BuildPrimary();
            Action act = () => primary.GetTopModsRect();
            act.Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        public void GetBottomModsRect_ThrowsWhenElementNull()
        {
            var primary = TestBuilders.BuildPrimary();
            Action act = () => primary.GetBottomModsRect();
            act.Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        public void InvalidateCache_ClearsWeightCache()
        {
            var primary = TestBuilders.BuildPrimary();
            int callCount = 0;

            AltarWeights Calculator(PrimaryAltarComponent component)
            {
                callCount++;
                return new AltarWeights { TopUpsideWeight = component.TopMods?.Upsides.Count ?? 0 };
            }

            _ = primary.GetCachedWeights(Calculator);
            primary.InvalidateCache();
            _ = primary.GetCachedWeights(Calculator);

            callCount.Should().Be(2);
        }

    }
}
