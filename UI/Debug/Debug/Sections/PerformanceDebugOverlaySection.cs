using ClickIt.Features.Observability;
using ClickIt.Shared;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using System.Diagnostics;
using Color = SharpDX.Color;

namespace ClickIt.UI.Debug.Sections
{
    internal sealed class PerformanceDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        internal readonly record struct ClickFrequencyTargetDebugMetrics(
            double ClickTargetMs,
            double ProcessingMs,
            double ClickDelayMs,
            double ModeledTotalMs,
            double ObservedTotalMs,
            double SchedulerDeltaMs,
            double TargetDeviationRatio);

        private const double FPS_HIGH_THRESHOLD = 144;
        private const double FPS_MEDIUM_THRESHOLD = 60;
        private const double RENDER_TIME_LOW_THRESHOLD = 6.94;
        private const double RENDER_TIME_MEDIUM_THRESHOLD = 16.67;
        private const double COROUTINE_HIGH_THRESHOLD = 50;
        private const double COROUTINE_MEDIUM_THRESHOLD = 25;
        private const double TARGET_DEVIATION_LOW = 0.05;
        private const double TARGET_DEVIATION_MEDIUM = 0.10;
        private const int MEMORY_SAMPLE_INTERVAL_MS = 500;

        private readonly Debug.DebugOverlayRenderContext _context = context;
        private readonly Stopwatch _memorySampleStopwatch = Stopwatch.StartNew();
        private long _cachedMemoryUsageMb;

        public int RenderPerformanceDebug(int xPos, int yPos, int lineHeight, PerformanceMetricsSnapshot performanceSnapshot)
        {
            _context.DeferredTextQueue.Enqueue("--- Performance ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            yPos = RenderFps(xPos, yPos, lineHeight, (performanceSnapshot.Fps.Current, performanceSnapshot.Fps.Average, performanceSnapshot.Fps.Max));
            yPos = RenderMemory(xPos, yPos, lineHeight);
            yPos = RenderRenderTime(xPos, yPos, lineHeight, performanceSnapshot.Render);
            yPos = RenderRenderSectionBreakdown(xPos, yPos, lineHeight, performanceSnapshot);
            yPos = RenderQueueDepthDebug(xPos, yPos, lineHeight);
            yPos = RenderCoroutineTimings(xPos, yPos, lineHeight, performanceSnapshot);

            return yPos;
        }

        public int RenderClickFrequencyTargetDebug(int xPos, int yPos, int lineHeight, PerformanceMetricsSnapshot performanceSnapshot)
        {
            _context.DeferredTextQueue.Enqueue("--- Click Frequency Target ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            ClickItSettings pluginSettings = _context.Plugin.Settings ?? new ClickItSettings();
            bool lazyModeEnabled = pluginSettings.LazyMode.Value;
            int lazyModeTarget = pluginSettings.LazyModeClickLimiting.Value;
            bool lazyModeDisableKeyHeld = Input.GetKeyState(pluginSettings.LazyModeDisableKey.Value);

            bool hasRestrictedItems = false;
            if (_context.Plugin is ClickIt clickItPlugin)
            {
                var gameController = _context.Plugin.GameController;
                LabelFilterService? labelFilterService = clickItPlugin.State.Services.LabelFilterService;
                InputHandler? inputHandler = clickItPlugin.State.Services.InputHandler;
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

            bool poeActive = _context.Plugin.GameController?.Window?.IsForeground() == true;
            bool lazyModeActive = lazyModeEnabled && !lazyModeDisableKeyHeld && !hasRestrictedItems && poeActive;

            double clickTarget = lazyModeActive ? lazyModeTarget : performanceSnapshot.ClickTargetIntervalMs;
            double avgClickProcessing = performanceSnapshot.ClickCoroutine.AverageMs;
            if (avgClickProcessing <= 0)
            {
                avgClickProcessing = performanceSnapshot.AverageSuccessfulClickTimingMs;
            }

            double observedInterval = performanceSnapshot.AverageClickIntervalMs;
            ClickFrequencyTargetDebugMetrics metrics = BuildClickFrequencyTargetDebugMetrics(clickTarget, avgClickProcessing, observedInterval);
            bool hasObservedInterval = observedInterval > 0;

            double targetDeviation = metrics.TargetDeviationRatio;
            double modeledDeviation = Math.Abs(metrics.ModeledTotalMs - metrics.ClickTargetMs) / Math.Max(1d, metrics.ClickTargetMs);
            string targetStatus = targetDeviation <= TARGET_DEVIATION_MEDIUM
                ? (hasObservedInterval ? "meeting target" : "estimating")
                : "not meeting target";
            Color modeledLineColor = modeledDeviation <= TARGET_DEVIATION_LOW
                ? Color.LawnGreen
                : modeledDeviation <= TARGET_DEVIATION_MEDIUM
                    ? Color.Yellow
                    : Color.Red;
            Color targetLineColor = targetDeviation <= TARGET_DEVIATION_LOW ? Color.LawnGreen : targetDeviation <= TARGET_DEVIATION_MEDIUM ? Color.Yellow : Color.Red;

            string delayStr = $"{metrics.ClickDelayMs:F0}";
            string procStr = $"{metrics.ProcessingMs:F0}";
            string modeledTotalStr = $"{metrics.ModeledTotalMs:F0}";
            string observedStr = $"{metrics.ObservedTotalMs:F0}";
            string schedStr = $"{metrics.SchedulerDeltaMs:+0;-0;0}";
            string settingStr = $"{metrics.ClickTargetMs:F0}";
            int maxLen = Math.Max(
                Math.Max(delayStr.Length, procStr.Length),
                Math.Max(Math.Max(modeledTotalStr.Length, observedStr.Length), Math.Max(schedStr.Length, settingStr.Length)));

            Color procColor = metrics.ProcessingMs > metrics.ClickTargetMs ? Color.Red : metrics.ProcessingMs >= metrics.ClickTargetMs * 0.75 ? Color.Yellow : Color.LawnGreen;
            double absSchedulerDelta = Math.Abs(metrics.SchedulerDeltaMs);
            Color schedulerColor = absSchedulerDelta <= 5 ? Color.LawnGreen : absSchedulerDelta <= 20 ? Color.Yellow : Color.OrangeRed;
            _context.DeferredTextQueue.Enqueue($"Target:      {settingStr.PadLeft(maxLen)} ms {(lazyModeActive ? "(Lazy)" : string.Empty)}", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Click Delay: {delayStr.PadLeft(maxLen)} ms +", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Processing:  {procStr.PadLeft(maxLen)} ms =", new Vector2(xPos, yPos), procColor, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Total:       {modeledTotalStr.PadLeft(maxLen)} ms (model)", new Vector2(xPos, yPos), modeledLineColor, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Scheduler:   {schedStr.PadLeft(maxLen)} ms", new Vector2(xPos, yPos), schedulerColor, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Observed:    {observedStr.PadLeft(maxLen)} ms ({targetStatus})", new Vector2(xPos, yPos), targetLineColor, 16);

            return yPos + lineHeight;
        }

        public int RenderErrorsDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Recent Errors ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_context.Plugin is ClickIt clickItPlugin)
            {
                var recentErrors = clickItPlugin.State.RecentErrors;
                if (recentErrors.Count == 0)
                {
                    _context.DeferredTextQueue.Enqueue("No Recent Errors", new Vector2(xPos, yPos), Color.LightGreen, 16);
                    return yPos + lineHeight;
                }

                _context.DeferredTextQueue.Enqueue($"Error Count: {recentErrors.Count}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;

                for (int i = Math.Max(0, recentErrors.Count - 3); i < recentErrors.Count; i++)
                {
                    string error = recentErrors[i];
                    yPos = _context.RenderWrappedText($"  {error}", new Vector2(xPos, yPos), Color.Red, 14, lineHeight, 50);
                }
            }

            return yPos;
        }

        internal static ClickFrequencyTargetDebugMetrics BuildClickFrequencyTargetDebugMetrics(
            double clickTargetMs,
            double processingMs,
            double observedIntervalMs)
        {
            double safeClickTargetMs = Math.Max(1d, clickTargetMs);
            double safeProcessingMs = Math.Max(0d, processingMs);
            double clickDelayMs = Math.Max(0d, safeClickTargetMs - safeProcessingMs);
            double modeledTotalMs = clickDelayMs + safeProcessingMs;
            double observedTotalMs = observedIntervalMs > 0d ? observedIntervalMs : modeledTotalMs;
            double schedulerDeltaMs = observedTotalMs - modeledTotalMs;
            double targetDeviationRatio = (observedTotalMs - safeClickTargetMs) / safeClickTargetMs;

            return new ClickFrequencyTargetDebugMetrics(
                ClickTargetMs: safeClickTargetMs,
                ProcessingMs: safeProcessingMs,
                ClickDelayMs: clickDelayMs,
                ModeledTotalMs: modeledTotalMs,
                ObservedTotalMs: observedTotalMs,
                SchedulerDeltaMs: schedulerDeltaMs,
                TargetDeviationRatio: targetDeviationRatio);
        }

        private int RenderFps(int xPos, int yPos, int lineHeight, (double Current, double Average, double Max) fpsStats)
        {
            double currentFps = fpsStats.Current;
            Color fpsColor = currentFps >= FPS_HIGH_THRESHOLD ? Color.LawnGreen : currentFps >= FPS_MEDIUM_THRESHOLD ? Color.Yellow : Color.Red;
            _context.DeferredTextQueue.Enqueue($"FPS: {currentFps:F1} (avg: {fpsStats.Average:F1}, max: {fpsStats.Max:F1})", new Vector2(xPos, yPos), fpsColor, 16);
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

            _context.DeferredTextQueue.Enqueue($"Memory Usage: {_cachedMemoryUsageMb} MB", new Vector2(xPos, yPos), Color.Yellow, 16);
            return yPos + lineHeight;
        }

        private int RenderRenderTime(int xPos, int yPos, int lineHeight, TimingMetricsSnapshot stats)
        {
            if (stats.SampleCount == 0)
                return yPos;

            double avgRenderTime = stats.AverageMs;
            Color renderColor = avgRenderTime <= RENDER_TIME_LOW_THRESHOLD
                ? Color.LawnGreen
                : avgRenderTime <= RENDER_TIME_MEDIUM_THRESHOLD
                    ? Color.Yellow
                    : Color.Red;

            _context.DeferredTextQueue.Enqueue($"Render: {stats.LastMs:F0} ms (avg: {avgRenderTime:F2} ms, max: {stats.MaxMs:F0} ms)", new Vector2(xPos, yPos), renderColor, 16);
            return yPos + lineHeight;
        }

        private int RenderRenderSectionBreakdown(int xPos, int yPos, int lineHeight, PerformanceMetricsSnapshot performanceSnapshot)
        {
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceSnapshot.GetRenderSection(RenderSection.LazyMode), "Render.Lazy");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceSnapshot.GetRenderSection(RenderSection.DebugOverlay), "Render.Debug");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceSnapshot.GetRenderSection(RenderSection.AltarOverlay), "Render.Altar");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceSnapshot.GetRenderSection(RenderSection.UltimatumOverlay), "Render.Ultimatum");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceSnapshot.GetRenderSection(RenderSection.StrongboxOverlay), "Render.Strongbox");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceSnapshot.GetRenderSection(RenderSection.PathfindingOverlay), "Render.Pathfinding");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceSnapshot.GetRenderSection(RenderSection.TextFlush), "Flush.Text");
            yPos = RenderSectionLine(xPos, yPos, lineHeight, performanceSnapshot.GetRenderSection(RenderSection.FrameFlush), "Flush.Frame");
            return yPos;
        }

        private int RenderSectionLine(int xPos, int yPos, int lineHeight, TimingMetricsSnapshot stats, string label)
        {
            if (stats.SampleCount == 0)
                return yPos;

            Color color = stats.AverageMs <= RENDER_TIME_LOW_THRESHOLD
                ? Color.LawnGreen
                : stats.AverageMs <= RENDER_TIME_MEDIUM_THRESHOLD
                    ? Color.Yellow
                    : Color.Red;

            _context.DeferredTextQueue.Enqueue($"{label}: {stats.LastMs:F2} ms (avg: {stats.AverageMs:F2}, max: {stats.MaxMs:F2})", new Vector2(xPos, yPos), color, 14);
            return yPos + lineHeight;
        }

        private int RenderQueueDepthDebug(int xPos, int yPos, int lineHeight)
        {
            if (_context.Plugin is not ClickIt clickIt)
                return yPos;

            int pendingText = clickIt.State.Rendering.DeferredTextQueue?.GetPendingCount() ?? 0;
            int pendingFrames = clickIt.State.Rendering.DeferredFrameQueue?.GetPendingCount() ?? 0;
            Color color = (pendingText + pendingFrames) > 200 ? Color.OrangeRed : Color.LightGray;
            _context.DeferredTextQueue.Enqueue($"  Queue: text={pendingText}, frames={pendingFrames}", new Vector2(xPos, yPos), color, 14);
            return yPos + lineHeight;
        }

        private int RenderCoroutineTimings(int xPos, int yPos, int lineHeight, PerformanceMetricsSnapshot performanceSnapshot)
        {
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceSnapshot.GetCoroutineTiming(TimingChannel.Altar), "Altar Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceSnapshot.GetCoroutineTiming(TimingChannel.Click), "Click Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceSnapshot.GetCoroutineTiming(TimingChannel.Flare), "Flare Coroutine");
            return yPos;
        }

        private int RenderCoroutineTiming(int xPos, int yPos, int lineHeight, TimingMetricsSnapshot stats, string label)
        {
            double current = stats.LastMs;
            double avg = stats.AverageMs;
            double max = stats.MaxMs;
            Color color = current >= COROUTINE_HIGH_THRESHOLD ? Color.Red : current >= COROUTINE_MEDIUM_THRESHOLD ? Color.Yellow : Color.LawnGreen;
            _context.DeferredTextQueue.Enqueue($"{label}: {current:F0} ms (avg: {avg:F1}, max: {max:F0})", new Vector2(xPos, yPos), color, 16);
            return yPos + lineHeight;
        }
    }
}