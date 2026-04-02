using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services.Label.Selection;

namespace ClickIt.Tests.Label
{
    [TestClass]
    public class LabelFilterServiceTests
    {
        [TestMethod]
        public void FilterHarvestLabels_ReturnsEmpty_WhenNullInput()
        {
            HarvestLabelFilter.FilterClickableHarvestLabels(null, _ => true).Should().BeEmpty();
        }
    }
}
