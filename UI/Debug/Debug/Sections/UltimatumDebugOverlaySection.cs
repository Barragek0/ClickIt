namespace ClickIt.UI.Debug.Sections
{
    internal sealed class UltimatumDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        private readonly Debug.DebugOverlayRenderContext _context = context;

        public int RenderUltimatumDebug(ref int xPos, int yPos, int lineHeight)
        {
            EnqueueLine(xPos, ref yPos, lineHeight, "--- Ultimatum ---", Color.Orange, 16);

            if (_context.Plugin is not ClickIt)
            {
                EnqueueLine(xPos, ref yPos, lineHeight, "Click service unavailable", Color.Gray, 14);
                return yPos + lineHeight;
            }

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            if (!telemetry.Click.ServiceAvailable)
            {
                EnqueueLine(xPos, ref yPos, lineHeight, "Click service unavailable", Color.Gray, 14);
                return yPos + lineHeight;
            }

            EnqueueLine(
                xPos,
                ref yPos,
                lineHeight,
                $"Enabled Initial/Other: {telemetry.Click.Settings.InitialUltimatumClickEnabled}/{telemetry.Click.Settings.OtherUltimatumClickEnabled}",
                Color.White,
                13);

            IReadOnlyList<UltimatumOptionPreviewSnapshot> previews = telemetry.Click.UltimatumOptionPreview;
            bool hasPreview = previews.Count > 0;

            var snap = telemetry.Click.Ultimatum;
            if (!snap.HasData)
            {
                EnqueueLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    hasPreview ? "No click-flow snapshot yet; showing live panel preview." : "No ultimatum debug data yet",
                    hasPreview ? Color.Yellow : Color.Gray,
                    14);

                if (!hasPreview)
                    return yPos + lineHeight;
            }
            else
            {
                Color stageColor = snap.ClickedTakeRewards
                    ? Color.Gold
                    : (snap.ClickedConfirm || snap.ClickedChoice)
                        ? Color.LightGreen
                        : Color.Yellow;
                EnqueueLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Stage: {snap.Stage}  Seq: {snap.Sequence}  Source: {snap.Source}",
                    stageColor,
                    14);

                EnqueueLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Panel Visible: {snap.IsPanelVisible}  GG Active: {snap.IsGruelingGauntletActive}",
                    Color.White,
                    13);

                EnqueueLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Saturated Choice: {snap.HasSaturatedChoice}  Modifier: {snap.SaturatedModifier}",
                    Color.White,
                    13);

                EnqueueLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"TakeReward Match: {snap.ShouldTakeReward}  Action: {snap.Action}",
                    Color.White,
                    13);

                EnqueueLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Candidates/Saturated: {snap.CandidateCount}/{snap.SaturatedCandidateCount}",
                    Color.White,
                    13);

                EnqueueLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Best Candidate: {snap.BestModifier} (priority={snap.BestPriority})",
                    Color.White,
                    13);

                EnqueueLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Clicks Choice/Confirm/Reward: {snap.ClickedChoice}/{snap.ClickedConfirm}/{snap.ClickedTakeRewards}",
                    Color.White,
                    13);

                yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Note: {snap.Notes}", Color.LightGray, 13, 78);
            }

            if (hasPreview)
            {
                EnqueueLine(xPos, ref yPos, lineHeight, $"Visible Options: {previews.Count}", Color.LightBlue, 13);

                int maxOptions = Math.Min(3, previews.Count);
                for (int i = 0; i < maxOptions; i++)
                {
                    var option = previews[i];
                    string selected = option.IsSelected ? "*" : "-";
                    yPos = _context.EnqueueWrappedDebugLine(
                        ref xPos,
                        yPos,
                        lineHeight,
                        $"{selected} {option.ModifierName} (prio={option.PriorityIndex}) center=({option.Rect.Center.X:0.0},{option.Rect.Center.Y:0.0})",
                        option.IsSelected ? Color.LightGreen : Color.LightGray,
                        12,
                        78);
                }
            }

            var trail = telemetry.Click.UltimatumTrail;
            yPos = _context.RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 10, wrapWidth: 80);

            return yPos;
        }

        private void EnqueueLine(int xPos, ref int yPos, int lineHeight, string text, Color color, int size)
        {
            _context.DeferredTextQueue.Enqueue(text, new Vector2(xPos, yPos), color, size);
            yPos += lineHeight;
        }
    }
}