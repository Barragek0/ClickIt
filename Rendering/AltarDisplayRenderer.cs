using ClickIt.Components;
using ClickIt.Utils;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;
using SharpDX;
using System;

#nullable enable

namespace ClickIt.Rendering
{
    public class AltarDisplayRenderer
    {
        private readonly ExileCore.Graphics _graphics;

        public AltarDisplayRenderer(ExileCore.Graphics graphics, ClickItSettings settings)
        {
            _graphics = graphics;
        }
        public Element? DetermineAltarChoice(PrimaryAltarComponent altar, Utils.AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 offset120_Minus60 = new(120, -70);
            Vector2 offset120_Minus25 = new(120, -25);

            // Validate rectangles before proceeding
            if (!IsValidRectangle(topModsRect) || !IsValidRectangle(bottomModsRect))
            {
                _graphics?.DrawText("Invalid altar rectangles detected", topModsTopLeft + offset120_Minus60, Color.Red, 30);
                return null;
            }

            // Check for unmatched mods first
            if (altar.TopMods.HasUnmatchedMods || altar.BottomMods.HasUnmatchedMods)
            {
                DrawFailedToMatchModText(topModsTopLeft + offset120_Minus60);
                DrawRedFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.TopUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top upside", altar.TopMods.FirstUpside, altar.TopMods.SecondUpside, altar.TopMods.ThirdUpside, altar.TopMods.FourthUpside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.TopDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top downside", altar.TopMods.FirstDownside, altar.TopMods.SecondDownside, altar.TopMods.ThirdDownside, altar.TopMods.FourthDownside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.BottomUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom upside", altar.BottomMods.FirstUpside, altar.BottomMods.SecondUpside, altar.BottomMods.ThirdUpside, altar.BottomMods.FourthUpside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            if (weights.BottomDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom downside", altar.BottomMods.FirstDownside, altar.BottomMods.SecondDownside, altar.BottomMods.ThirdDownside, altar.BottomMods.FourthDownside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }
            return EvaluateAltarWeights(weights, altar, topModsRect, bottomModsRect, topModsTopLeft + offset120_Minus60, topModsTopLeft + offset120_Minus25);
        }
        private Element? EvaluateAltarWeights(Utils.AltarWeights weights, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos1, Vector2 textPos2)
        {
            if ((weights.TopDownside1Weight >= 90 || weights.TopDownside2Weight >= 90 || weights.TopDownside3Weight >= 90 || weights.TopDownside4Weight >= 90) &&
                (weights.BottomDownside1Weight >= 90 || weights.BottomDownside2Weight >= 90 || weights.BottomDownside3Weight >= 90 || weights.BottomDownside4Weight >= 90))
            {
                _ = _graphics.DrawText("Weighting has been overridden\n\nBoth options have downsides with a weight of 90+ that may brick your build.", textPos1, Color.Orange, 30);
                _graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                _graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return null;
            }
            if (weights.TopUpside1Weight >= 90 || weights.TopUpside2Weight >= 90 || weights.TopUpside3Weight >= 90 || weights.TopUpside4Weight >= 90)
            {
                _ = _graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                _graphics.DrawFrame(topModsRect, Color.LawnGreen, 3);
                _graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return altar.TopButton.Element;
            }
            if (weights.BottomUpside1Weight >= 90 || weights.BottomUpside2Weight >= 90 || weights.BottomUpside3Weight >= 90 || weights.BottomUpside4Weight >= 90)
            {
                _ = _graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                _graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                _graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 3);
                return altar.BottomButton.Element;
            }
            if (weights.TopDownside1Weight >= 90 || weights.TopDownside2Weight >= 90 || weights.TopDownside3Weight >= 90 || weights.TopDownside4Weight >= 90)
            {
                _ = _graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the top downsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                _graphics.DrawFrame(topModsRect, Color.OrangeRed, 3);
                _graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 2);
                return altar.BottomButton.Element;
            }
            if (weights.BottomDownside1Weight >= 90 || weights.BottomDownside2Weight >= 90 || weights.BottomDownside3Weight >= 90 || weights.BottomDownside4Weight >= 90)
            {
                _ = _graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the bottom downsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                _graphics.DrawFrame(topModsRect, Color.LawnGreen, 2);
                _graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 3);
                return altar.TopButton.Element;
            }
            if (weights.TopWeight > weights.BottomWeight)
            {
                _graphics.DrawFrame(topModsRect, Color.LawnGreen, 3);
                _graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return altar.TopButton.Element;
            }
            if (weights.BottomWeight > weights.TopWeight)
            {
                _graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                _graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 3);
                return altar.BottomButton.Element;
            }
            _ = _graphics.DrawText("Mods have equal weight, you should choose.", textPos2, Color.Orange, 30);
            DrawYellowFrames(topModsRect, bottomModsRect);
            return null;
        }
        private void DrawUnrecognizedWeightText(string weightType, string mod1, string mod2, string mod3, string mod4, Vector2 position)
        {
            if (_graphics == null) return;
            string modsText = $"1:{mod1}";
            if (!string.IsNullOrEmpty(mod2)) modsText += $"\n2:{mod2}";
            if (!string.IsNullOrEmpty(mod3)) modsText += $"\n3:{mod3}";
            if (!string.IsNullOrEmpty(mod4)) modsText += $"\n4:{mod4}";
            _ = _graphics.DrawText($"{weightType} weights couldn't be recognised\n{modsText}\nPlease report this as a bug on github", position, Color.Orange, 30);
        }
        private void DrawFailedToMatchModText(Vector2 position)
        {
            if (_graphics == null) return;
            _ = _graphics.DrawText("Failed to match mod - unable to determine best choice.\nPlease report this as a bug on github", position, Color.Red, 30);
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
        private static bool IsValidRectangle(RectangleF rect)
        {
            return rect.Width > 0 && rect.Height > 0 && !float.IsNaN(rect.X) && !float.IsNaN(rect.Y) && !float.IsNaN(rect.Width) && !float.IsNaN(rect.Height);
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
            _ = _graphics.DrawText("Upside: " + weights.TopUpsideWeight, topModsTopLeft + offset5_Minus32, colorLawnGreen, 14);
            _ = _graphics.DrawText("Downside: " + weights.TopDownsideWeight, topModsTopLeft + offset5_Minus20, colorOrangeRed, 14);
            _ = _graphics.DrawText("Upside: " + weights.BottomUpsideWeight, bottomModsTopLeft + offset10_Minus32, colorLawnGreen, 14);
            _ = _graphics.DrawText("Downside: " + weights.BottomDownsideWeight, bottomModsTopLeft + offset10_Minus20, colorOrangeRed, 14);
            Color topWeightColor = GetWeightColor(weights.TopWeight, weights.BottomWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            Color bottomWeightColor = GetWeightColor(weights.BottomWeight, weights.TopWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            _ = _graphics.DrawText("" + weights.TopWeight, topModsTopLeft + offset10_5, topWeightColor, 18);
            _ = _graphics.DrawText("" + weights.BottomWeight, bottomModsTopLeft + offset10_5, bottomWeightColor, 18);
        }
        private static Color GetWeightColor(decimal weight1, decimal weight2, Color winColor, Color loseColor, Color tieColor)
        {
            if (weight1 > weight2) return winColor;
            if (weight2 > weight1) return loseColor;
            return tieColor;
        }
    }
}
