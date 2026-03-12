using System.Reflection;
using ClickIt.Rendering;
using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LazyModeRendererTests
    {
        private static LazyModeRenderer CreateRenderer()
        {
            var settings = new ClickItSettings();
            var performanceMonitor = new PerformanceMonitor(settings);
            var inputHandler = new InputHandler(settings, performanceMonitor);
            return new LazyModeRenderer(settings, new DeferredTextQueue(), inputHandler, null);
        }

        [TestMethod]
        public void GetHoldClickLabelHint_CachesString_PerHotkey()
        {
            var renderer = CreateRenderer();
            MethodInfo? method = typeof(LazyModeRenderer).GetMethod("GetHoldClickLabelHint", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            var first = (string)method!.Invoke(renderer, new object[] { Keys.F1 })!;
            var second = (string)method.Invoke(renderer, new object[] { Keys.F1 })!;
            var third = (string)method.Invoke(renderer, new object[] { Keys.F2 })!;

            first.Should().Be("Hold F1 to click them.");
            second.Should().Be("Hold F1 to click them.");
            third.Should().Be("Hold F2 to click them.");
            ReferenceEquals(first, second).Should().BeTrue();
            ReferenceEquals(first, third).Should().BeFalse();
        }

        [TestMethod]
        public void GetToggleDisableHint_CachesString_PerHotkey()
        {
            var renderer = CreateRenderer();
            MethodInfo? method = typeof(LazyModeRenderer).GetMethod("GetToggleDisableHint", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            var first = (string)method!.Invoke(renderer, new object[] { Keys.F2 })!;
            var second = (string)method.Invoke(renderer, new object[] { Keys.F2 })!;
            var third = (string)method.Invoke(renderer, new object[] { Keys.F3 })!;

            first.Should().Be("Press F2 again to resume lazy clicking.");
            second.Should().Be("Press F2 again to resume lazy clicking.");
            third.Should().Be("Press F3 again to resume lazy clicking.");
            ReferenceEquals(first, second).Should().BeTrue();
            ReferenceEquals(first, third).Should().BeFalse();
        }
    }
}
