using System.Reflection;
using ClickIt.Rendering;
using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using System.Collections.Generic;

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

            first.Should().Be("Hold F1 to override.");
            second.Should().Be("Hold F1 to override.");
            third.Should().Be("Hold F2 to override.");
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

        [TestMethod]
        public void GetLazyModeRestrictionDisplayReason_UsesFallback_WhenNullOrWhitespace()
        {
            MethodInfo? method = typeof(LazyModeRenderer).GetMethod("GetLazyModeRestrictionDisplayReason", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var nullResult = (string)method!.Invoke(null, new object?[] { null })!;
            var whitespaceResult = (string)method.Invoke(null, new object?[] { "  " })!;

            nullResult.Should().Be("Lazy mode blocking condition detected.");
            whitespaceResult.Should().Be("Lazy mode blocking condition detected.");
        }

        [TestMethod]
        public void WrapOverlayText_WrapsContinuously_ForAllLinesBeyondLimit()
        {
            MethodInfo? method = typeof(LazyModeRenderer).GetMethod("WrapOverlayText", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            string text = "one two three four five six seven eight nine ten eleven twelve thirteen";
            var result = (List<string>)method!.Invoke(null, new object?[] { text, 10 })!;

            result.Count.Should().BeGreaterThan(2);
            result.Should().OnlyContain(line => line.Length <= 10);
        }

        [TestMethod]
        public void WrapOverlayText_DoesNotInsertEmptyLines_WhenInputContainsBlankLines()
        {
            MethodInfo? method = typeof(LazyModeRenderer).GetMethod("WrapOverlayText", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            string text = "first line\n\nsecond line that wraps";
            var result = (List<string>)method!.Invoke(null, new object?[] { text, 12 })!;

            result.Should().OnlyContain(line => !string.IsNullOrWhiteSpace(line));
            result[0].Should().Be("first line");
        }
    }
}
