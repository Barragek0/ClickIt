using ClickIt.Services.Observability;

namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        internal readonly record struct ClickFrequencyTargetDebugMetrics(
            double ClickTargetMs,
            double ProcessingMs,
            double ClickDelayMs,
            double ModeledTotalMs,
            double ObservedTotalMs,
            double SchedulerDeltaMs,
            double TargetDeviationRatio);

        internal static ClickFrequencyTargetDebugMetrics BuildClickFrequencyTargetDebugMetrics(
            double clickTargetMs,
            double processingMs,
            double observedIntervalMs)
        {
            var metrics = Debug.Sections.PerformanceDebugOverlaySection.BuildClickFrequencyTargetDebugMetrics(
                clickTargetMs,
                processingMs,
                observedIntervalMs);
            return new ClickFrequencyTargetDebugMetrics(
                ClickTargetMs: metrics.ClickTargetMs,
                ProcessingMs: metrics.ProcessingMs,
                ClickDelayMs: metrics.ClickDelayMs,
                ModeledTotalMs: metrics.ModeledTotalMs,
                ObservedTotalMs: metrics.ObservedTotalMs,
                SchedulerDeltaMs: metrics.SchedulerDeltaMs,
                TargetDeviationRatio: metrics.TargetDeviationRatio);
        }

        public int RenderErrorsDebug(int xPos, int yPos, int lineHeight)
            => _performanceDebugOverlaySection.RenderErrorsDebug(xPos, yPos, lineHeight);
    }
}
