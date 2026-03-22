using ClickIt.Services;
using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ServiceIntegrationBoundaryTests
    {
        [TestMethod]
        public void LabelService_ElementSearchBoundary_DelegatesToLabelUtils()
        {
            var fromService = LabelService.GetElementsByStringContains(null, "anything");
            var fromUtils = LabelUtils.GetElementsByStringContains(null, "anything");

            fromService.Should().NotBeNull();
            fromService.Count.Should().Be(fromUtils.Count);
        }

        [TestMethod]
        public void InputHandler_MouseBlockingBoundary_ReturnsConsistentTuple()
        {
            var settings = new ClickItSettings();
            settings.DisableLazyModeLeftClickHeld.Value = true;
            settings.DisableLazyModeRightClickHeld.Value = true;

            var (left, right, any) = InputHandler.GetMouseButtonBlockingState(
                settings,
                key => key == System.Windows.Forms.Keys.LButton);

            left.Should().BeTrue();
            right.Should().BeFalse();
            any.Should().Be(left || right);
        }
    }
}
