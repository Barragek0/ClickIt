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

        public int RenderPerformanceDebug(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            _deferredTextQueue.Enqueue("--- Performance ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            yPos = RenderFps(xPos, yPos, lineHeight, performanceMonitor.CurrentFPS);
            yPos = RenderMemory(xPos, yPos, lineHeight);
            yPos = RenderRenderTime(xPos, yPos, lineHeight, performanceMonitor);
            yPos = RenderCoroutineTimings(xPos, yPos, lineHeight, performanceMonitor);

            return yPos;
        }

        private int RenderFps(int xPos, int yPos, int lineHeight, double fps)
        {
            Color fpsColor = fps >= FPS_HIGH_THRESHOLD ? Color.LawnGreen : fps >= FPS_MEDIUM_THRESHOLD ? Color.Yellow : Color.Red;
            _deferredTextQueue.Enqueue($"FPS: {fps:F1}", new Vector2(xPos, yPos), fpsColor, 16);
            return yPos + lineHeight;
        }

        private int RenderMemory(int xPos, int yPos, int lineHeight)
        {
            Process process = Process.GetCurrentProcess();
            long memoryUsage = process.WorkingSet64 / 1024 / 1024;
            _deferredTextQueue.Enqueue($"Memory Usage: {memoryUsage} MB", new Vector2(xPos, yPos), Color.Yellow, 16);
            return yPos + lineHeight;
        }

        private int RenderRenderTime(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            var renderTimings = performanceMonitor.RenderTimings;
            if (renderTimings == null || renderTimings.Count == 0)
                return yPos;

            long lastRenderTime = 0;
            long sum = 0;
            long maxRenderTime = long.MinValue;
            int count = 0;
            foreach (long t in renderTimings)
            {
                lastRenderTime = t;
                sum += t;
                if (t > maxRenderTime)
                    maxRenderTime = t;
                count++;
            }

            double avgRenderTime = count > 0 ? (double)sum / count : 0.0;
            Color renderColor = avgRenderTime <= RENDER_TIME_LOW_THRESHOLD
                ? Color.LawnGreen
                : avgRenderTime <= RENDER_TIME_MEDIUM_THRESHOLD
                    ? Color.Yellow
                    : Color.Red;

            _deferredTextQueue.Enqueue($"Render: {lastRenderTime} ms (avg: {avgRenderTime:F2}, max: {maxRenderTime})", new Vector2(xPos, yPos), renderColor, 16);
            return yPos + lineHeight;
        }

        private int RenderCoroutineTimings(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, TimingChannel.Altar, "Altar Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, TimingChannel.Click, "Click Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, TimingChannel.Flare, "Flare Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, TimingChannel.Shrine, "Shrine Coroutine");
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