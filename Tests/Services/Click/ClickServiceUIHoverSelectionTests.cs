using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceUIHoverSelectionTests
    {
        [TestMethod]
        public void ChooseLabelIndexByUIHoverForTests_PicksHoveredLabel_WhenAddressMatchesDifferentLabel()
        {
            var labels = new ulong[] { 10UL, 20UL, 30UL };
            var chosen = Services.ClickServiceSeams.ChooseLabelIndexByUIHoverForTests(labels, 0, 20UL);
            chosen.Should().Be(1);
        }

        [TestMethod]
        public void ChooseLabelIndexByUIHoverForTests_StaysWithCandidate_WhenUIHoverDoesNotMatch()
        {
            var labels = new ulong[] { 100UL, 200UL, 300UL };
            var chosen = Services.ClickServiceSeams.ChooseLabelIndexByUIHoverForTests(labels, 2, 999UL);
            chosen.Should().Be(2);
        }

        [TestMethod]
        public void ChooseLabelIndexByUIHoverForTests_HandlesNullUIHover_ReturnsCandidate()
        {
            var labels = new ulong[] { 5UL, 6UL };
            var chosen = Services.ClickServiceSeams.ChooseLabelIndexByUIHoverForTests(labels, 1, 0UL);
            chosen.Should().Be(1);
        }

        [TestMethod]
        public void ShouldPreferHoveredEssenceLabel_ReturnsTrue_WhenHoveredEssenceOverlapsAndDiffers()
        {
            bool result = Services.ClickService.ShouldPreferHoveredEssenceLabel(
                hoveredIsEssence: true,
                hoveredHasOverlappingEssence: true,
                nextIsEssence: false,
                hoveredDiffersFromNext: true);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferHoveredEssenceLabel_ReturnsTrue_WhenBothAreEssenceAndHoveredDiffers()
        {
            bool result = Services.ClickService.ShouldPreferHoveredEssenceLabel(
                hoveredIsEssence: true,
                hoveredHasOverlappingEssence: false,
                nextIsEssence: true,
                hoveredDiffersFromNext: true);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferHoveredEssenceLabel_ReturnsFalse_WhenHoveredIsNotEssence()
        {
            bool result = Services.ClickService.ShouldPreferHoveredEssenceLabel(
                hoveredIsEssence: false,
                hoveredHasOverlappingEssence: true,
                nextIsEssence: true,
                hoveredDiffersFromNext: true);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferHoveredEssenceLabel_ReturnsFalse_WhenHoveredIsSameAsNext()
        {
            bool result = Services.ClickService.ShouldPreferHoveredEssenceLabel(
                hoveredIsEssence: true,
                hoveredHasOverlappingEssence: true,
                nextIsEssence: true,
                hoveredDiffersFromNext: false);

            result.Should().BeFalse();
        }
    }
}
