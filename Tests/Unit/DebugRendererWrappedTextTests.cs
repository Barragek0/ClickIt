using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using ClickIt.Rendering;
using ClickIt.Utils;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DebugRendererWrappedTextTests
    {
        private static object? InvokePrivateStatic(string methodName, params object[] args)
        {
            MethodInfo? mi = typeof(DebugRenderer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();
            return mi!.Invoke(null, args);
        }

        [TestMethod]
        public void RenderWrappedText_SplitsIntoExpectedLineCount_AndAdvancesY()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var queue = new DeferredTextQueue();
            var renderer = new DebugRenderer(plugin, deferredTextQueue: queue);

            int resultY = renderer.RenderWrappedText("  one two three four", new Vector2(10, 20), Color.White, 12, 10, 6);

            queue.GetPendingCount().Should().Be(4);
            resultY.Should().Be(60);
        }

        [TestMethod]
        public void RenderWrappedText_EmptyString_ReturnsSingleLineAdvance_AndDoesNotEnqueue()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var queue = new DeferredTextQueue();
            var renderer = new DebugRenderer(plugin, deferredTextQueue: queue);

            int resultY = renderer.RenderWrappedText(string.Empty, new Vector2(10, 25), Color.White, 12, 15, 10);

            queue.GetPendingCount().Should().Be(0);
            resultY.Should().Be(40);
        }

        [TestMethod]
        public void IsCursorInsideWindow_ReturnsFalse_WhenOutsideWindow()
        {
            var windowRect = new RectangleF(100, 200, 300, 150);

            bool inside = (bool)InvokePrivateStatic("IsCursorInsideWindow", windowRect, 50, 50)!;
            bool outside = (bool)InvokePrivateStatic("IsCursorInsideWindow", windowRect, 120, 220)!;

            inside.Should().BeFalse();
            outside.Should().BeTrue();
        }

        [TestMethod]
        public void IsCursorOverLabelRect_AccountsForWindowOffset()
        {
            var labelRect = new RectangleF(10, 20, 50, 40);
            var windowRect = new RectangleF(100, 200, 800, 600);

            bool hit = (bool)InvokePrivateStatic("IsCursorOverLabelRect", labelRect, windowRect, 130, 240)!;
            bool miss = (bool)InvokePrivateStatic("IsCursorOverLabelRect", labelRect, windowRect, 90, 190)!;

            hit.Should().BeTrue();
            miss.Should().BeFalse();
        }

        [TestMethod]
        public void RenderGameStateDebug_Works_WhenGameControllerAndCacheUnavailable()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            plugin.State.CachedLabels = null;

            var queue = new DeferredTextQueue();
            var renderer = new DebugRenderer(plugin, deferredTextQueue: queue);

            int resultY = renderer.RenderGameStateDebug(10, 20, 10);

            resultY.Should().BeGreaterThan(20);
            queue.GetPendingCount().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void RenderDetailedDebugInfo_DoesNotQueueText_WhenAllDetailedSectionsDisabled()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            plugin.__Test_SetSettings(settings);

            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowRecentErrors.Value = false;

            var queue = new DeferredTextQueue();
            var renderer = new DebugRenderer(plugin, deferredTextQueue: queue);
            var monitor = new PerformanceMonitor(settings);

            renderer.RenderDetailedDebugInfo(settings, monitor);

            queue.GetPendingCount().Should().Be(0);
        }
    }
}
