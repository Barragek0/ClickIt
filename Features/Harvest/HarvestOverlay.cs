namespace ClickIt.Features.Harvest
{
    /// <summary>
    /// Owns the harvest lifeforce-estimation overlay: refresh recomputes the plot estimates on the
    /// host coroutine; Draw renders the cached estimates each frame with fresh label bounds.
    /// </summary>
    public sealed class HarvestOverlay : IOverlay
    {
        private const int HarvestRefreshIntervalMs = 50;

        private static readonly Color ChosenHighlightColor = new(0, 255, 0, 255);
        private static readonly Color NotChosenHighlightColor = new(255, 0, 0, 255);
        private static readonly Color EqualEstimateHighlightColor = new(255, 165, 0, 255);

        private readonly HarvestService _harvestService;

        public HarvestOverlay(HarvestService harvestService)
        {
            _harvestService = harvestService;
        }

        public string Name => "Harvest";

        public RenderSection Section => RenderSection.HarvestOverlay;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.Throttled(HarvestRefreshIntervalMs);

        public TimingChannel? RefreshTimingChannel => TimingChannel.LabelOverlay;

        public ProcessingSection ProcessingSection => ProcessingSection.Harvest;

        public bool IsEnabled(ClickItSettings settings)
            => settings.ClickHarvest.Value;

        public void Refresh(OverlayRefreshContext ctx)
            => _harvestService.ProcessHarvestPlots(ctx.Labels, ctx.GameController);

        public void Draw(OverlayRenderContext ctx)
        {
            if (!ctx.Settings.ShowHarvestLifeforceEstimation.Value)
                return;

            IReadOnlyList<HarvestPlotEstimate> estimates = _harvestService.CurrentEstimates;
            if (estimates.Count == 0)
                return;

            bool debug = ctx.Settings.DebugShowHarvest.Value;
            LabelOnGround? chosenLabel = _harvestService.GetChosenLabel();

            for (int i = 0; i < estimates.Count; i++)
            {
                HarvestPlotEstimate estimate = estimates[i];

                RectangleF labelBounds = HarvestService.ResolveLabelBounds(estimate.Label);
                if (labelBounds == RectangleF.Empty || labelBounds.IsEmpty)
                    continue;

                bool isEqual = _harvestService.CurrentDecision.Outcome == HarvestDecisionOutcome.EqualEstimatesNoClick;
                bool isChosen = !isEqual && chosenLabel != null && ReferenceEquals(estimate.Label, chosenLabel);

                Color highlightColor = isEqual
                    ? EqualEstimateHighlightColor
                    : isChosen ? ChosenHighlightColor : NotChosenHighlightColor;
                ctx.FrameQueue.Enqueue(
                    new RectangleF(
                        labelBounds.X,
                        labelBounds.Y,
                        labelBounds.Width,
                        labelBounds.Height),
                    highlightColor,
                    thickness: 2);

                float textX = labelBounds.Right + 4f;
                float textY = labelBounds.Y + 2f;
                string estimateText = $"{estimate.EstimatedLifeforce:F1}";
                Color textColor = highlightColor;

                ctx.TextQueue.Enqueue(estimateText, new Vector2(textX, textY), textColor, 14, FontAlign.Left);

                if (debug)
                    DrawDebugInfo(ctx.TextQueue, estimate, textX, textY + 18f);
            }
        }

        private void DrawDebugInfo(DeferredTextQueue textQueue, HarvestPlotEstimate estimate, float startX, float startY)
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

                textQueue.Enqueue(debugLine, new Vector2(startX, y + 1f), debugColor, 12, FontAlign.Left);
                y += 15f;
            }
        }
    }
}
