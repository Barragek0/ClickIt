using ClickIt.Components;
using ClickIt.Utils;
using ExileCore.PoEMemory;
using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

#nullable enable

namespace ClickIt.Rendering
{
    public partial class AltarDisplayRenderer
    {
        public Element? DetermineAltarChoice(PrimaryAltarComponent altar, AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 offset120_Minus60 = Offset120_Minus60;
            Vector2 offset120_Minus25 = Offset120_Minus25;

            if (!IsValidRectangles(topModsRect, bottomModsRect))
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
            int dangerousThreshold = _settings.DangerousDownsideThreshold.Value;
            int valuableThreshold = _settings.ValuableUpsideThreshold.Value;
            int lowValueThreshold = _settings.UnvaluableUpsideThreshold.Value;

            bool topHasDangerousDownside = _settings.DangerousDownside.Value && HasAnyWeightOverThreshold(weights, true, false, dangerousThreshold);
            bool bottomHasDangerousDownside = _settings.DangerousDownside.Value && HasAnyWeightOverThreshold(weights, false, false, dangerousThreshold);
            bool topHasHighValueUpside = _settings.ValuableUpside.Value && HasAnyWeightOverThreshold(weights, true, true, valuableThreshold);
            bool bottomHasHighValueUpside = _settings.ValuableUpside.Value && HasAnyWeightOverThreshold(weights, false, true, valuableThreshold);
            bool topHasLowValue = _settings.UnvaluableUpside.Value && HasAnyWeightAtOrBelowThreshold(weights, true, true, lowValueThreshold);
            bool bottomHasLowValue = _settings.UnvaluableUpside.Value && HasAnyWeightAtOrBelowThreshold(weights, false, true, lowValueThreshold);

            if (topHasHighValueUpside || bottomHasHighValueUpside)
            {
                return HandleHighValueOverride(topHasHighValueUpside, altar, topModsRect, bottomModsRect, textPos1, valuableThreshold);
            }

            if (topHasDangerousDownside && bottomHasDangerousDownside)
            {
                return HandleBothDangerousCase(altar, topModsRect, bottomModsRect, textPos1, dangerousThreshold);
            }

            if (topHasDangerousDownside || bottomHasDangerousDownside)
            {
                return HandleDangerousDownside(topHasDangerousDownside, altar, topModsRect, bottomModsRect, textPos1, dangerousThreshold);
            }

            if (topHasLowValue || bottomHasLowValue)
            {
                return HandleLowValueOverride(topHasLowValue, bottomHasLowValue, altar, topModsRect, bottomModsRect, textPos1, lowValueThreshold);
            }

            if (TryHandleMinWeightThreshold(weights, altar, topModsRect, bottomModsRect, textPos1, out Element? thresholdChoice))
            {
                return thresholdChoice;
            }

            return HandleNormalWeight(weights, altar, topModsRect, bottomModsRect, textPos2);
        }

        private bool TryHandleMinWeightThreshold(AltarWeights weights, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos, out Element? choice)
        {
            choice = null;
            if (!_settings.MinWeightThresholdEnabled.Value)
                return false;

            decimal minThreshold = _settings.MinWeightThreshold.Value;
            bool topBelowMin = weights.TopWeight < minThreshold;
            bool bottomBelowMin = weights.BottomWeight < minThreshold;

            if (topBelowMin && bottomBelowMin)
            {
                _deferredTextQueue.Enqueue($"Both options have final weights below the minimum threshold ({minThreshold}) - please choose manually.", textPos, Color.Orange, 30);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return true;
            }

            if (topBelowMin)
            {
                _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because top weight ({weights.TopWeight}) is below minimum {minThreshold}", textPos, Color.Yellow, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                choice = GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
                return true;
            }

            if (bottomBelowMin)
            {
                _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because bottom weight ({weights.BottomWeight}) is below minimum {minThreshold}", textPos, Color.Yellow, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
                choice = GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
                return true;
            }

            return false;
        }

        private Element? HandleBothDangerousCase(PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos, int dangerThreshold)
        {
            _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nBoth options have downsides with a weight of {dangerThreshold}+ that may brick your build.", textPos, Color.Orange, 30);
            _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
            _logMessage?.Invoke("[EvaluateAltarWeights] BOTH DANGEROUS CASE - both sides >= threshold", 10);
            _ = GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
            _ = GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            return null;
        }

        private Element? HandleHighValueOverride(bool topChosen, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos, int highValueThreshold)
        {
            if (topChosen)
            {
                _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of {highValueThreshold}+", textPos, Color.LawnGreen, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 3);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 2);
                return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
            }

            _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of {highValueThreshold}+", textPos, Color.LawnGreen, 30);
            _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 3);
            return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
        }

        private Element? HandleLowValueOverride(bool topHasLowValue, bool bottomHasLowValue, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos, int lowValueThreshold)
        {
            if (topHasLowValue && bottomHasLowValue)
            {
                _deferredTextQueue.Enqueue($"Both options have low value modifiers (weight <= {lowValueThreshold}), you should choose.", textPos, Color.Orange, 30);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (topHasLowValue)
            {
                _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nBottom has been chosen because top has a modifier with weight <= {lowValueThreshold}", textPos, Color.Yellow, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            }

            _deferredTextQueue.Enqueue($"Weighting has been overridden\n\nTop has been chosen because bottom has a modifier with weight <= {lowValueThreshold}", textPos, Color.Yellow, 30);
            _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
            return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
        }

        private Element? HandleDangerousDownside(bool topHasDanger, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos, int dangerThreshold)
        {
            if (topHasDanger)
            {
                _deferredTextQueue.Enqueue($"Weighting overridden\n\nBottom chosen due to top downside {dangerThreshold}+", textPos, Color.LawnGreen, 30);
                _deferredFrameQueue.Enqueue(topModsRect, Color.OrangeRed, 3);
                _deferredFrameQueue.Enqueue(bottomModsRect, Color.LawnGreen, 2);
                return GetValidatedButtonElement(altar.BottomButton, BOTTOM_BUTTON_NAME);
            }

            _deferredTextQueue.Enqueue($"Weighting overridden\n\nTop chosen due to bottom downside {dangerThreshold}+", textPos, Color.LawnGreen, 30);
            _deferredFrameQueue.Enqueue(topModsRect, Color.LawnGreen, 2);
            _deferredFrameQueue.Enqueue(bottomModsRect, Color.OrangeRed, 3);
            return GetValidatedButtonElement(altar.TopButton, TOP_BUTTON_NAME);
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

                Element? el = button.Element;
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

        private static bool HasAnyWeightOverThreshold(AltarWeights weights, bool isTop, bool isUpside, int threshold)
        {
            return HasAnyWeightMatching(weights, isTop, isUpside, w => w >= threshold);
        }

        private static bool HasAnyWeightAtOrBelowThreshold(AltarWeights weights, bool isTop, bool isUpside, int threshold)
        {
            return HasAnyWeightMatching(weights, isTop, isUpside, w => w > 0 && w <= threshold);
        }

        private static bool HasAnyWeightMatching(AltarWeights weights, bool isTop, bool isUpside, Func<decimal, bool> predicate)
        {
            decimal[] weightArray = GetWeightArray(weights, isTop, isUpside);
            for (int i = 0; i < weightArray.Length; i++)
            {
                if (predicate(weightArray[i]))
                    return true;
            }

            return false;
        }

        private static decimal[] GetWeightArray(AltarWeights weights, bool isTop, bool isUpside)
        {
            if (isUpside)
                return isTop ? weights.GetTopUpsideWeights() : weights.GetBottomUpsideWeights();

            return isTop ? weights.GetTopDownsideWeights() : weights.GetBottomDownsideWeights();
        }
    }
}