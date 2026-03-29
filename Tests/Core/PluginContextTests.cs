using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class PluginContextTests
    {
        [TestMethod]
        public void Constructor_InitializesExpectedDefaults()
        {
            var state = new PluginContext();

            state.Random.Should().NotBeNull();
            state.LastRenderTimer.Should().NotBeNull();
            state.LastTickTimer.Should().NotBeNull();
            state.Timer.Should().NotBeNull();
            state.SecondTimer.Should().NotBeNull();
            state.LastHotkeyState.Should().BeFalse();
            state.WorkFinished.Should().BeFalse();
            state.PerformanceMonitor.Should().BeNull();
            state.AreaService.Should().BeNull();
            state.Camera.Should().BeNull();
        }

        [TestMethod]
        public void MutableProperties_CanBeSetAndReadBack()
        {
            var state = new PluginContext
            {
                LastHotkeyState = true,
                WorkFinished = true
            };

            state.LastHotkeyState.Should().BeTrue();
            state.WorkFinished.Should().BeTrue();
        }
    }
}