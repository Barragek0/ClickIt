using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class VisibleMechanicCacheStateTests
    {
        [TestMethod]
        public void TryGetVisibleCandidates_ReusesCachedEntry_WithinCacheWindow()
        {
            var state = new VisibleMechanicCacheState();

            state.StoreVisibleCandidates(now: 100, labelCount: 3, lostShipmentCandidate: null, settlersOreCandidate: null);

            bool reused = state.TryGetVisibleCandidates(
                now: 150,
                labelCount: 3,
                cacheWindowMs: 80,
                isLostShipmentUsable: _ => true,
                isSettlersUsable: _ => true,
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate);

            reused.Should().BeTrue();
            lostShipmentCandidate.Should().BeNull();
            settlersOreCandidate.Should().BeNull();
        }

        [TestMethod]
        public void TryGetVisibleCandidates_DoesNotReuseCachedEntry_WhenValidatorRejectsIt()
        {
            var state = new VisibleMechanicCacheState();

            state.StoreVisibleCandidates(now: 100, labelCount: 3, lostShipmentCandidate: null, settlersOreCandidate: null);

            bool reused = state.TryGetVisibleCandidates(
                now: 150,
                labelCount: 3,
                cacheWindowMs: 80,
                isLostShipmentUsable: _ => false,
                isSettlersUsable: _ => true,
                out _,
                out _);

            reused.Should().BeFalse();
        }

        [TestMethod]
        public void TryGetHiddenFallbackCandidates_ReusesCachedEntry_WithinCacheWindow()
        {
            var state = new VisibleMechanicCacheState();

            state.StoreHiddenFallbackCandidates(now: 200, labelCount: 2, lostShipmentCandidate: null, settlersOreCandidate: null);

            bool reused = state.TryGetHiddenFallbackCandidates(
                now: 250,
                labelCount: 2,
                cacheWindowMs: 80,
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate);

            reused.Should().BeTrue();
            lostShipmentCandidate.Should().BeNull();
            settlersOreCandidate.Should().BeNull();
        }
    }
}