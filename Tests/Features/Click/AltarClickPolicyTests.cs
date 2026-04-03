using ClickIt.Features.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class AltarClickPolicyTests
    {
        [TestMethod]
        public void ShouldEvaluateAltarScan_ReturnsTrue_WhenEitherAltarTypeEnabled()
        {
            AltarClickPolicy.ShouldEvaluateAltarScan(clickEaterEnabled: true, clickExarchEnabled: false).Should().BeTrue();
            AltarClickPolicy.ShouldEvaluateAltarScan(clickEaterEnabled: false, clickExarchEnabled: true).Should().BeTrue();
            AltarClickPolicy.ShouldEvaluateAltarScan(clickEaterEnabled: true, clickExarchEnabled: true).Should().BeTrue();
            AltarClickPolicy.ShouldEvaluateAltarScan(clickEaterEnabled: false, clickExarchEnabled: false).Should().BeFalse();
        }

        [TestMethod]
        public void AreBothAltarOptionsActionable_RequiresBothVisibleAndClickable()
        {
            AltarClickPolicy.AreBothAltarOptionsActionable(topVisibleAndClickable: true, bottomVisibleAndClickable: true).Should().BeTrue();
            AltarClickPolicy.AreBothAltarOptionsActionable(topVisibleAndClickable: true, bottomVisibleAndClickable: false).Should().BeFalse();
            AltarClickPolicy.AreBothAltarOptionsActionable(topVisibleAndClickable: false, bottomVisibleAndClickable: true).Should().BeFalse();
            AltarClickPolicy.AreBothAltarOptionsActionable(topVisibleAndClickable: false, bottomVisibleAndClickable: false).Should().BeFalse();
        }
    }
}