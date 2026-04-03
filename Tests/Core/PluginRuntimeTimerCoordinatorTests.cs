using ClickIt.Core.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace ClickIt.Tests.Core
{
    [TestClass]
    public class PluginRuntimeTimerCoordinatorTests
    {
        [TestMethod]
        public void StartAll_StartsAllProvidedTimers()
        {
            var render = new Stopwatch();
            var tick = new Stopwatch();
            var timer = new Stopwatch();
            var second = new Stopwatch();

            PluginRuntimeTimerCoordinator.StartAll(render, tick, timer, second);

            render.IsRunning.Should().BeTrue();
            tick.IsRunning.Should().BeTrue();
            timer.IsRunning.Should().BeTrue();
            second.IsRunning.Should().BeTrue();
        }

        [TestMethod]
        public void StopAll_StopsAllRunningTimers()
        {
            var render = new Stopwatch();
            var tick = new Stopwatch();
            var timer = new Stopwatch();
            var second = new Stopwatch();
            render.Start();
            tick.Start();
            timer.Start();
            second.Start();

            PluginRuntimeTimerCoordinator.StopAll(render, tick, timer, second);

            render.IsRunning.Should().BeFalse();
            tick.IsRunning.Should().BeFalse();
            timer.IsRunning.Should().BeFalse();
            second.IsRunning.Should().BeFalse();
        }
    }
}