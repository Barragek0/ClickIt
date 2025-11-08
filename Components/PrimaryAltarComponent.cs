using static ClickIt.ClickIt;
using SharpDX;
using System;
using System.Diagnostics;
namespace ClickIt.Components
{
    public class PrimaryAltarComponent
    {
        public PrimaryAltarComponent(AltarType AltarType, SecondaryAltarComponent TopMods, AltarButton TopButton, SecondaryAltarComponent BottomMods, AltarButton BottomButton)
        {
            this.AltarType = AltarType;
            this.TopMods = TopMods;
            this.TopButton = TopButton;
            this.BottomMods = BottomMods;
            this.BottomButton = BottomButton;
            _cacheTimer = new Stopwatch();
            _cacheTimer.Start();
        }
        public AltarType AltarType { get; set; }
        public SecondaryAltarComponent TopMods { get; set; }
        public AltarButton TopButton { get; set; }
        public SecondaryAltarComponent BottomMods { get; set; }
        public AltarButton BottomButton { get; set; }

        // Performance optimization: Cache validation and calculation results
        private bool? _isValidCache;
        private long _lastValidationTime;
        private Utils.AltarWeights? _cachedWeights;
        private long _lastWeightCalculationTime;
        private RectangleF? _cachedTopModsRect;
        private RectangleF? _cachedBottomModsRect;
        private long _lastRectCalculationTime;
        private readonly Stopwatch _cacheTimer;
        private const long CACHE_DURATION_MS = 100; // Cache for 100ms

        public bool IsValidCached()
        {
            long currentTime = _cacheTimer.ElapsedMilliseconds;
            if (_isValidCache.HasValue && (currentTime - _lastValidationTime) < CACHE_DURATION_MS)
            {
                return _isValidCache.Value;
            }

            bool isValid = TopMods?.Element != null && BottomMods?.Element != null &&
                          TopMods.Element.IsValid && BottomMods.Element.IsValid &&
                          !TopMods.HasUnmatchedMods && !BottomMods.HasUnmatchedMods;

            _isValidCache = isValid;
            _lastValidationTime = currentTime;
            return isValid;
        }

        public Utils.AltarWeights? GetCachedWeights(Func<PrimaryAltarComponent, Utils.AltarWeights> weightCalculator)
        {
            long currentTime = _cacheTimer.ElapsedMilliseconds;
            if (_cachedWeights.HasValue && (currentTime - _lastWeightCalculationTime) < CACHE_DURATION_MS)
            {
                return _cachedWeights.Value;
            }

            var weights = weightCalculator(this);
            _cachedWeights = weights;
            _lastWeightCalculationTime = currentTime;
            return weights;
        }

        public (RectangleF topRect, RectangleF bottomRect) GetCachedRects()
        {
            long currentTime = _cacheTimer.ElapsedMilliseconds;
            if (_cachedTopModsRect.HasValue && _cachedBottomModsRect.HasValue &&
                (currentTime - _lastRectCalculationTime) < CACHE_DURATION_MS)
            {
                return (_cachedTopModsRect.Value, _cachedBottomModsRect.Value);
            }

            var topRect = TopMods.Element.GetClientRect();
            var bottomRect = BottomMods.Element.GetClientRect();

            _cachedTopModsRect = topRect;
            _cachedBottomModsRect = bottomRect;
            _lastRectCalculationTime = currentTime;

            return (topRect, bottomRect);
        }

        public void InvalidateCache()
        {
            _isValidCache = null;
            _cachedWeights = null;
            _cachedTopModsRect = null;
            _cachedBottomModsRect = null;
        }
    }
}
