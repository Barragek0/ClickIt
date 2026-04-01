using ClickIt.Services.Click.Label;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelClickPointResolutionPolicyTests
    {
        [TestMethod]
        public void ShouldRetryWithoutClickableArea_ReturnsTrue_ForSettlersMechanic()
        {
            bool result = LabelClickPointResolutionPolicy.ShouldRetryWithoutClickableArea("settlers-crimson-iron");

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRetryWithoutClickableArea_ReturnsFalse_ForNonSettlersMechanic()
        {
            bool result = LabelClickPointResolutionPolicy.ShouldRetryWithoutClickableArea("items");

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowSettlersRelaxedFallback_ReturnsTrue_OnlyWhenBackingEntityExistsAndProjectionIsOutsideWindow()
        {
            LabelClickPointResolutionPolicy.ShouldAllowSettlersRelaxedFallback(hasBackingEntity: true, worldProjectionInWindow: false).Should().BeTrue();
            LabelClickPointResolutionPolicy.ShouldAllowSettlersRelaxedFallback(hasBackingEntity: false, worldProjectionInWindow: false).Should().BeFalse();
            LabelClickPointResolutionPolicy.ShouldAllowSettlersRelaxedFallback(hasBackingEntity: true, worldProjectionInWindow: true).Should().BeFalse();
        }
    }
}
