using System.Collections.Generic;
using SharpDX;
using Color = SharpDX.Color;

namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        private void EnqueueUltimatumLine(int xPos, ref int yPos, int lineHeight, string text, Color color, int size)
        {
            _deferredTextQueue.Enqueue(text, new Vector2(xPos, yPos), color, size);
            yPos += lineHeight;
        }

        private int RenderUltimatumDebug(ref int xPos, int yPos, int lineHeight)
        {
            EnqueueUltimatumLine(xPos, ref yPos, lineHeight, "--- Ultimatum ---", Color.Orange, 16);

            if (_plugin is not ClickIt clickIt || clickIt.State.ClickService == null)
            {
                EnqueueUltimatumLine(xPos, ref yPos, lineHeight, "Click service unavailable", Color.Gray, 14);
                return yPos + lineHeight;
            }

            var settings = _plugin.Settings;
            EnqueueUltimatumLine(
                xPos,
                ref yPos,
                lineHeight,
                $"Enabled Initial/Other: {settings.IsInitialUltimatumClickEnabled()}/{settings.IsOtherUltimatumClickEnabled()}",
                Color.White,
                13);

            bool hasPreview = clickIt.State.ClickService.TryGetUltimatumOptionPreview(out List<Services.ClickService.UltimatumPanelOptionPreview> previews)
                && previews.Count > 0;

            var snap = clickIt.State.ClickService.GetLatestUltimatumDebug();
            if (!snap.HasData)
            {
                EnqueueUltimatumLine(
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
                EnqueueUltimatumLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Stage: {snap.Stage}  Seq: {snap.Sequence}  Source: {snap.Source}",
                    stageColor,
                    14);

                EnqueueUltimatumLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Panel Visible: {snap.IsPanelVisible}  GG Active: {snap.IsGruelingGauntletActive}",
                    Color.White,
                    13);

                EnqueueUltimatumLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Saturated Choice: {snap.HasSaturatedChoice}  Modifier: {snap.SaturatedModifier}",
                    Color.White,
                    13);

                EnqueueUltimatumLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"TakeReward Match: {snap.ShouldTakeReward}  Action: {snap.Action}",
                    Color.White,
                    13);

                EnqueueUltimatumLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Candidates/Saturated: {snap.CandidateCount}/{snap.SaturatedCandidateCount}",
                    Color.White,
                    13);

                EnqueueUltimatumLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Best Candidate: {snap.BestModifier} (priority={snap.BestPriority})",
                    Color.White,
                    13);

                EnqueueUltimatumLine(
                    xPos,
                    ref yPos,
                    lineHeight,
                    $"Clicks Choice/Confirm/Reward: {snap.ClickedChoice}/{snap.ClickedConfirm}/{snap.ClickedTakeRewards}",
                    Color.White,
                    13);

                yPos = EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Note: {snap.Notes}", Color.LightGray, 13, 78);
            }

            if (hasPreview)
            {
                EnqueueUltimatumLine(xPos, ref yPos, lineHeight, $"Visible Options: {previews.Count}", Color.LightBlue, 13);

                int maxOptions = Math.Min(3, previews.Count);
                for (int i = 0; i < maxOptions; i++)
                {
                    var option = previews[i];
                    string selected = option.IsSelected ? "*" : "-";
                    yPos = EnqueueWrappedDebugLine(
                        ref xPos,
                        yPos,
                        lineHeight,
                        $"{selected} {option.ModifierName} (prio={option.PriorityIndex}) center=({option.Rect.Center.X:0.0},{option.Rect.Center.Y:0.0})",
                        option.IsSelected ? Color.LightGreen : Color.LightGray,
                        12,
                        78);
                }
            }

            var trail = clickIt.State.ClickService.GetLatestUltimatumDebugTrail();
            yPos = RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 10, wrapWidth: 80);

            return yPos;
        }
    }
}