using ExileCore;
using ExileCore.PoEMemory.Elements;
using ClickIt.Services;
using ClickIt.Utils;
using SharpDX;
using System.Diagnostics;
using Color = SharpDX.Color;

#nullable enable

namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {

        private const double FPS_HIGH_THRESHOLD = 144;
        private const double FPS_MEDIUM_THRESHOLD = 60;
        private const double RENDER_TIME_LOW_THRESHOLD = 6.94;
        private const double RENDER_TIME_MEDIUM_THRESHOLD = 16.67;
        private const double COROUTINE_HIGH_THRESHOLD = 50;
        private const double COROUTINE_MEDIUM_THRESHOLD = 25;
        private const double TARGET_DEVIATION_LOW = 0.05;
        private const double TARGET_DEVIATION_MEDIUM = 0.10;
        private const int MEMORY_SAMPLE_INTERVAL_MS = 500;

        private readonly Stopwatch _memorySampleStopwatch = Stopwatch.StartNew();
        private long _cachedMemoryUsageMb;

        public int RenderPerformanceDebug(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            _deferredTextQueue.Enqueue("--- Performance ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            yPos = RenderFps(xPos, yPos, lineHeight, performanceMonitor.GetFpsStats());
            yPos = RenderMemory(xPos, yPos, lineHeight);
            yPos = RenderRenderTime(xPos, yPos, lineHeight, performanceMonitor);
            yPos = RenderRenderSectionBreakdown(xPos, yPos, lineHeight, performanceMonitor);
            yPos = RenderQueueDepthDebug(xPos, yPos, lineHeight);
            yPos = RenderCoroutineTimings(xPos, yPos, lineHeight, performanceMonitor);

            return yPos;
        }

        private int RenderFps(int xPos, int yPos, int lineHeight, (double Current, double Average, double Max) fpsStats)
        {
            double currentFps = fpsStats.Current;
            Color fpsColor = currentFps >= FPS_HIGH_THRESHOLD ? Color.LawnGreen : currentFps >= FPS_MEDIUM_THRESHOLD ? Color.Yellow : Color.Red;
            _deferredTextQueue.Enqueue($"FPS: {currentFps:F1} (avg: {fpsStats.Average:F1}, max: {fpsStats.Max:F1})", new Vector2(xPos, yPos), fpsColor, 16);
            return yPos + lineHeight;
        }

        private int RenderMemory(int xPos, int yPos, int lineHeight)
        {
            if (_memorySampleStopwatch.ElapsedMilliseconds >= MEMORY_SAMPLE_INTERVAL_MS || _cachedMemoryUsageMb == 0)
            {
                Process process = Process.GetCurrentProcess();
                _cachedMemoryUsageMb = process.WorkingSet64 / 1024 / 1024;
                _memorySampleStopwatch.Restart();
            }

            _deferredTextQueue.Enqueue($"Memory Usage: {_cachedMemoryUsageMb} MB", new Vector2(xPos, yPos), Color.Yellow, 16);
            return yPos + lineHeight;
        }

        private int RenderRenderTime(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            var stats = performanceMonitor.GetRenderTimingStats();
            if (stats.SampleCount == 0)
                return yPos;

            double avgRenderTime = stats.AverageMs;
            Color renderColor = avgRenderTime <= RENDER_TIME_LOW_THRESHOLD
                ? Color.LawnGreen
                : avgRenderTime <= RENDER_TIME_MEDIUM_THRESHOLD
                    ? Color.Yellow
                    : Color.Red;

            double avgRenderFps = avgRenderTime > 0 ? 1000.0 / avgRenderTime : 0.0;
            double worstFrameFps = stats.MaxMs > 0 ? 1000.0 / stats.MaxMs : 0.0;

            _deferredTextQueue.Enqueue($"Render: {stats.LastMs} ms (avg: {avgRenderTime:F2} ms {avgRenderFps:F1} FPS, max: {stats.MaxMs} ms {worstFrameFps:F1} FPS)", new Vector2(xPos, yPos), renderColor, 16);
            return yPos + lineHeight;
        }

        private int RenderRenderSectionBreakdown(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceMonitor, RenderSection.LazyMode, "Render.Lazy");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceMonitor, RenderSection.DebugOverlay, "Render.Debug");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceMonitor, RenderSection.AltarOverlay, "Render.Altar");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceMonitor, RenderSection.UltimatumOverlay, "Render.Ultimatum");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceMonitor, RenderSection.StrongboxOverlay, "Render.Strongbox");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceMonitor, RenderSection.TextFlush, "Flush.Text");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceMonitor, RenderSection.FrameFlush, "Flush.Frame");
            return yPos;
        }

        private int RenderSectionLine(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor, RenderSection section, string label)
        {
            var stats = performanceMonitor.GetRenderSectionStats(section);
            if (stats.SampleCount == 0)
                return yPos;

            Color color = stats.AverageMs <= RENDER_TIME_LOW_THRESHOLD
                ? Color.LawnGreen
                : stats.AverageMs <= RENDER_TIME_MEDIUM_THRESHOLD
                    ? Color.Yellow
                    : Color.Red;

            _deferredTextQueue.Enqueue($"{label}: {stats.LastMs:F2} ms (avg: {stats.AverageMs:F2}, max: {stats.MaxMs:F2})", new Vector2(xPos, yPos), color, 14);
            return yPos + lineHeight;
        }

        private int RenderQueueDepthDebug(int xPos, int yPos, int lineHeight)
        {
            if (_plugin is not ClickIt clickIt)
                return yPos;

            int pendingText = clickIt.State.DeferredTextQueue?.GetPendingCount() ?? 0;
            int pendingFrames = clickIt.State.DeferredFrameQueue?.GetPendingCount() ?? 0;
            Color color = (pendingText + pendingFrames) > 200 ? Color.OrangeRed : Color.LightGray;
            _deferredTextQueue.Enqueue($"  Queue: text={pendingText}, frames={pendingFrames}", new Vector2(xPos, yPos), color, 14);
            return yPos + lineHeight;
        }

        private int RenderCoroutineTimings(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, TimingChannel.Altar, "Altar Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, TimingChannel.Click, "Click Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, TimingChannel.Flare, "Flare Coroutine");
            return yPos;
        }

        private int RenderCoroutineTiming(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor, TimingChannel timingType, string label)
        {
            double current = performanceMonitor.GetLastTiming(timingType);
            double avg = performanceMonitor.GetAverageTiming(timingType);
            double max = performanceMonitor.GetMaxTiming(timingType);
            Color color = current >= COROUTINE_HIGH_THRESHOLD ? Color.Red : current >= COROUTINE_MEDIUM_THRESHOLD ? Color.Yellow : Color.LawnGreen;
            _deferredTextQueue.Enqueue($"{label}: {current:F0} ms (avg: {avg:F1}, max: {max:F0})", new Vector2(xPos, yPos), color, 16);
            return yPos + lineHeight;
        }

        private void RenderClickFrequencyTargetDebug(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            _deferredTextQueue.Enqueue("--- Click Frequency Target ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            ClickItSettings pluginSettings = _plugin.Settings;
            bool lazyModeEnabled = pluginSettings.LazyMode.Value;
            int lazyModeTarget = pluginSettings.LazyModeClickLimiting.Value;
            bool lazyModeDisableKeyHeld = Input.GetKeyState(pluginSettings.LazyModeDisableKey.Value);

            bool hasRestrictedItems = false;
            if (_plugin is ClickIt clickItPlugin)
            {
                var gameController = _plugin.GameController;
                LabelFilterService? labelFilterService = clickItPlugin.State.LabelFilterService;
                InputHandler? inputHandler = clickItPlugin.State.InputHandler;
                if (labelFilterService != null)
                {
                    var allLabels = (IReadOnlyList<LabelOnGround>?)gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
                    hasRestrictedItems = labelFilterService.HasLazyModeRestrictedItemsOnScreen(allLabels);
                }

                if (inputHandler != null)
                {
                    lazyModeDisableKeyHeld = inputHandler.IsLazyModeDisableActiveForCurrentInputState();
                }
            }

            bool poeActive = _plugin.GameController?.Window?.IsForeground() == true;
            bool lazyModeActive = lazyModeEnabled && !lazyModeDisableKeyHeld && !hasRestrictedItems && poeActive;

            double clickTarget = lazyModeActive ? lazyModeTarget : performanceMonitor.GetClickTargetInterval();
            double avgClickTime = performanceMonitor.GetAverageSuccessfulClickTiming();
            double effectiveDelay = clickTarget - avgClickTime;
            double expectedTotal = effectiveDelay + avgClickTime;
            double targetDeviation = (expectedTotal - clickTarget) / clickTarget;
            string targetStatus = targetDeviation <= TARGET_DEVIATION_MEDIUM ? "meeting target" : "not meeting target";
            Color targetLineColor = targetDeviation <= TARGET_DEVIATION_LOW ? Color.LawnGreen : targetDeviation <= TARGET_DEVIATION_MEDIUM ? Color.Yellow : Color.Red;

            string delayStr = $"{effectiveDelay:F0}";
            string procStr = $"{avgClickTime:F0}";
            string targetStr = $"{expectedTotal:F0}";
            string settingStr = $"{clickTarget:F0}";
            int maxLen = Math.Max(Math.Max(delayStr.Length, procStr.Length), Math.Max(targetStr.Length, settingStr.Length));

            Color procColor = avgClickTime > clickTarget ? Color.Red : avgClickTime >= clickTarget * 0.75 ? Color.Yellow : Color.LawnGreen;
            _deferredTextQueue.Enqueue($"Target:      {settingStr.PadLeft(maxLen)} ms {(lazyModeActive ? "(Lazy)" : "")}", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Click Delay: {delayStr.PadLeft(maxLen)} ms +", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Processing:  {procStr.PadLeft(maxLen)} ms =", new Vector2(xPos, yPos), procColor, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Total:       {targetStr.PadLeft(maxLen)} ms ({targetStatus})", new Vector2(xPos, yPos), targetLineColor, 16);
        }

        public int RenderErrorsDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue("--- Recent Errors ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_plugin is ClickIt clickItPlugin)
            {
                var recentErrors = clickItPlugin.State.RecentErrors;
                if (recentErrors.Count == 0)
                {
                    _deferredTextQueue.Enqueue("No Recent Errors", new Vector2(xPos, yPos), Color.LightGreen, 16);
                    return yPos + lineHeight;
                }

                _deferredTextQueue.Enqueue($"Error Count: {recentErrors.Count}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;

                for (int i = Math.Max(0, recentErrors.Count - 3); i < recentErrors.Count; i++)
                {
                    string error = recentErrors[i];
                    yPos = RenderWrappedText($"  {error}", new Vector2(xPos, yPos), Color.Red, 14, lineHeight, 50);
                }
            }

            return yPos;
        }
    }
}