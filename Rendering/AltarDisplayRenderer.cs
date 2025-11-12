using ClickIt.Components;
using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ClickIt.Rendering
{
    public class AltarDisplayRenderer
    {
        // Optional external lock to synchronize element access with ClickService
        public object? ElementAccessLock { get; set; }

        private readonly Graphics _graphics;
        private readonly ClickItSettings _settings;
        private readonly AltarService? _altarService;
        private readonly GameController _gameController;
        private readonly WeightCalculator _weightCalculator;
        private readonly Action<string, int> _logMessage;
        // Deferred text rendering queue to avoid calling DrawText inside sensitive code paths
        private readonly DeferredTextQueue _deferredTextQueue = new();

        public AltarDisplayRenderer(Graphics graphics, ClickItSettings settings, GameController gameController, WeightCalculator weightCalculator, AltarService? altarService = null, Action<string, int>? logMessage = null)
        {
            _graphics = graphics;
            _settings = settings;
            _gameController = gameController;
            _weightCalculator = weightCalculator;
            _altarService = altarService;
            _logMessage = logMessage ?? ((msg, frame) => { });
        }

        public Element? DetermineAltarChoice(PrimaryAltarComponent altar, Utils.AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 offset120_Minus60 = new(120, -80);
            Vector2 offset120_Minus25 = new(120, -25);

            if (!IsValidRectangle(topModsRect) || !IsValidRectangle(bottomModsRect))
            {
                _graphics?.DrawText("Invalid altar rectangles detected", topModsTopLeft + offset120_Minus60, Color.Red, 30);
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
                DrawUnrecognizedWeightText("Top upside", altar.TopMods.GetAllUpsides(), topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.TopDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top downside", altar.TopMods.GetAllDownsides(), topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.BottomUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom upside", altar.BottomMods.GetAllUpsides(), topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.BottomDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom downside", altar.BottomMods.GetAllDownsides(), topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            return EvaluateAltarWeights(weights, altar, topModsRect, bottomModsRect, topModsTopLeft + offset120_Minus60, topModsTopLeft + offset120_Minus25);
        }
        private Element? EvaluateAltarWeights(Utils.AltarWeights weights, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos1, Vector2 textPos2)
        {
            // Decision thresholds
            const int DANGEROUS_THRESHOLD = 90;
            const int HIGH_VALUE_THRESHOLD = 90;

            bool topHasDangerousDownside = HasAnyWeightOverThreshold(weights, true, false, DANGEROUS_THRESHOLD);
            bool bottomHasDangerousDownside = HasAnyWeightOverThreshold(weights, false, false, DANGEROUS_THRESHOLD);
            bool topHasHighValueUpside = HasAnyWeightOverThreshold(weights, true, true, HIGH_VALUE_THRESHOLD);
            bool bottomHasHighValueUpside = HasAnyWeightOverThreshold(weights, false, true, HIGH_VALUE_THRESHOLD);

            // Helper to validate and return a button's element safely (under optional global lock).
            // Keep only vital failure logs; successful validations remain quiet.
            const string TOP_BUTTON_NAME = "TopButton";
            const string BOTTOM_BUTTON_NAME = "BottomButton";
            Element? GetValidatedButtonElement(AltarButton? button, string buttonName)
            {
                var gm = global::ClickIt.Utils.LockManager.Instance;
                if (gm != null)
                {
                    using (gm.Acquire(ElementAccessLock))
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

                if (button == null)
                {
                    _logMessage?.Invoke($"[EvaluateAltarWeights] CRITICAL: {buttonName} is null", 10);
                    return null;
                }
                Element elNoLock = button.Element;
                if (elNoLock == null)
                {
                    _logMessage?.Invoke($"[EvaluateAltarWeights] CRITICAL: {buttonName}.Element is null", 10);
                    return null;
                }
                if (!elNoLock.IsValid)
                {
                    _logMessage?.Invoke($"[EvaluateAltarWeights] CRITICAL: {buttonName}.Element is not valid", 10);
                    return null;
                }
                return elNoLock;
            }

            // Both sides are dangerous -> highlight and log diagnostics, but do not auto-click
            if (topHasDangerousDownside && bottomHasDangerousDownside)
            {
                _deferredTextQueue.Enqueue("Weighting has been overridden\n\nBoth options have downsides with a weight of 90+ that may brick your build.", textPos1, Color.Orange, 30);
                _graphics?.DrawFrame(topModsRect, Color.OrangeRed, 2);
                _graphics?.DrawFrame(bottomModsRect, Color.OrangeRed, 2);

                // Additional diagnostics: both sides are over the threshold. Only failures are logged by validator.
                _logMessage?.Invoke("[EvaluateAltarWeights] BOTH DANGEROUS CASE - both sides >= threshold", 10);
                GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
                GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
                return null;
            }

            // High-value upsides override normal weighting
            if (topHasHighValueUpside)
            {
                _deferredTextQueue.Enqueue("Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                _graphics?.DrawFrame(topModsRect, Color.LawnGreen, 3);
                _graphics?.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
            }
            if (bottomHasHighValueUpside)
            {
                _deferredTextQueue.Enqueue("Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                _graphics?.DrawFrame(topModsRect, Color.OrangeRed, 2);
                _graphics?.DrawFrame(bottomModsRect, Color.LawnGreen, 3);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            }

            // Dangerous downsides: prefer the other option
            if (topHasDangerousDownside)
            {
                _deferredTextQueue.Enqueue("Weighting overridden\n\nBottom chosen due to top downside 90+", textPos1, Color.LawnGreen, 30);
                _graphics?.DrawFrame(topModsRect, Color.OrangeRed, 3);
                _graphics?.DrawFrame(bottomModsRect, Color.LawnGreen, 2);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            }
            if (bottomHasDangerousDownside)
            {
                _deferredTextQueue.Enqueue("Weighting overridden\n\nTop chosen due to bottom downside 90+", textPos1, Color.LawnGreen, 30);
                _graphics?.DrawFrame(topModsRect, Color.LawnGreen, 2);
                _graphics?.DrawFrame(bottomModsRect, Color.OrangeRed, 3);
                return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
            }

            // Normal weighting
            if (weights.TopWeight > weights.BottomWeight)
            {
                _graphics?.DrawFrame(topModsRect, Color.LawnGreen, 3);
                _graphics?.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME) ?? altar.TopButton?.Element;
            }
            if (weights.BottomWeight > weights.TopWeight)
            {
                _graphics?.DrawFrame(topModsRect, Color.OrangeRed, 2);
                _graphics?.DrawFrame(bottomModsRect, Color.LawnGreen, 3);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME) ?? altar.BottomButton?.Element;
            }

            // Tie - leave choice to user
            _deferredTextQueue.Enqueue("Mods have equal weight, you should choose.", textPos2, Color.Orange, 30);
            DrawYellowFrames(topModsRect, bottomModsRect);
            return null;
        }

        // Flush deferred texts through the shared DeferredTextQueue
        // (callers must provide the Graphics instance and a logger via constructor)

        private bool HasAnyWeightOverThreshold(Utils.AltarWeights weights, bool isTop, bool isUpside, int threshold)
        {
            // Create a collection of the relevant weights and check if any exceed threshold
            var weightArray = GetWeightArray(weights, isTop, isUpside);
            return weightArray.Any(w => w >= threshold);
        }

        private decimal[] GetWeightArray(Utils.AltarWeights weights, bool isTop, bool isUpside)
        {
            if (isUpside)
            {
                return isTop ?
                    new[] { weights.TopUpside1Weight, weights.TopUpside2Weight, weights.TopUpside3Weight, weights.TopUpside4Weight,
                            weights.TopUpside5Weight, weights.TopUpside6Weight, weights.TopUpside7Weight, weights.TopUpside8Weight } :
                    new[] { weights.BottomUpside1Weight, weights.BottomUpside2Weight, weights.BottomUpside3Weight, weights.BottomUpside4Weight,
                            weights.BottomUpside5Weight, weights.BottomUpside6Weight, weights.BottomUpside7Weight, weights.BottomUpside8Weight };
            }
            else
            {
                return isTop ?
                    new[] { weights.TopDownside1Weight, weights.TopDownside2Weight, weights.TopDownside3Weight, weights.TopDownside4Weight,
                            weights.TopDownside5Weight, weights.TopDownside6Weight, weights.TopDownside7Weight, weights.TopDownside8Weight } :
                    new[] { weights.BottomDownside1Weight, weights.BottomDownside2Weight, weights.BottomDownside3Weight, weights.BottomDownside4Weight,
                            weights.BottomDownside5Weight, weights.BottomDownside6Weight, weights.BottomDownside7Weight, weights.BottomDownside8Weight };
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
                _ = _graphics.DrawText($"{weightType} weights couldn't be recognised\n{modsText}\nPlease report this as a bug on github", position, Color.Orange, 30);
            }
        }
        private void DrawFailedToMatchModText(Vector2 position)
        {
            if (_graphics == null) return;
            _graphics.DrawText("Failed to match mod - unable to determine best choice.\nPlease report this as a bug on github", position, Color.Red, 30);
        }
        private void DrawRedFrames(RectangleF topModsRect, RectangleF bottomModsRect)
        {
            if (_graphics == null) return;
            if (!IsValidRectangle(topModsRect) || !IsValidRectangle(bottomModsRect)) return;
            _graphics.DrawFrame(topModsRect, Color.Red, 2);
            _graphics.DrawFrame(bottomModsRect, Color.Red, 2);
        }
        private void DrawYellowFrames(RectangleF topModsRect, RectangleF bottomModsRect)
        {
            if (_graphics == null) return;
            if (!IsValidRectangle(topModsRect) || !IsValidRectangle(bottomModsRect)) return;
            _graphics.DrawFrame(topModsRect, Color.Yellow, 2);
            _graphics.DrawFrame(bottomModsRect, Color.Yellow, 2);
        }
        public void DrawWeightTexts(Utils.AltarWeights weights, Vector2 topModsTopLeft, Vector2 bottomModsTopLeft)
        {
            Vector2 offset5_Minus32 = new(5, -32);
            Vector2 offset5_Minus20 = new(5, -20);
            Vector2 offset10_Minus32 = new(10, -32);
            Vector2 offset10_Minus20 = new(10, -20);
            Vector2 offset10_5 = new(10, 5);
            Color colorLawnGreen = Color.LawnGreen;
            Color colorOrangeRed = Color.OrangeRed;
            Color colorYellow = Color.Yellow;
            _ = _graphics?.DrawText("Upside: " + weights.TopUpsideWeight, topModsTopLeft + offset5_Minus32, colorLawnGreen, 14);
            _ = _graphics?.DrawText("Downside: " + weights.TopDownsideWeight, topModsTopLeft + offset5_Minus20, colorOrangeRed, 14);
            _ = _graphics?.DrawText("Upside: " + weights.BottomUpsideWeight, bottomModsTopLeft + offset10_Minus32, colorLawnGreen, 14);
            _ = _graphics?.DrawText("Downside: " + weights.BottomDownsideWeight, bottomModsTopLeft + offset10_Minus20, colorOrangeRed, 14);
            Color topWeightColor = GetWeightColor(weights.TopWeight, weights.BottomWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            Color bottomWeightColor = GetWeightColor(weights.BottomWeight, weights.TopWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            _ = _graphics?.DrawText("" + weights.TopWeight, topModsTopLeft + offset10_5, topWeightColor, 18);
            _ = _graphics?.DrawText("" + weights.BottomWeight, bottomModsTopLeft + offset10_5, bottomWeightColor, 18);
        }

        public void RenderAltarComponents()
        {
            List<PrimaryAltarComponent> altarSnapshot = _altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
            if (altarSnapshot.Count == 0) return;

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
            // Flush any deferred DrawText calls queued during DetermineAltarChoice
            _deferredTextQueue.Flush(_graphics, _logMessage);
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
