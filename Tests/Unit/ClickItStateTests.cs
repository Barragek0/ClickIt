using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class PluginContextSanityTests
    {
        [TestMethod]
        public void PluginContext_DefaultsProvideSafeDefaults()
        {
            var ctx = new PluginContext();

            ctx.Random.Should().NotBeNull();
            ctx.LastRenderTimer.Should().NotBeNull();
            ctx.Timer.Should().NotBeNull();
            ctx.WorkFinished.Should().BeFalse();
            ctx.LastHotkeyState.Should().BeFalse();
            ctx.RecentErrors.Should().NotBeNull();
            ctx.RenderTimings.Should().NotBeNull();
        }
    }
}
