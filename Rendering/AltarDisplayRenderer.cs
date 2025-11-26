using ClickIt.Components;
using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Graphics = ExileCore.Graphics;

#nullable enable

namespace ClickIt.Rendering
{
    public class AltarDisplayRenderer(Graphics graphics, ClickItSettings settings, GameController gameController, WeightCalculator weightCalculator, DeferredTextQueue deferredTextQueue, DeferredFrameQueue deferredFrameQueue, AltarService? altarService = null, Action<string, int>? logMessage = null)
    {
        // Optional external lock to synchronize element access with ClickService
        public object? ElementAccessLock { get; set; }

        private readonly Graphics _graphics = graphics;
        private readonly ClickItSettings _settings = settings;
        private readonly AltarService? _altarService = altarService;
        private readonly GameController _gameController = gameController;
        private readonly WeightCalculator _weightCalculator = weightCalculator;
        private readonly Action<string, int> _logMessage = logMessage ?? ((msg, frame) => { });
        private readonly DeferredTextQueue _deferredTextQueue = deferredTextQueue ?? throw new ArgumentNullException(nameof(deferredTextQueue));
        private readonly DeferredFrameQueue _deferredFrameQueue = deferredFrameQueue ?? throw new ArgumentNullException(nameof(deferredFrameQueue));

        // Button name constants to avoid repeating literal strings
        private const string TOP_BUTTON_NAME = "TopButton";
        private const string BOTTOM_BUTTON_NAME = "BottomButton";

        // Common offsets used for text positioning (avoid constructing every frame)
        private static readonly Vector2 Offset120_Minus60 = new(120, -80);
        private static readonly Vector2 Offset120_Minus25 = new(120, -25);

        public Element? DetermineAltarChoice(PrimaryAltarComponent altar, AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 offset120_Minus60 = Offset120_Minus60;
            Vector2 offset120_Minus25 = Offset120_Minus25;

            if (!IsValidRectangle(topModsRect) || !IsValidRectangle(bottomModsRect))
            {
                _deferredTextQueue.Enqueue("Invalid altar rectangles detected", topModsTopLeft + offset120_Minus60, Color.Red, 30);
                return null;
            }

            if ((altar.TopMods?.HasUnmatchedMods ?? false) || (altar.BottomMods?.HasUnmatchedMods ?? false))
            {
                DrawFailedToMatchModText(topModsTopLeft + offset120_Minus60);
                DrawRedFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.TopUpsideWeight <= 0)
            {
                // TopMods may be null; ensure we pass a non-null string[] to avoid dereference issues
                DrawUnrecognizedWeightText("Top upside", altar.TopMods?.GetAllUpsides() ?? Array.Empty<string>(), topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.TopDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top downside", altar.TopMods?.GetAllDownsides() ?? Array.Empty<string>(), topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.BottomUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom upside", altar.BottomMods?.GetAllUpsides() ?? Array.Empty<string>(), topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.BottomDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom downside", altar.BottomMods?.GetAllDownsides() ?? Array.Empty<string>(), topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            return EvaluateAltarWeights(weights, altar, topModsRect, bottomModsRect, topModsTopLeft + offset120_Minus60, topModsTopLeft + offset120_Minus25);
        }
        private Element? EvaluateAltarWeights(AltarWeights weights, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos1, Vector2 textPos2)
        {
            bool topHasDangerousDownside = _settings.DangerousDownside.Value && HasAnyWeightOverThreshold(weights, true, false, _settings.DangerousDownsideThreshold.Value);
            bool bottomHasDangerousDownside = _settings.DangerousDownside.Value && HasAnyWeightOverThreshold(weights, false, false, _settings.DangerousDownsideThreshold.Value);
            bool topHasHighValueUpside = _settings.ValuableUpside.Value && HasAnyWeightOverThreshold(weights, true, true, _settings.ValuableUpsideThreshold.Value);
            bool bottomHasHighValueUpside = _settings.ValuableUpside.Value && HasAnyWeightOverThreshold(weights, false, true, _settings.ValuableUpsideThreshold.Value);
            bool topHasLowValue = _settings.UnvaluableUpside.Value && HasAnyWeightAtOrBelowThreshold(weights, true, true, _settings.UnvaluableUpsideThreshold.Value);
            bool bottomHasLowValue = _settings.UnvaluableUpside.Value && HasAnyWeightAtOrBelowThreshold(weights, false, true, _settings.UnvaluableUpsideThreshold.Value);

            if (topHasHighValueUpside || bottomHasHighValueUpside)
            {
                return HandleHighValueOverride(topHasHighValueUpside, altar, topModsRect, bottomModsRect, textPos1);
            }

            if (topHasDangerousDownside && bottomHasDangerousDownside)
            {
                return HandleBothDangerousCase(altar, topModsRect, bottomModsRect, textPos1);
            }

            if (topHasDangerousDownside || bottomHasDangerousDownside)
            {
                return HandleDangerousDownside(topHasDangerousDownside, altar, topModsRect, bottomModsRect, textPos1);
            }

            if (topHasLowValue || bottomHasLowValue)
            {
                return HandleLowValueOverride(topHasLowValue, bottomHasLowValue, altar, topModsRect, bottomModsRect, textPos1);
            }

            return HandleNormalWeight(weights, altar, topModsRect, bottomModsRect, textPos2);
        }

        private Element? HandleBothDangerousCase(PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos)
        {
            _deferredTextQueue.Enqueue("Weighting has been overridden\n\nBoth options have downsides with a weight of 90+ that may brick your build.", textPos, Color.Orange, 30);
            _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
            _logMessage?.Invoke("[EvaluateAltarWeights] BOTH DANGEROUS CASE - both sides >= threshold", 10);
            // Validate so diagnostics are logged if buttons are missing/invalid
            _ = GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
            _ = GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            return null;
        }

        private Element? HandleHighValueOverride(bool topChosen, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos)
        {
            if (topChosen)
            {
                _deferredTextQueue.Enqueue("Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of 90+", textPos, Color.LawnGreen, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 3);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
            }
            else
            {
                _deferredTextQueue.Enqueue("Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of 90+", textPos, Color.LawnGreen, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 3);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            }
        }

        private Element? HandleLowValueOverride(bool topHasLowValue, bool bottomHasLowValue, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos)
        {
            // If both sides have low value, treat as equal and let user choose
            if (topHasLowValue && bottomHasLowValue)
            {
                _deferredTextQueue.Enqueue("Both options have low value modifiers (weight < 1), you should choose.", textPos, Color.Orange, 30);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (topHasLowValue)
            {
                _deferredTextQueue.Enqueue("Weighting has been overridden\n\nBottom has been chosen because top has a modifier with weight < 1", textPos, Color.Yellow, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            }
            else
            {
                _deferredTextQueue.Enqueue("Weighting has been overridden\n\nTop has been chosen because bottom has a modifier with weight < 1", textPos, Color.Yellow, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
            }
        }

        private Element? HandleDangerousDownside(bool topHasDanger, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos)
        {
            if (topHasDanger)
            {
                _deferredTextQueue.Enqueue("Weighting overridden\n\nBottom chosen due to top downside 90+", textPos, Color.LawnGreen, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            }
            else
            {
                _deferredTextQueue.Enqueue("Weighting overridden\n\nTop chosen due to bottom downside 90+", textPos, Color.LawnGreen, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
            }
        }

        private Element? HandleNormalWeight(AltarWeights weights, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 tieTextPos)
        {
            if (weights.TopWeight > weights.BottomWeight)
            {
                _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 3);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME) ?? altar.TopButton?.Element;
            }
            if (weights.BottomWeight > weights.TopWeight)
            {
                _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 3);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME) ?? altar.BottomButton?.Element;
            }

            // Tie - leave choice to user
            _deferredTextQueue.Enqueue("Mods have equal weight, you should choose.", tieTextPos, Color.Orange, 30);
            DrawYellowFrames(topModsRect, bottomModsRect);
            return null;
        }

        private Element? GetValidatedButtonElement(AltarButton? button, string buttonName)
        {
            using (LockManager.AcquireStatic(ElementAccessLock))
            {
                if (button == null)
                {
                    _logMessage?.Invoke($"[EvaluateAltarWeights] CRITICAL: {buttonName} is null", 10);
                    return null;
                }
                Element el = button.Element;
                if (el == null)
                {
                    _logMessage?.Invoke($"[EvaluateAltarWeights] CRITICAL: {buttonName}.Element is null", 10);
                    return null;
                }
                if (!el.IsValid)
                {
                    _logMessage?.Invoke($"[EvaluateAltarWeights] CRITICAL: {buttonName}.Element is not valid", 10);
                    return null;
                }
                return el;
            }
        }

        // Flush deferred texts through the shared DeferredTextQueue
        // (callers must provide the Graphics instance and a logger via constructor)

        private bool HasAnyWeightOverThreshold(AltarWeights weights, bool isTop, bool isUpside, int threshold)
        {
            // Create a collection of the relevant weights and check if any exceed threshold
            var weightArray = GetWeightArray(weights, isTop, isUpside);
            for (int i = 0; i < weightArray.Length; i++)
            {
                if (weightArray[i] >= threshold) return true;
            }
            return false;
        }

        private bool HasAnyWeightAtOrBelowThreshold(AltarWeights weights, bool isTop, bool isUpside, int threshold)
        {
            // Create a collection of the relevant weights and check if any are at or below the threshold
            var weightArray = GetWeightArray(weights, isTop, isUpside);
            for (int i = 0; i < weightArray.Length; i++)
            {
                var v = weightArray[i];
                if (v > 0 && v <= threshold) return true;
            }
            return false;
        }

        private static decimal[] GetWeightArray(AltarWeights weights, bool isTop, bool isUpside)
        {
            // Prefer direct array accessors on AltarWeights to reduce duplicated property usage
            if (isUpside)
            {
                return isTop ? weights.GetTopUpsideWeights() : weights.GetBottomUpsideWeights();
            }
            else
            {
                return isTop ? weights.GetTopDownsideWeights() : weights.GetBottomDownsideWeights();
            }
        }
        private void DrawUnrecognizedWeightText(string weightType, string[] mods, Vector2 position)
        {
            if (_graphics == null) return;
            if (mods == null || mods.Length == 0) return;

            var modsText = new System.Text.StringBuilder();
            bool first = true;
            for (int i = 0; i < mods.Length; i++)
            {
                if (!string.IsNullOrEmpty(mods[i]))
                {
                    if (!first) modsText.Append("\n");
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
            if (!IsValidRectangle(topModsRect) || !IsValidRectangle(bottomModsRect)) return;
            _deferredFrameQueue.Enqueue(topModsRect, Color.Red, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.Red, 2);
        }
        private void DrawYellowFrames(RectangleF topModsRect, RectangleF bottomModsRect)
        {
            if (!IsValidRectangle(topModsRect) || !IsValidRectangle(bottomModsRect)) return;
            _deferredFrameQueue.Enqueue(topModsRect, Color.Yellow, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.Yellow, 2);
        }

        public void DrawWeightTexts(AltarWeights weights, Vector2 topModsTopLeft, Vector2 bottomModsTopLeft)
        {
            Vector2 offset5_Minus32 = new(5, -32);
            Vector2 offset5_Minus20 = new(5, -20);
            Vector2 offset10_Minus32 = new(10, -32);
            Vector2 offset10_Minus20 = new(10, -20);
            Vector2 offset10_5 = new(10, 5);
            Color colorLawnGreen = Color.LawnGreen;
            Color colorOrangeRed = Color.OrangeRed;
            Color colorYellow = Color.Yellow;
            _deferredTextQueue.Enqueue("Upside: " + weights.TopUpsideWeight, topModsTopLeft + offset5_Minus32, colorLawnGreen, 14);
            _deferredTextQueue.Enqueue("Downside: " + weights.TopDownsideWeight, topModsTopLeft + offset5_Minus20, colorOrangeRed, 14);
            _deferredTextQueue.Enqueue("Upside: " + weights.BottomUpsideWeight, bottomModsTopLeft + offset10_Minus32, colorLawnGreen, 14);
            _deferredTextQueue.Enqueue("Downside: " + weights.BottomDownsideWeight, bottomModsTopLeft + offset10_Minus20, colorOrangeRed, 14);
            Color topWeightColor = GetWeightColor(weights.TopWeight, weights.BottomWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            Color bottomWeightColor = GetWeightColor(weights.BottomWeight, weights.TopWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            _deferredTextQueue.Enqueue("" + weights.TopWeight, topModsTopLeft + offset10_5, topWeightColor, 18);
            _deferredTextQueue.Enqueue("" + weights.BottomWeight, bottomModsTopLeft + offset10_5, bottomWeightColor, 18);
        }

        public void RenderAltarComponents()
        {
            var altarSnapshot = _altarService?.GetAltarComponentsReadOnly();
            if (altarSnapshot == null || altarSnapshot.Count == 0) return;

            // Cache frequently accessed settings
            bool clickEater = _settings.ClickEaterAltars;
            bool clickExarch = _settings.ClickExarchAltars;
            bool leftHanded = _settings.LeftHanded;
            Vector2 windowTopLeft = _gameController.Window.GetWindowRectangleTimeCache.TopLeft;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                RenderSingleAltar(altar, clickEater, clickExarch, leftHanded, windowTopLeft);
            }
        }

        public void RenderSingleAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch, bool leftHanded, Vector2 windowTopLeft)
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

            if (!IsValidRectangle(topModsRect) || !IsValidRectangle(bottomModsRect))
            {
                return;
            }

            Vector2 topModsTopLeft = topModsRect.TopLeft;
            Vector2 bottomModsTopLeft = bottomModsRect.TopLeft;

            DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);
            DrawWeightTexts(altarWeights.Value, topModsTopLeft, bottomModsTopLeft);
            // Deferred text rendering is flushed at the end of the main Render method
        }

        private static bool IsValidRectangle(RectangleF rect)
        {
            return rect.Width > 0 && rect.Height > 0 &&
                   !float.IsNaN(rect.X) && !float.IsNaN(rect.Y) &&
                   !float.IsInfinity(rect.X) && !float.IsInfinity(rect.Y);
        }

        private static Color GetWeightColor(decimal weight1, decimal weight2, Color winColor, Color loseColor, Color tieColor)
        {
            if (weight1 > weight2) return winColor;
            if (weight2 > weight1) return loseColor;
            return tieColor;
        }
    }
}
