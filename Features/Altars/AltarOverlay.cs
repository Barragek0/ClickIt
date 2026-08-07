namespace ClickIt.Features.Altars
{
    /// <summary>
    /// Owns the altar choice overlay. Pure per-frame draw of the cached altar snapshot — the heavy
    /// mod parsing runs on the altar scan coroutine and each PrimaryAltarComponent caches its
    /// validity (1s) and weights (5s) via TimedValueCache. Fresh element rects per frame so the
    /// boxes follow the player at full refresh rate.
    /// </summary>
    public sealed class AltarOverlay : IOverlay
    {
        // Common offsets used for text positioning (avoid constructing every frame)
        private static readonly Vector2 Offset120_Minus60 = new(120, -80);
        private static readonly Vector2 Offset120_Minus25 = new(120, -25);
        private static readonly Vector2 Offset5_Minus32 = new(5, -32);
        private static readonly Vector2 Offset5_Minus20 = new(5, -20);
        private static readonly Vector2 Offset10_Minus32 = new(10, -32);
        private static readonly Vector2 Offset10_Minus20 = new(10, -20);
        private static readonly Vector2 Offset10_5 = new(10, 5);
        private static readonly Color WeightWinColor = Color.LawnGreen;
        private static readonly Color WeightLoseColor = Color.OrangeRed;
        private static readonly Color WeightTieColor = Color.Yellow;

        private readonly AltarService? _altarService;
        private readonly WeightCalculator _weightCalculator;
        private readonly AltarChoiceEvaluator _altarChoiceEvaluator;
        private readonly Action<string, int> _logMessage;

        public AltarOverlay(WeightCalculator weightCalculator, AltarChoiceEvaluator altarChoiceEvaluator, AltarService? altarService, Action<string, int>? logMessage)
        {
            _weightCalculator = weightCalculator;
            _altarChoiceEvaluator = altarChoiceEvaluator;
            _altarService = altarService;
            _logMessage = logMessage ?? ((msg, frame) => { });
        }

        public string Name => "Altar";

        public RenderSection Section => RenderSection.AltarOverlay;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.None;

        public TimingChannel? RefreshTimingChannel => null;

        public ProcessingSection ProcessingSection => ProcessingSection.Altar;

        public bool IsEnabled(ClickItSettings settings)
            => (_altarService?.GetAltarComponentCount() ?? 0) > 0;

        public void Refresh(OverlayRefreshContext ctx)
        {
        }

        public void Draw(OverlayRenderContext ctx)
        {
            IReadOnlyList<PrimaryAltarComponent>? altarSnapshot = _altarService?.GetAltarComponentsReadOnly();
            if (altarSnapshot == null || altarSnapshot.Count == 0)
                return;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                RenderSingleAltar(ctx.TextQueue, ctx.FrameQueue, altar);
            }
        }

        private void RenderSingleAltar(DeferredTextQueue textQueue, DeferredFrameQueue frameQueue, PrimaryAltarComponent altar)
        {
            if (!altar.IsValidCached())
            {
                return;
            }

            AltarWeights? altarWeights = altar.GetCachedWeights(_weightCalculator.CalculateAltarWeights);

            if (!altarWeights.HasValue)
            {
                return;
            }

            RectangleF topModsRect = altar.GetTopModsRect();
            RectangleF bottomModsRect = altar.GetBottomModsRect();

            if (!IsValidRectangles(topModsRect, bottomModsRect))
            {
                return;
            }

            Vector2 topModsTopLeft = topModsRect.TopLeft;
            Vector2 bottomModsTopLeft = bottomModsRect.TopLeft;

            AltarChoiceEvaluation evaluation = _altarChoiceEvaluator.EvaluateChoice(altar, altarWeights.Value, topModsRect, bottomModsRect);
            RenderChoiceEvaluation(textQueue, frameQueue, evaluation, topModsRect, bottomModsRect, topModsTopLeft);

            DrawWeightTexts(textQueue, altarWeights.Value, topModsTopLeft, bottomModsTopLeft);
        }

        private static bool IsValidRectangle(RectangleF rect)
        {
            return rect.Width > 0 && rect.Height > 0 &&
                   !float.IsNaN(rect.X) && !float.IsNaN(rect.Y) &&
                   !float.IsInfinity(rect.X) && !float.IsInfinity(rect.Y);
        }

        private static bool IsValidRectangles(RectangleF first, RectangleF second)
        {
            return IsValidRectangle(first) && IsValidRectangle(second);
        }

        private static Color GetWeightColor(decimal weight1, decimal weight2, Color winColor, Color loseColor, Color tieColor)
        {
            if (weight1 > weight2) return winColor;
            if (weight2 > weight1) return loseColor;
            return tieColor;
        }

        private void DrawWeightTexts(DeferredTextQueue textQueue, AltarWeights weights, Vector2 topModsTopLeft, Vector2 bottomModsTopLeft)
        {
            textQueue.Enqueue($"Upside: {weights.TopUpsideWeight}", topModsTopLeft + Offset5_Minus32, WeightWinColor, 14);
            textQueue.Enqueue($"Downside: {weights.TopDownsideWeight}", topModsTopLeft + Offset5_Minus20, WeightLoseColor, 14);
            textQueue.Enqueue($"Upside: {weights.BottomUpsideWeight}", bottomModsTopLeft + Offset10_Minus32, WeightWinColor, 14);
            textQueue.Enqueue($"Downside: {weights.BottomDownsideWeight}", bottomModsTopLeft + Offset10_Minus20, WeightLoseColor, 14);
            Color topWeightColor = GetWeightColor(weights.TopWeight, weights.BottomWeight, WeightWinColor, WeightLoseColor, WeightTieColor);
            Color bottomWeightColor = GetWeightColor(weights.BottomWeight, weights.TopWeight, WeightWinColor, WeightLoseColor, WeightTieColor);
            textQueue.Enqueue($"{weights.TopWeight}", topModsTopLeft + Offset10_5, topWeightColor, 18);
            textQueue.Enqueue($"{weights.BottomWeight}", bottomModsTopLeft + Offset10_5, bottomWeightColor, 18);
        }

        private void RenderChoiceEvaluation(DeferredTextQueue textQueue, DeferredFrameQueue frameQueue, AltarChoiceEvaluation evaluation, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 textPos1 = topModsTopLeft + Offset120_Minus60;
            Vector2 textPos2 = topModsTopLeft + Offset120_Minus25;

            switch (evaluation.Outcome)
            {
                case AltarChoiceOutcome.InvalidRectangles:
                    textQueue.Enqueue("Invalid altar rectangles detected", textPos1, Color.Red, 30);
                    break;
                case AltarChoiceOutcome.UnmatchedMods:
                    DrawFailedToMatchModText(textQueue, textPos1);
                    DrawRedFrames(frameQueue, topModsRect, bottomModsRect);
                    break;
                case AltarChoiceOutcome.UnrecognizedTopUpside:
                case AltarChoiceOutcome.UnrecognizedTopDownside:
                case AltarChoiceOutcome.UnrecognizedBottomUpside:
                case AltarChoiceOutcome.UnrecognizedBottomDownside:
                    DrawUnrecognizedWeightText(textQueue, evaluation.UnrecognizedWeightType ?? string.Empty, evaluation.UnrecognizedMods ?? [], textPos1);
                    DrawYellowFrames(frameQueue, topModsRect, bottomModsRect);
                    break;
                case AltarChoiceOutcome.HighValueTopChosen:
                    textQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of {evaluation.Threshold}+", textPos1, Color.LawnGreen, 30);
                    frameQueue.Enqueue(topModsRect, Color.LawnGreen, 3);
                    frameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                    break;
                case AltarChoiceOutcome.HighValueBottomChosen:
                    textQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of {evaluation.Threshold}+", textPos1, Color.LawnGreen, 30);
                    frameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
                    frameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 3);
                    break;
                case AltarChoiceOutcome.BothDangerousManual:
                    textQueue.Enqueue($"Weighting has been overridden\n\nBoth options have downsides with a weight of {evaluation.Threshold}+ that may brick your build.", textPos1, Color.Orange, 30);
                    frameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
                    frameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                    _logMessage("[RenderChoiceEvaluation] BOTH DANGEROUS CASE - both sides >= threshold", 10);
                    break;
                case AltarChoiceOutcome.DangerousTopChooseBottom:
                    textQueue.Enqueue($"Weighting overridden\n\nBottom chosen due to top downside {evaluation.Threshold}+", textPos1, Color.LawnGreen, 30);
                    frameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                    frameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                    break;
                case AltarChoiceOutcome.DangerousBottomChooseTop:
                    textQueue.Enqueue($"Weighting overridden\n\nTop chosen due to bottom downside {evaluation.Threshold}+", textPos1, Color.LawnGreen, 30);
                    frameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                    frameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                    break;
                case AltarChoiceOutcome.BothLowValueManual:
                    textQueue.Enqueue($"Both options have low value modifiers (weight <= {evaluation.Threshold}), you should choose.", textPos1, Color.Orange, 30);
                    DrawYellowFrames(frameQueue, topModsRect, bottomModsRect);
                    break;
                case AltarChoiceOutcome.TopLowValueChooseBottom:
                    textQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because top has a modifier with weight <= {evaluation.Threshold}", textPos1, Color.Yellow, 30);
                    frameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                    frameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                    break;
                case AltarChoiceOutcome.BottomLowValueChooseTop:
                    textQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because bottom has a modifier with weight <= {evaluation.Threshold}", textPos1, Color.Yellow, 30);
                    frameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                    frameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                    break;
                case AltarChoiceOutcome.BothBelowMinimumManual:
                    textQueue.Enqueue($"Both options have final weights below the minimum threshold ({evaluation.Threshold}) - please choose manually.", textPos1, Color.Orange, 30);
                    DrawYellowFrames(frameQueue, topModsRect, bottomModsRect);
                    break;
                case AltarChoiceOutcome.TopBelowMinimumChooseBottom:
                    textQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because top weight ({evaluation.TopWeight}) is below minimum {evaluation.Threshold}", textPos1, Color.Yellow, 30);
                    frameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                    frameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                    break;
                case AltarChoiceOutcome.BottomBelowMinimumChooseTop:
                    textQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because bottom weight ({evaluation.BottomWeight}) is below minimum {evaluation.Threshold}", textPos1, Color.Yellow, 30);
                    frameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                    frameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                    break;
                case AltarChoiceOutcome.TopWeightHigher:
                    frameQueue.Enqueue(topModsRect, Color.LawnGreen, 3);
                    frameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                    break;
                case AltarChoiceOutcome.BottomWeightHigher:
                    frameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
                    frameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 3);
                    break;
                case AltarChoiceOutcome.EqualWeightsManual:
                    textQueue.Enqueue("Mods have equal weight, you should choose.", textPos2, Color.Orange, 30);
                    DrawYellowFrames(frameQueue, topModsRect, bottomModsRect);
                    break;
                default:
                    break;
            }
        }

        private void DrawUnrecognizedWeightText(DeferredTextQueue textQueue, string weightType, string[] mods, Vector2 position)
        {
            if (mods == null || mods.Length == 0) return;

            StringBuilder modsText = new();
            bool first = true;
            for (int i = 0; i < mods.Length; i++)
            {
                if (!string.IsNullOrEmpty(mods[i]))
                {
                    if (!first) modsText.Append('\n');
                    modsText.Append($"{i + 1}:{mods[i]}");
                    first = false;
                }
            }

            if (modsText.Length > 0)
            {
                textQueue.Enqueue($"{weightType} weights couldn't be recognised\n{modsText}\nPlease report this as a bug on github", position, Color.Orange, 30);
            }
        }

        private void DrawFailedToMatchModText(DeferredTextQueue textQueue, Vector2 position)
        {
            textQueue.Enqueue("Failed to match mod - unable to determine best choice.\nPlease report this as a bug on github", position, Color.Red, 30);
        }

        private void DrawRedFrames(DeferredFrameQueue frameQueue, RectangleF topModsRect, RectangleF bottomModsRect)
        {
            if (!IsValidRectangles(topModsRect, bottomModsRect)) return;
            frameQueue.Enqueue(topModsRect, Color.Red, 2);
            frameQueue.Enqueue(bottomModsRect, Color.Red, 2);
        }

        private void DrawYellowFrames(DeferredFrameQueue frameQueue, RectangleF topModsRect, RectangleF bottomModsRect)
        {
            if (!IsValidRectangles(topModsRect, bottomModsRect)) return;
            frameQueue.Enqueue(topModsRect, Color.Yellow, 2);
            frameQueue.Enqueue(bottomModsRect, Color.Yellow, 2);
        }
    }
}
