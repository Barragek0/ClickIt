namespace ClickIt.Tests.Features.Labels.Selection
{
    [TestClass]
    public class HarvestLabelFilterTests
    {
        [TestMethod]
        public void FilterHarvestLabels_ReturnsEmpty_WhenNullInput()
        {
            HarvestLabelFilter.FilterClickableHarvestLabels(null, _ => true).Should().BeEmpty();
        }
    }
}