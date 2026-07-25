namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginLazyModeContextCacheTests
    {
        [TestMethod]
        public void GetContext_ReusesRestrictedItemEvaluation_WhileCacheIsFresh()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;

            IReadOnlyList<LabelOnGround> labels = [];
            int restrictedItemCalls = 0;
            long now = 1_000;

            var cache = new PluginLazyModeContextCache(new PluginLazyModeContextCacheDependencies(
                settings,
                GetLabels: () => labels,
                IsRitualActive: static () => false,
                HasLazyModeRestrictedItems: _ =>
                {
                    restrictedItemCalls++;
                    return true;
                },
                GetTimestampMs: () => now));

            PluginLazyModeContextSnapshot first = cache.GetContext(shouldEvaluateRitualState: false, shouldEvaluateRestrictedItems: true);
            PluginLazyModeContextSnapshot second = cache.GetContext(shouldEvaluateRitualState: false, shouldEvaluateRestrictedItems: true);

            first.HasLazyModeRestrictedItems.Should().BeTrue();
            second.HasLazyModeRestrictedItems.Should().BeTrue();
            restrictedItemCalls.Should().Be(1);
        }

        [TestMethod]
        public void GetContext_ReevaluatesRestrictedItems_WhenLabelReferenceChanges()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;

            IReadOnlyList<LabelOnGround> labels = [];
            int restrictedItemCalls = 0;
            long now = 1_000;

            var cache = new PluginLazyModeContextCache(new PluginLazyModeContextCacheDependencies(
                settings,
                GetLabels: () => labels,
                IsRitualActive: static () => false,
                HasLazyModeRestrictedItems: _ =>
                {
                    restrictedItemCalls++;
                    return false;
                },
                GetTimestampMs: () => now));

            _ = cache.GetContext(shouldEvaluateRitualState: false, shouldEvaluateRestrictedItems: true);
            labels = new List<LabelOnGround>();
            _ = cache.GetContext(shouldEvaluateRitualState: false, shouldEvaluateRestrictedItems: true);

            restrictedItemCalls.Should().Be(2);
        }

        [TestMethod]
        public void GetContext_ReevaluatesRitualState_WhenCacheWindowExpires()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;

            int ritualCalls = 0;
            long now = 1_000;

            var cache = new PluginLazyModeContextCache(new PluginLazyModeContextCacheDependencies(
                settings,
                GetLabels: static () => [],
                IsRitualActive: () =>
                {
                    ritualCalls++;
                    return false;
                },
                HasLazyModeRestrictedItems: static _ => false,
                GetTimestampMs: () => now));

            _ = cache.GetContext(shouldEvaluateRitualState: true, shouldEvaluateRestrictedItems: false);
            now += 60;
            _ = cache.GetContext(shouldEvaluateRitualState: true, shouldEvaluateRestrictedItems: false);

            ritualCalls.Should().Be(2);
        }
    }
}