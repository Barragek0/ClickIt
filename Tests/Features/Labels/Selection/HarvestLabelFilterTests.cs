using ClickIt.Features.Labels.Selection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Labels.Selection
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