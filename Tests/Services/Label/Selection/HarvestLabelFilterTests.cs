using ClickIt.Services.Label.Selection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Label.Selection
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