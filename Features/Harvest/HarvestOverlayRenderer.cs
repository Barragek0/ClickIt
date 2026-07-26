namespace ClickIt.Features.Harvest;

public sealed class HarvestOverlayRenderer
{
    private readonly HarvestService _harvestService;
    private readonly ClickItSettings _settings;
    private readonly DeferredTextQueue _deferredTextQueue;
    private readonly DeferredFrameQueue _deferredFrameQueue;

    private static readonly Color ChosenHighlightColor = new(0, 255, 0, 255);
    private static readonly Color NotChosenHighlightColor = new(255, 0, 0, 255);
    private static readonly Color EqualEstimateHighlightColor = new(255, 165, 0, 255);

    internal HarvestOverlayRenderer(
        HarvestService harvestService,
        ClickItSettings settings,
        DeferredTextQueue deferredTextQueue,
        DeferredFrameQueue deferredFrameQueue)
    {
        _harvestService = harvestService;
        _settings = settings;
        _deferredTextQueue = deferredTextQueue;
        _deferredFrameQueue = deferredFrameQueue;
    }

    internal void Render()
    {
        if (!_settings.ShowHarvestLifeforceEstimation.Value)
            return;

        IReadOnlyList<HarvestPlotEstimate> estimates = _harvestService.CurrentEstimates;
        if (estimates.Count == 0)
            return;

        bool debug = _settings.DebugShowHarvest.Value;
        LabelOnGround? chosenLabel = _harvestService.GetChosenLabel();

        for (int i = 0; i < estimates.Count; i++)
        {
            HarvestPlotEstimate estimate = estimates[i];
            if (estimate.LabelBounds == RectangleF.Empty || estimate.LabelBounds.IsEmpty)
                continue;

            bool isEqual = _harvestService.CurrentDecision.Outcome == HarvestDecisionOutcome.EqualEstimatesNoClick;
            bool isChosen = !isEqual && chosenLabel != null && ReferenceEquals(estimate.Label, chosenLabel);

            Color highlightColor = isEqual
                ? EqualEstimateHighlightColor
                : isChosen ? ChosenHighlightColor : NotChosenHighlightColor;
            _deferredFrameQueue.Enqueue(
                new RectangleF(
                    estimate.LabelBounds.X,
                    estimate.LabelBounds.Y,
                    estimate.LabelBounds.Width,
                    estimate.LabelBounds.Height),
                highlightColor,
                thickness: 2);

            float textX = estimate.LabelBounds.Right + 4f;
            float textY = estimate.LabelBounds.Y + 2f;
            string estimateText = $"{estimate.EstimatedLifeforce:F1}";
            Color textColor = highlightColor;

            _deferredTextQueue.Enqueue(estimateText, new Vector2(textX, textY), textColor, 14, FontAlign.Left);

            if (debug)
                DrawDebugInfo(estimate, textX, textY + 18f);
        }
    }

    private void DrawDebugInfo(HarvestPlotEstimate estimate, float startX, float startY)
    {
        float y = startY;
        Color debugColor = new(220, 220, 220, 255);

        for (int i = 0; i < estimate.SeedRows.Count; i++)
        {
            HarvestSeedRow row = estimate.SeedRows[i];
            string text = row.RawText;
            if (string.IsNullOrWhiteSpace(text))
                text = $"{row.Quantity}x {row.MonsterType}";

            string debugLine = $"[T{(int)row.Tier + 1}] {text}  (R={row.ColorR} G={row.ColorG} B={row.ColorB})";

            _deferredTextQueue.Enqueue(debugLine, new Vector2(startX, y + 1f), debugColor, 12, FontAlign.Left);
            y += 15f;
        }
    }

}
