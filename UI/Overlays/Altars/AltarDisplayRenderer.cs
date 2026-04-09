#nullable enable

namespace ClickIt.UI.Overlays.Altars
{
    public class AltarDisplayRenderer(Graphics graphics, ClickItSettings settings, GameController gameController, WeightCalculator weightCalculator, AltarChoiceEvaluator altarChoiceEvaluator, DeferredTextQueue deferredTextQueue, DeferredFrameQueue deferredFrameQueue, AltarService? altarService = null, Action<string, int>? logMessage = null)
    {
        private readonly Graphics _graphics = graphics;
        private readonly ClickItSettings _settings = settings;
        private readonly AltarService? _altarService = altarService;
        private readonly GameController _gameController = gameController;
        private readonly WeightCalculator _weightCalculator = weightCalculator;
        private readonly AltarChoiceEvaluator _altarChoiceEvaluator = altarChoiceEvaluator;
        private readonly Action<string, int> _logMessage = logMessage ?? ((msg, frame) => { });
        private readonly DeferredTextQueue _deferredTextQueue = deferredTextQueue ?? throw new ArgumentNullException(nameof(deferredTextQueue));
        private readonly DeferredFrameQueue _deferredFrameQueue = deferredFrameQueue ?? throw new ArgumentNullException(nameof(deferredFrameQueue));

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

        private void DrawUnrecognizedWeightText(string weightType, string[] mods, Vector2 position)
        {
            if (_graphics == null) return;
            if (mods == null || mods.Length == 0) return;

            var modsText = new StringBuilder();
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
                _deferredTextQueue.Enqueue($"{weightType} weights couldn't be recognised\n{modsText}\nPlease report this as a bug on github", position, Color.Orange, 30);
            }
        }
        private void DrawFailedToMatchModText(Vector2 position)
        {
            if (_graphics == null) return;
            _deferredTextQueue.Enqueue("Failed to match mod - unable to determine best choice.\nPlease report this as a bug on github", position, Color.Red, 30);
        }
        private void DrawRedFrames(RectangleF topModsRect, RectangleF bottomModsRect)
        {
            if (!IsValidRectangles(topModsRect, bottomModsRect)) return;
            _deferredFrameQueue.Enqueue(topModsRect, Color.Red, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.Red, 2);
        }
        private void DrawYellowFrames(RectangleF topModsRect, RectangleF bottomModsRect)
        {
            if (!IsValidRectangles(topModsRect, bottomModsRect)) return;
            _deferredFrameQueue.Enqueue(topModsRect, Color.Yellow, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.Yellow, 2);
        }

        public void DrawWeightTexts(AltarWeights weights, Vector2 topModsTopLeft, Vector2 bottomModsTopLeft)
        {
            _deferredTextQueue.Enqueue($"Upside: {weights.TopUpsideWeight}", topModsTopLeft + Offset5_Minus32, WeightWinColor, 14);
            _deferredTextQueue.Enqueue($"Downside: {weights.TopDownsideWeight}", topModsTopLeft + Offset5_Minus20, WeightLoseColor, 14);
            _deferredTextQueue.Enqueue($"Upside: {weights.BottomUpsideWeight}", bottomModsTopLeft + Offset10_Minus32, WeightWinColor, 14);
            _deferredTextQueue.Enqueue($"Downside: {weights.BottomDownsideWeight}", bottomModsTopLeft + Offset10_Minus20, WeightLoseColor, 14);
            Color topWeightColor = GetWeightColor(weights.TopWeight, weights.BottomWeight, WeightWinColor, WeightLoseColor, WeightTieColor);
            Color bottomWeightColor = GetWeightColor(weights.BottomWeight, weights.TopWeight, WeightWinColor, WeightLoseColor, WeightTieColor);
            _deferredTextQueue.Enqueue($"{weights.TopWeight}", topModsTopLeft + Offset10_5, topWeightColor, 18);
            _deferredTextQueue.Enqueue($"{weights.BottomWeight}", bottomModsTopLeft + Offset10_5, bottomWeightColor, 18);
        }

        public void RenderAltarComponents()
        {
            var altarSnapshot = _altarService?.GetAltarComponentsReadOnly();
            if (altarSnapshot == null || altarSnapshot.Count == 0) return;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                RenderSingleAltar(altar);
            }
        }

        public void RenderSingleAltar(PrimaryAltarComponent altar)
        {
            if (!altar.IsValidCached())
            {
                return;
            }

            var altarWeights = altar.GetCachedWeights(pc => _weightCalculator.CalculateAltarWeights(pc));

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
            RenderChoiceEvaluation(evaluation, topModsRect, bottomModsRect, topModsTopLeft);

            DrawWeightTexts(altarWeights.Value, topModsTopLeft, bottomModsTopLeft);
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

        private void RenderChoiceEvaluation(AltarChoiceEvaluation evaluation, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 textPos1 = topModsTopLeft + Offset120_Minus60;
            Vector2 textPos2 = topModsTopLeft + Offset120_Minus25;

            switch (evaluation.Outcome)
            {
                case AltarChoiceOutcome.InvalidRectangles:
                    _deferredTextQueue.Enqueue("Invalid altar rectangles detected", textPos1, Color.Red, 30);
                    break;
                case AltarChoiceOutcome.UnmatchedMods:
                    DrawFailedToMatchModText(textPos1);
                    DrawRedFrames(topModsRect, bottomModsRect);
                    break;
                case AltarChoiceOutcome.UnrecognizedTopUpside:
                case AltarChoiceOutcome.UnrecognizedTopDownside:
                case AltarChoiceOutcome.UnrecognizedBottomUpside:
                case AltarChoiceOutcome.UnrecognizedBottomDownside:
                    DrawUnrecognizedWeightText(evaluation.UnrecognizedWeightType ?? string.Empty, evaluation.UnrecognizedMods ?? [], textPos1);
                    DrawYellowFrames(topModsRect, bottomModsRect);
                    break;
                case AltarChoiceOutcome.HighValueTopChosen:
                    _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of {evaluation.Threshold}+", textPos1, Color.LawnGreen, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 3);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                    break;
                case AltarChoiceOutcome.HighValueBottomChosen:
                    _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of {evaluation.Threshold}+", textPos1, Color.LawnGreen, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 3);
                    break;
                case AltarChoiceOutcome.BothDangerousManual:
                    _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nBoth options have downsides with a weight of {evaluation.Threshold}+ that may brick your build.", textPos1, Color.Orange, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                    _logMessage("[RenderChoiceEvaluation] BOTH DANGEROUS CASE - both sides >= threshold", 10);
                    break;
                case AltarChoiceOutcome.DangerousTopChooseBottom:
                    _deferredTextQueue.Enqueue($"Weighting overridden\n\nBottom chosen due to top downside {evaluation.Threshold}+", textPos1, Color.LawnGreen, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                    break;
                case AltarChoiceOutcome.DangerousBottomChooseTop:
                    _deferredTextQueue.Enqueue($"Weighting overridden\n\nTop chosen due to bottom downside {evaluation.Threshold}+", textPos1, Color.LawnGreen, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                    break;
                case AltarChoiceOutcome.BothLowValueManual:
                    _deferredTextQueue.Enqueue($"Both options have low value modifiers (weight <= {evaluation.Threshold}), you should choose.", textPos1, Color.Orange, 30);
                    DrawYellowFrames(topModsRect, bottomModsRect);
                    break;
                case AltarChoiceOutcome.TopLowValueChooseBottom:
                    _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because top has a modifier with weight <= {evaluation.Threshold}", textPos1, Color.Yellow, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                    break;
                case AltarChoiceOutcome.BottomLowValueChooseTop:
                    _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because bottom has a modifier with weight <= {evaluation.Threshold}", textPos1, Color.Yellow, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                    break;
                case AltarChoiceOutcome.BothBelowMinimumManual:
                    _deferredTextQueue.Enqueue($"Both options have final weights below the minimum threshold ({evaluation.Threshold}) - please choose manually.", textPos1, Color.Orange, 30);
                    DrawYellowFrames(topModsRect, bottomModsRect);
                    break;
                case AltarChoiceOutcome.TopBelowMinimumChooseBottom:
                    _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because top weight ({evaluation.TopWeight}) is below minimum {evaluation.Threshold}", textPos1, Color.Yellow, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                    break;
                case AltarChoiceOutcome.BottomBelowMinimumChooseTop:
                    _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because bottom weight ({evaluation.BottomWeight}) is below minimum {evaluation.Threshold}", textPos1, Color.Yellow, 30);
                    _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                    break;
                case AltarChoiceOutcome.TopWeightHigher:
                    _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 3);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                    break;
                case AltarChoiceOutcome.BottomWeightHigher:
                    _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
                    _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 3);
                    break;
                case AltarChoiceOutcome.EqualWeightsManual:
                    _deferredTextQueue.Enqueue("Mods have equal weight, you should choose.", textPos2, Color.Orange, 30);
                    DrawYellowFrames(topModsRect, bottomModsRect);
                    break;
                default:
                    break;
            }
        }
    }
}
