namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class TimedValueCacheTests
    {
        [TestMethod]
        public void TryGet_ReturnsCachedValue_WhenKeyMatchesAndWindowIsFresh()
        {
            var cache = new TimedValueCache<int, string>(50);

            cache.SetValue(7, 1_000, "alpha");

            bool hit = cache.TryGetValue(7, 1_030, out string value);

            hit.Should().BeTrue();
            value.Should().Be("alpha");
        }

        [TestMethod]
        public void TryGet_ReturnsMiss_WhenWindowExpired()
        {
            var cache = new TimedValueCache<int, string>(50);

            cache.SetValue(7, 1_000, "alpha");

            bool hit = cache.TryGetValue(7, 1_051, out _);

            hit.Should().BeFalse();
        }

        [TestMethod]
        public void TryGet_ReturnsMiss_WhenKeyDiffers()
        {
            var cache = new TimedValueCache<int, string>(50);

            cache.SetValue(7, 1_000, "alpha");

            bool hit = cache.TryGetValue(8, 1_020, out _);

            hit.Should().BeFalse();
        }

        [TestMethod]
        public void Clear_RemovesCachedValue()
        {
            var cache = new TimedValueCache<int, string>(50);

            cache.SetValue(7, 1_000, "alpha");
            cache.Invalidate();

            bool hit = cache.TryGetValue(7, 1_001, out _);

            hit.Should().BeFalse();
            cache.HasCachedValue.Should().BeFalse();
        }

        [TestMethod]
        public void TryGet_ReturnsMiss_WhenAgeNegative_AndNonNegativeAgeRequired()
        {
            var cache = new TimedValueCache<int, string>(
                50,
                settings: new TimedValueCacheSettings(RequireNonNegativeAge: true));

            cache.SetValue(7, 1_000, "alpha");

            bool hit = cache.TryGetValue(7, 999, out _);

            hit.Should().BeFalse();
        }

        [TestMethod]
        public void TryGet_UsesProvidedKeyComparer()
        {
            var cache = new TimedValueCache<string, int>(
                50,
                keyComparer: StringComparer.OrdinalIgnoreCase);

            cache.SetValue("alpha", 1_000, 42);

            bool hit = cache.TryGetValue("ALPHA", 1_010, out int value);

            hit.Should().BeTrue();
            value.Should().Be(42);
        }
    }
}