using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarScannerUnitTests
    {
        [TestMethod]
        public void CollectElementsFromLabels_NullOrEmpty_ReturnsEmpty()
        {
            var resultNull = AltarScanner.CollectElementsFromLabels(null);
            resultNull.Should().NotBeNull();
            resultNull.Should().BeEmpty();

            var resultEmpty = AltarScanner.CollectElementsFromLabels([]);
            resultEmpty.Should().NotBeNull();
            resultEmpty.Should().BeEmpty();
        }
    }
}
