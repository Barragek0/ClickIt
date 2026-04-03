using ClickIt.Features.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumGroundOptionSelectorTests
    {
        [TestMethod]
        public void TryGetBest_SelectsLowestPriorityCandidate()
        {
            var candidates = new[]
            {
                new UltimatumGroundOptionCandidate(null!, "A", 6, false),
                new UltimatumGroundOptionCandidate(null!, "B", 2, false),
                new UltimatumGroundOptionCandidate(null!, "C", 4, false)
            };

            bool ok = UltimatumGroundOptionSelector.TryGetBest(candidates, out UltimatumGroundOptionCandidate best);

            ok.Should().BeTrue();
            best.ModifierName.Should().Be("B");
            best.PriorityIndex.Should().Be(2);
        }

        [TestMethod]
        public void TryGetFirstSaturated_ReturnsFirstSaturatedCandidate()
        {
            var candidates = new[]
            {
                new UltimatumGroundOptionCandidate(null!, "A", 1, false),
                new UltimatumGroundOptionCandidate(null!, "B", 9, true),
                new UltimatumGroundOptionCandidate(null!, "C", 0, true)
            };

            bool ok = UltimatumGroundOptionSelector.TryGetFirstSaturated(candidates, out UltimatumGroundOptionCandidate saturated);

            ok.Should().BeTrue();
            saturated.ModifierName.Should().Be("B");
        }

        [TestMethod]
        public void TryGetSelected_PrefersSaturatedWhenGruelingGauntletIsActive()
        {
            var candidates = new[]
            {
                new UltimatumGroundOptionCandidate(null!, "A", 1, false),
                new UltimatumGroundOptionCandidate(null!, "B", 8, true)
            };

            bool ok = UltimatumGroundOptionSelector.TryGetSelected(candidates, isGruelingGauntletActive: true, out UltimatumGroundOptionCandidate selected);

            ok.Should().BeTrue();
            selected.ModifierName.Should().Be("B");
        }
    }
}