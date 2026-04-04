namespace ClickIt.Tests.Features.Altars
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

        [TestMethod]
        public void CollectVisibleAltarLabels_NullCache_ReturnsEmptyAndClearsCounts()
        {
            var debugInfo = new AltarServiceDebugInfo
            {
                LastScanExarchLabels = 5,
                LastScanEaterLabels = 7
            };

            List<LabelOnGround> labels = AltarScanner.CollectVisibleAltarLabels(null, includeExarch: true, includeEater: true, debugInfo);

            labels.Should().BeEmpty();
            debugInfo.LastScanExarchLabels.Should().Be(0);
            debugInfo.LastScanEaterLabels.Should().Be(0);
        }

        [TestMethod]
        public void DetermineAltarType_ReturnsExpectedTypeFromPath()
        {
            AltarScanner.DetermineAltarType("SomePath/CleansingFireAltar/Other").Should().Be(AltarType.SearingExarch);
            AltarScanner.DetermineAltarType("prefix/TangleAltar/suffix").Should().Be(AltarType.EaterOfWorlds);
            AltarScanner.DetermineAltarType(string.Empty).Should().Be(AltarType.Unknown);
        }
    }
}
