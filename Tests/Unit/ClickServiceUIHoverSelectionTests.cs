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
            // three labels: addresses 10, 20, 30. candidate initially index 0; uiHover is address 20 -> pick index 1
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
    }
}
