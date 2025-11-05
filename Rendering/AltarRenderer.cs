using ExileCore;
using ExileCore.PoEMemory;
using SharpDX;
using System.Collections.Generic;

#nullable enable

namespace ClickIt.Rendering
{
    /// <summary>
    /// Handles all altar rendering and UI display logic
    /// </summary>
    public class AltarRenderer
    {
        private readonly Graphics _graphics;
        private readonly ClickItSettings _settings;

        private const string ReportBugMessage = "\nPlease report this as a bug on github";

        public AltarRenderer(Graphics graphics, ClickItSettings settings)
        {
            _graphics = graphics;
            _settings = settings;
        }

        public void RenderAltarComponents(List<PrimaryAltarComponent> altarComponents, bool clickEater, bool clickExarch, bool leftHanded, Vector2 windowTopLeft, Services.AltarWeightCalculator calculator)
        {
            foreach (PrimaryAltarComponent altar in altarComponents)
            {
                RenderSingleAltar(altar, clickEater, clickExarch, leftHanded, windowTopLeft, calculator);
            }
        }

        private void RenderSingleAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch, bool leftHanded, Vector2 windowTopLeft, Services.AltarWeightCalculator calculator)
        {
            var altarWeights = calculator.CalculateAltarWeights(altar);
            RectangleF topModsRect = altar.TopMods.Element.GetClientRect();
            RectangleF bottomModsRect = altar.BottomMods.Element.GetClientRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;
            Vector2 bottomModsTopLeft = bottomModsRect.TopLeft;

            DetermineAltarChoice(altar, altarWeights, topModsRect, bottomModsRect, topModsTopLeft);

            DrawWeightTexts(altarWeights, topModsTopLeft, bottomModsTopLeft);
        }

        private Element? DetermineAltarChoice(PrimaryAltarComponent altar, Services.AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 offset120_Minus60 = new(120, -70);
            Vector2 offset120_Minus25 = new(120, -25);

            if (weights.TopUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top upside", altar.TopMods.FirstUpside, altar.TopMods.SecondUpside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.TopDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top downside", altar.TopMods.FirstDownside, altar.TopMods.SecondDownside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.BottomUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom upside", altar.BottomMods.FirstUpside, altar.BottomMods.SecondUpside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.BottomDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom downside", altar.BottomMods.FirstDownside, altar.BottomMods.SecondDownside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            return EvaluateAltarWeights(weights, altar, topModsRect, bottomModsRect, topModsTopLeft + offset120_Minus60, topModsTopLeft + offset120_Minus25);
        }

        private Element? EvaluateAltarWeights(Services.AltarWeights weights, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos1, Vector2 textPos2)
        {
            Color colorOrange = Color.Orange;
            Color colorLawnGreen = Color.LawnGreen;
            Color colorOrangeRed = Color.OrangeRed;

            if ((weights.TopDownside1Weight >= 90 || weights.TopDownside2Weight >= 90) && (weights.BottomDownside1Weight >= 90 || weights.BottomDownside2Weight >= 90))
            {
                _graphics.DrawText("Weighting has been overridden\n\nBoth options have downsides with a weight of 90+ that may brick your build.", textPos1, colorOrange, 30);
                _graphics.DrawFrame(topModsRect, colorOrangeRed, 2);
                _graphics.DrawFrame(bottomModsRect, colorOrangeRed, 2);
                return null;
            }

            if (weights.TopUpside1Weight >= 90 || weights.TopUpside2Weight >= 90)
            {
                _graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of 90+", textPos1, colorLawnGreen, 30);
                _graphics.DrawFrame(topModsRect, colorLawnGreen, 3);
                _graphics.DrawFrame(bottomModsRect, colorOrangeRed, 2);
                return altar.TopButton.Element;
            }

            if (weights.BottomUpside1Weight >= 90 || weights.BottomUpside2Weight >= 90)
            {
                _graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of 90+", textPos1, colorLawnGreen, 30);
                _graphics.DrawFrame(topModsRect, colorOrangeRed, 2);
                _graphics.DrawFrame(bottomModsRect, colorLawnGreen, 3);
                return altar.BottomButton.Element;
            }

            if (weights.TopDownside1Weight >= 90 || weights.TopDownside2Weight >= 90)
            {
                _graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the top downsides has a weight of 90+", textPos1, colorLawnGreen, 30);
                _graphics.DrawFrame(topModsRect, colorOrangeRed, 3);
                _graphics.DrawFrame(bottomModsRect, colorLawnGreen, 2);
                return altar.BottomButton.Element;
            }

            if (weights.BottomDownside1Weight >= 90 || weights.BottomDownside2Weight >= 90)
            {
                _graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the bottom downsides has a weight of 90+", textPos1, colorLawnGreen, 30);
                _graphics.DrawFrame(topModsRect, colorLawnGreen, 2);
                _graphics.DrawFrame(bottomModsRect, colorOrangeRed, 3);
                return altar.TopButton.Element;
            }

            if (weights.TopWeight > weights.BottomWeight)
            {
                _graphics.DrawFrame(topModsRect, colorLawnGreen, 3);
                _graphics.DrawFrame(bottomModsRect, colorOrangeRed, 2);
                return altar.TopButton.Element;
            }

            if (weights.BottomWeight > weights.TopWeight)
            {
                _graphics.DrawFrame(topModsRect, colorOrangeRed, 2);
                _graphics.DrawFrame(bottomModsRect, colorLawnGreen, 3);
                return altar.BottomButton.Element;
            }

            _graphics.DrawText("Mods have equal weight, you should choose.", textPos2, colorOrange, 30);
            DrawYellowFrames(topModsRect, bottomModsRect);
            return null;
        }

        private void DrawUnrecognizedWeightText(string weightType, string mod1, string mod2, Vector2 position)
        {
            _graphics.DrawText($"{weightType} weights couldn't be recognised\n1:{mod1}\n2:{mod2}{ReportBugMessage}", position, Color.Orange, 30);
        }

        private void DrawYellowFrames(RectangleF topModsRect, RectangleF bottomModsRect)
        {
            _graphics.DrawFrame(topModsRect, Color.Yellow, 2);
            _graphics.DrawFrame(bottomModsRect, Color.Yellow, 2);
        }

        private void DrawWeightTexts(Services.AltarWeights weights, Vector2 topModsTopLeft, Vector2 bottomModsTopLeft)
        {
            Vector2 offset5_Minus32 = new(5, -32);
            Vector2 offset5_Minus20 = new(5, -20);
            Vector2 offset10_Minus32 = new(10, -32);
            Vector2 offset10_Minus20 = new(10, -20);
            Vector2 offset10_5 = new(10, 5);
            Color colorLawnGreen = Color.LawnGreen;
            Color colorOrangeRed = Color.OrangeRed;
            Color colorYellow = Color.Yellow;

            _graphics.DrawText("Upside: " + weights.TopUpsideWeight, topModsTopLeft + offset5_Minus32, colorLawnGreen, 14);
            _graphics.DrawText("Downside: " + weights.TopDownsideWeight, topModsTopLeft + offset5_Minus20, colorOrangeRed, 14);
            _graphics.DrawText("Upside: " + weights.BottomUpsideWeight, bottomModsTopLeft + offset10_Minus32, colorLawnGreen, 14);
            _graphics.DrawText("Downside: " + weights.BottomDownsideWeight, bottomModsTopLeft + offset10_Minus20, colorOrangeRed, 14);

            Color topWeightColor = GetWeightColor(weights.TopWeight, weights.BottomWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            Color bottomWeightColor = GetWeightColor(weights.BottomWeight, weights.TopWeight, colorLawnGreen, colorOrangeRed, colorYellow);

            _graphics.DrawText("" + weights.TopWeight, topModsTopLeft + offset10_5, topWeightColor, 18);
            _graphics.DrawText("" + weights.BottomWeight, bottomModsTopLeft + offset10_5, bottomWeightColor, 18);
        }

        private static Color GetWeightColor(decimal weight1, decimal weight2, Color winColor, Color loseColor, Color tieColor)
        {
            if (weight1 > weight2) return winColor;
            if (weight2 > weight1) return loseColor;
            return tieColor;
        }

        public void RenderDebugFrames(RectangleF fullScreenRectangle, RectangleF healthAndFlaskRectangle, RectangleF manaAndSkillsRectangle, RectangleF buffsAndDebuffsRectangle)
        {
            bool debugMode = _settings.DebugMode;
            bool renderDebug = _settings.RenderDebug;

            if (debugMode && renderDebug)
            {
                _graphics.DrawFrame(fullScreenRectangle, Color.Green, 1);
                _graphics.DrawFrame(healthAndFlaskRectangle, Color.Orange, 1);
                _graphics.DrawFrame(manaAndSkillsRectangle, Color.Cyan, 1);
                _graphics.DrawFrame(buffsAndDebuffsRectangle, Color.Yellow, 1);
            }
        }
    }
}