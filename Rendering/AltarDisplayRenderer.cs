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
    public partial class AltarDisplayRenderer(Graphics graphics, ClickItSettings settings, GameController gameController, WeightCalculator weightCalculator, DeferredTextQueue deferredTextQueue, DeferredFrameQueue deferredFrameQueue, AltarService? altarService = null, Action<string, int>? logMessage = null)
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

            if (!IsValidRectangles(topModsRect, bottomModsRect))
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
    }
}
