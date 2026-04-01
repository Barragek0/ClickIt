using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class VisibleMechanicCachePolicyTests
    {
        [TestMethod]
        public void ShouldReuseTimedLabelCountCache_ReturnsFalse_WhenCacheNotInitialized()
        {
            VisibleMechanicCachePolicy.ShouldReuseTimedLabelCountCache(
                now: 100,
                cachedAtMs: 0,
                cachedLabelCount: 3,
                currentLabelCount: 3,
                cacheWindowMs: 80).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldReuseTimedLabelCountCache_ReturnsFalse_WhenLabelCountDiffers()
        {
            VisibleMechanicCachePolicy.ShouldReuseTimedLabelCountCache(
                now: 150,
                cachedAtMs: 100,
                cachedLabelCount: 3,
                currentLabelCount: 4,
                cacheWindowMs: 80).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldReuseTimedLabelCountCache_ReturnsTrue_OnlyInsideWindow()
        {
            VisibleMechanicCachePolicy.ShouldReuseTimedLabelCountCache(
                now: 150,
                cachedAtMs: 100,
                cachedLabelCount: 3,
                currentLabelCount: 3,
                cacheWindowMs: 80).Should().BeTrue();

            VisibleMechanicCachePolicy.ShouldReuseTimedLabelCountCache(
                now: 190,
                cachedAtMs: 100,
                cachedLabelCount: 3,
                currentLabelCount: 3,
                cacheWindowMs: 80).Should().BeFalse();
        }
    }
}