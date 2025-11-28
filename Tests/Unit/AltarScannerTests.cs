using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarScannerTests
    {
        [TestMethod]
        public void CollectElementsFromLabels_ReturnsEmpty_WhenNullInput()
        {
            var result = AltarScanner.CollectElementsFromLabels(null);
            result.Should().BeEmpty();
        }

        [TestMethod]
        public void CollectElementsFromLabels_ReturnsEmpty_WhenEmptyList()
        {
            var result = AltarScanner.CollectElementsFromLabels([]);
            result.Should().BeEmpty();
        }
    }
}
