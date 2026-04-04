namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct PathfindingLabelSuppressionEvaluatorDependencies(
        ClickItSettings Settings,
        ClickRuntimeState RuntimeState);

    internal sealed class PathfindingLabelSuppressionEvaluator(PathfindingLabelSuppressionEvaluatorDependencies dependencies)
    {
        private readonly PathfindingLabelSuppressionEvaluatorDependencies _dependencies = dependencies;

        public bool ShouldSuppressLeverClick(LabelOnGround label)
        {
            if (!_dependencies.Settings.LazyMode.Value)
                return false;
            if (!ClickLabelSelectionMath.IsLeverLabel(label))
                return false;

            int cooldownMs = _dependencies.Settings.LazyModeLeverReclickDelay?.Value ?? 1200;
            ulong currentLeverKey = ClickLabelSelectionMath.GetLeverIdentityKey(label);
            long now = Environment.TickCount64;

            return ClickLabelSelectionMath.IsLeverClickSuppressedByCooldown(
                _dependencies.RuntimeState.LastLeverKey,
                _dependencies.RuntimeState.LastLeverClickTimestampMs,
                currentLeverKey,
                now,
                cooldownMs);
        }

        public bool ShouldSuppressPathfindingLabel(LabelOnGround label)
            => ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(
                ShouldSuppressLeverClick(label),
                UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(label));

        public void RecordLeverClick(LabelOnGround label)
        {
            if (!_dependencies.Settings.LazyMode.Value)
                return;
            if (!ClickLabelSelectionMath.IsLeverLabel(label))
                return;

            ulong key = ClickLabelSelectionMath.GetLeverIdentityKey(label);
            if (key == 0)
                return;

            _dependencies.RuntimeState.LastLeverKey = key;
            _dependencies.RuntimeState.LastLeverClickTimestampMs = Environment.TickCount64;
        }
    }
}