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

        private static int ReadPrivateConstInt(string fieldName)
        {
            FieldInfo? field = typeof(DebugRenderer).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            field.Should().NotBeNull();
            object? value = field!.GetRawConstantValue();
            value.Should().NotBeNull();
            return (int)value!;
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

        [TestMethod]
        public void ResolveDebugColumnForNextSection_DoesNotShift_WhenBelowTwentyEightLines()
        {
            MethodInfo? method = typeof(DebugRenderer).GetMethod("ResolveDebugColumnForNextSection", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            object? result = method!.Invoke(null, [0, 10, 120 + (27 * 18), 120, 18, 28, 4, 10, 405]);
            result.Should().NotBeNull();

            dynamic tuple = result!;
            ((int)tuple.Item1).Should().Be(0);
            ((int)tuple.Item2).Should().Be(10);
            ((int)tuple.Item3).Should().Be(120 + (27 * 18));
        }

        [TestMethod]
        public void ResolveDebugColumnForNextSection_ShiftsByFourHundredFive_WhenAtTwentyEightLines()
        {
            MethodInfo? method = typeof(DebugRenderer).GetMethod("ResolveDebugColumnForNextSection", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            object? result = method!.Invoke(null, [0, 10, 120 + (28 * 18), 120, 18, 28, 4, 10, 405]);
            result.Should().NotBeNull();

            dynamic tuple = result!;
            ((int)tuple.Item1).Should().Be(1);
            ((int)tuple.Item2).Should().Be(415);
            ((int)tuple.Item3).Should().Be(120);
        }

        [TestMethod]
        public void ResolveDebugColumnForNextSection_DoesNotShiftPastFourthColumn()
        {
            MethodInfo? method = typeof(DebugRenderer).GetMethod("ResolveDebugColumnForNextSection", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            int fourthColumnX = 10 + (3 * 405);
            int yOverflow = 120 + (40 * 18);
            object? result = method!.Invoke(null, [3, fourthColumnX, yOverflow, 120, 18, 28, 4, 10, 405]);
            result.Should().NotBeNull();

            dynamic tuple = result!;
            ((int)tuple.Item1).Should().Be(3);
            ((int)tuple.Item2).Should().Be(fourthColumnX);
            ((int)tuple.Item3).Should().Be(yOverflow);
        }

        [TestMethod]
        public void EnqueueWrappedDebugLine_MovesToNextColumn_WhenCurrentColumnIsFull()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var queue = new DeferredTextQueue();
            var renderer = new DebugRenderer(plugin, deferredTextQueue: queue);

            int startY = ReadPrivateConstInt("DetailedDebugStartY");
            int lineHeight = ReadPrivateConstInt("DetailedDebugLineHeight");
            int baseX = ReadPrivateConstInt("DetailedDebugBaseX");
            int linesPerColumn = ReadPrivateConstInt("DetailedDebugLinesPerColumn");
            int columnShiftPx = ReadPrivateConstInt("DetailedDebugColumnShiftPx");

            MethodInfo? method = typeof(DebugRenderer).GetMethod("EnqueueWrappedDebugLine", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            object[] args = [baseX, startY + (linesPerColumn * lineHeight), lineHeight, "Alpha Beta", Color.White, 12, 20];
            object? result = method!.Invoke(renderer, args);

            result.Should().NotBeNull();
            ((int)result!).Should().Be(startY + lineHeight);
            ((int)args[0]).Should().Be(baseX + columnShiftPx);

            var pending = queue.GetPendingTextSnapshot();
            pending.Should().NotBeEmpty();
            pending[0].Should().Contain("Alpha");
        }
    }
}
