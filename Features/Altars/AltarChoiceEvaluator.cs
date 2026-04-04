#nullable enable

namespace ClickIt.Features.Altars
{
    internal enum AltarChoiceOutcome
    {
        InvalidRectangles,
        UnmatchedMods,
        UnrecognizedTopUpside,
        UnrecognizedTopDownside,
        UnrecognizedBottomUpside,
        UnrecognizedBottomDownside,
        HighValueTopChosen,
        HighValueBottomChosen,
        BothDangerousManual,
        DangerousTopChooseBottom,
        DangerousBottomChooseTop,
        BothLowValueManual,
        TopLowValueChooseBottom,
        BottomLowValueChooseTop,
        BothBelowMinimumManual,
        TopBelowMinimumChooseBottom,
        BottomBelowMinimumChooseTop,
        TopWeightHigher,
        BottomWeightHigher,
        EqualWeightsManual
    }

    internal readonly record struct AltarChoiceEvaluation(
        AltarChoiceOutcome Outcome,
        Element? ChosenElement = null,
        decimal Threshold = 0,
        decimal TopWeight = 0,
        decimal BottomWeight = 0,
        string? UnrecognizedWeightType = null,
        string[]? UnrecognizedMods = null);

    public sealed class AltarChoiceEvaluator(ClickItSettings settings, Action<string, int>? logMessage = null)
    {
        private const string TopButtonName = "TopButton";
        private const string BottomButtonName = "BottomButton";

        private readonly ClickItSettings _settings = settings;
        private readonly Action<string, int> _logMessage = logMessage ?? ((_, _) => { });

        internal Element? DetermineChoiceElement(PrimaryAltarComponent altar, AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect)
            => EvaluateChoice(altar, weights, topModsRect, bottomModsRect).ChosenElement;

        internal AltarChoiceEvaluation EvaluateChoice(PrimaryAltarComponent altar, AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect)
        {
            if (!IsValidRectangles(topModsRect, bottomModsRect))
                return new(AltarChoiceOutcome.InvalidRectangles);

            if ((altar.TopMods?.HasUnmatchedMods ?? false) || (altar.BottomMods?.HasUnmatchedMods ?? false))
                return new(AltarChoiceOutcome.UnmatchedMods);

            if (weights.TopUpsideWeight <= 0)
                return CreateUnrecognizedWeightEvaluation(AltarChoiceOutcome.UnrecognizedTopUpside, "Top upside", altar.TopMods?.GetAllUpsides());

            if (weights.TopDownsideWeight <= 0)
                return CreateUnrecognizedWeightEvaluation(AltarChoiceOutcome.UnrecognizedTopDownside, "Top downside", altar.TopMods?.GetAllDownsides());

            if (weights.BottomUpsideWeight <= 0)
                return CreateUnrecognizedWeightEvaluation(AltarChoiceOutcome.UnrecognizedBottomUpside, "Bottom upside", altar.BottomMods?.GetAllUpsides());

            if (weights.BottomDownsideWeight <= 0)
                return CreateUnrecognizedWeightEvaluation(AltarChoiceOutcome.UnrecognizedBottomDownside, "Bottom downside", altar.BottomMods?.GetAllDownsides());

            return EvaluateWeights(altar, weights);
        }

        private AltarChoiceEvaluation EvaluateWeights(PrimaryAltarComponent altar, AltarWeights weights)
        {
            int dangerousThreshold = _settings.DangerousDownsideThreshold.Value;
            int valuableThreshold = _settings.ValuableUpsideThreshold.Value;
            int lowValueThreshold = _settings.UnvaluableUpsideThreshold.Value;

            bool topHasDangerousDownside = _settings.DangerousDownside.Value && HasAnyWeightOverThreshold(weights, isTop: true, isUpside: false, dangerousThreshold);
            bool bottomHasDangerousDownside = _settings.DangerousDownside.Value && HasAnyWeightOverThreshold(weights, isTop: false, isUpside: false, dangerousThreshold);
            bool topHasHighValueUpside = _settings.ValuableUpside.Value && HasAnyWeightOverThreshold(weights, isTop: true, isUpside: true, valuableThreshold);
            bool bottomHasHighValueUpside = _settings.ValuableUpside.Value && HasAnyWeightOverThreshold(weights, isTop: false, isUpside: true, valuableThreshold);
            bool topHasLowValue = _settings.UnvaluableUpside.Value && HasAnyWeightAtOrBelowThreshold(weights, isTop: true, isUpside: true, lowValueThreshold);
            bool bottomHasLowValue = _settings.UnvaluableUpside.Value && HasAnyWeightAtOrBelowThreshold(weights, isTop: false, isUpside: true, lowValueThreshold);

            if (topHasHighValueUpside)
                return CreateChoiceEvaluation(AltarChoiceOutcome.HighValueTopChosen, altar.TopButton, TopButtonName, valuableThreshold);

            if (bottomHasHighValueUpside)
                return CreateChoiceEvaluation(AltarChoiceOutcome.HighValueBottomChosen, altar.BottomButton, BottomButtonName, valuableThreshold);

            if (topHasDangerousDownside && bottomHasDangerousDownside)
                return new(AltarChoiceOutcome.BothDangerousManual, Threshold: dangerousThreshold);

            if (topHasDangerousDownside)
                return CreateChoiceEvaluation(AltarChoiceOutcome.DangerousTopChooseBottom, altar.BottomButton, BottomButtonName, dangerousThreshold);

            if (bottomHasDangerousDownside)
                return CreateChoiceEvaluation(AltarChoiceOutcome.DangerousBottomChooseTop, altar.TopButton, TopButtonName, dangerousThreshold);

            if (topHasLowValue && bottomHasLowValue)
                return new(AltarChoiceOutcome.BothLowValueManual, Threshold: lowValueThreshold);

            if (topHasLowValue)
                return CreateChoiceEvaluation(AltarChoiceOutcome.TopLowValueChooseBottom, altar.BottomButton, BottomButtonName, lowValueThreshold);

            if (bottomHasLowValue)
                return CreateChoiceEvaluation(AltarChoiceOutcome.BottomLowValueChooseTop, altar.TopButton, TopButtonName, lowValueThreshold);

            if (_settings.MinWeightThresholdEnabled.Value)
            {
                decimal minThreshold = _settings.MinWeightThreshold.Value;
                bool topBelowMin = weights.TopWeight < minThreshold;
                bool bottomBelowMin = weights.BottomWeight < minThreshold;

                if (topBelowMin && bottomBelowMin)
                    return new(AltarChoiceOutcome.BothBelowMinimumManual, Threshold: minThreshold, TopWeight: weights.TopWeight, BottomWeight: weights.BottomWeight);

                if (topBelowMin)
                    return CreateChoiceEvaluation(AltarChoiceOutcome.TopBelowMinimumChooseBottom, altar.BottomButton, BottomButtonName, minThreshold, weights.TopWeight, weights.BottomWeight);

                if (bottomBelowMin)
                    return CreateChoiceEvaluation(AltarChoiceOutcome.BottomBelowMinimumChooseTop, altar.TopButton, TopButtonName, minThreshold, weights.TopWeight, weights.BottomWeight);
            }

            if (weights.TopWeight > weights.BottomWeight)
                return CreateChoiceEvaluation(AltarChoiceOutcome.TopWeightHigher, altar.TopButton, TopButtonName, topWeight: weights.TopWeight, bottomWeight: weights.BottomWeight);

            if (weights.BottomWeight > weights.TopWeight)
                return CreateChoiceEvaluation(AltarChoiceOutcome.BottomWeightHigher, altar.BottomButton, BottomButtonName, topWeight: weights.TopWeight, bottomWeight: weights.BottomWeight);

            return new(AltarChoiceOutcome.EqualWeightsManual, TopWeight: weights.TopWeight, BottomWeight: weights.BottomWeight);
        }

        private AltarChoiceEvaluation CreateChoiceEvaluation(AltarChoiceOutcome outcome, AltarButton? preferredButton, string buttonName, decimal threshold = 0, decimal topWeight = 0, decimal bottomWeight = 0)
            => new(outcome, GetValidatedButtonElement(preferredButton, buttonName), threshold, topWeight, bottomWeight);

        private static AltarChoiceEvaluation CreateUnrecognizedWeightEvaluation(AltarChoiceOutcome outcome, string weightType, string[]? mods)
            => new(outcome, UnrecognizedWeightType: weightType, UnrecognizedMods: mods ?? []);

        private Element? GetValidatedButtonElement(AltarButton? button, string buttonName)
        {
            if (button == null)
            {
                _logMessage($"[AltarChoiceEvaluator] CRITICAL: {buttonName} is null", 10);
                return null;
            }

            Element? element = button.Element;
            if (element == null)
            {
                _logMessage($"[AltarChoiceEvaluator] CRITICAL: {buttonName}.Element is null", 10);
                return null;
            }

            if (!element.IsValid)
            {
                _logMessage($"[AltarChoiceEvaluator] CRITICAL: {buttonName}.Element is not valid", 10);
                return null;
            }

            return element;
        }

        private static bool IsValidRectangle(RectangleF rect)
            => rect.Width > 0
                && rect.Height > 0
                && !float.IsNaN(rect.X)
                && !float.IsNaN(rect.Y)
                && !float.IsInfinity(rect.X)
                && !float.IsInfinity(rect.Y);

        private static bool IsValidRectangles(RectangleF first, RectangleF second)
            => IsValidRectangle(first) && IsValidRectangle(second);

        private static bool HasAnyWeightOverThreshold(AltarWeights weights, bool isTop, bool isUpside, int threshold)
            => HasAnyWeightMatching(weights, isTop, isUpside, weight => weight >= threshold);

        private static bool HasAnyWeightAtOrBelowThreshold(AltarWeights weights, bool isTop, bool isUpside, int threshold)
            => HasAnyWeightMatching(weights, isTop, isUpside, weight => weight > 0 && weight <= threshold);

        private static bool HasAnyWeightMatching(AltarWeights weights, bool isTop, bool isUpside, Func<decimal, bool> predicate)
        {
            decimal[] values = isTop
                ? (isUpside ? weights.GetTopUpsideWeights() : weights.GetTopDownsideWeights())
                : (isUpside ? weights.GetBottomUpsideWeights() : weights.GetBottomDownsideWeights());

            for (int i = 0; i < values.Length; i++)
            {
                if (predicate(values[i]))
                    return true;
            }

            return false;
        }
    }
}