namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumPanelChoiceSelectorTests
    {
        [TestMethod]
        public void TryGetBest_SelectsLowestPriorityCandidate()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "A", 7, false),
                new UltimatumPanelChoiceCandidate(null!, "B", 2, false),
                new UltimatumPanelChoiceCandidate(null!, "C", 4, false)
            };

            bool ok = UltimatumPanelChoiceSelector.TryGetBest(candidates, out UltimatumPanelChoiceCandidate best);

            ok.Should().BeTrue();
            best.ModifierName.Should().Be("B");
            best.PriorityIndex.Should().Be(2);
        }

        [TestMethod]
        public void TryGetFirstSaturated_ReturnsFirstSaturatedCandidate()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "A", 1, false),
                new UltimatumPanelChoiceCandidate(null!, "B", 5, true),
                new UltimatumPanelChoiceCandidate(null!, "C", 0, true)
            };

            bool ok = UltimatumPanelChoiceSelector.TryGetFirstSaturated(candidates, out UltimatumPanelChoiceCandidate saturated);

            ok.Should().BeTrue();
            saturated.ModifierName.Should().Be("B");
        }

        [TestMethod]
        public void TryGetSelected_PrefersSaturatedWhenGruelingGauntletIsActive()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "A", 1, false),
                new UltimatumPanelChoiceCandidate(null!, "B", 9, true)
            };

            bool ok = UltimatumPanelChoiceSelector.TryGetSelected(candidates, isGruelingGauntletActive: true, out UltimatumPanelChoiceCandidate selected);

            ok.Should().BeTrue();
            selected.ModifierName.Should().Be("B");
        }
    }
}