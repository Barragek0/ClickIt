using static ClickIt.ClickIt;
using SharpDX;
using System;
using System.Diagnostics;
namespace ClickIt.Components
{
#nullable enable
    public class PrimaryAltarComponent
    {
        public PrimaryAltarComponent(AltarType AltarType, SecondaryAltarComponent? TopMods, AltarButton? TopButton, SecondaryAltarComponent? BottomMods, AltarButton? BottomButton)
        {
            this.AltarType = AltarType;
            this.TopMods = TopMods;
            this.TopButton = TopButton;
            this.BottomMods = BottomMods;
            this.BottomButton = BottomButton;
            _cacheTimer = new Stopwatch();
            _cacheTimer.Start();
        }
        public AltarType? AltarType { get; set; }
        public SecondaryAltarComponent? TopMods { get; set; }
        public AltarButton? TopButton { get; set; }
        public SecondaryAltarComponent? BottomMods { get; set; }
        public AltarButton? BottomButton { get; set; }

        private bool? _isValidCache;
        private long _lastValidationTime;
        private Utils.AltarWeights? _cachedWeights;
        private long _lastWeightCalculationTime;
        private readonly Stopwatch _cacheTimer;
        private const long CACHE_DURATION_MS = 100;

        // Thread safety lock for cache operations
        private readonly object _cacheLock = new object();

        public bool IsValidCached()
        {
            var gm = Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_cacheLock))
                {
                    long currentTime = _cacheTimer.ElapsedMilliseconds;
                    if (_isValidCache.HasValue && (currentTime - _lastValidationTime) < CACHE_DURATION_MS)
                    {
                        return _isValidCache.Value;
                    }

                    bool isValid = TopMods?.Element?.IsValid == true && BottomMods?.Element?.IsValid == true &&
                                  !TopMods.HasUnmatchedMods && !BottomMods.HasUnmatchedMods;

                    _isValidCache = isValid;
                    _lastValidationTime = currentTime;
                    return isValid;
                }
            }
            else
            {
                long currentTime = _cacheTimer.ElapsedMilliseconds;
                if (_isValidCache.HasValue && (currentTime - _lastValidationTime) < CACHE_DURATION_MS)
                {
                    return _isValidCache.Value;
                }

                bool isValid = TopMods?.Element?.IsValid == true && BottomMods?.Element?.IsValid == true &&
                              !TopMods.HasUnmatchedMods && !BottomMods.HasUnmatchedMods;

                _isValidCache = isValid;
                _lastValidationTime = currentTime;
                return isValid;
            }
        }

        public Utils.AltarWeights? GetCachedWeights(Func<PrimaryAltarComponent, Utils.AltarWeights> weightCalculator)
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_cacheLock))
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
            }
            else
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
        }

        public RectangleF GetTopModsRect()
        {
            if (TopMods?.Element == null)
            {
                throw new InvalidOperationException("Cannot get top mods rect - element is null");
            }

            if (!TopMods.Element.IsValid)
            {
                throw new InvalidOperationException("Cannot get top mods rect - element is not valid");
            }

            return TopMods.Element.GetClientRect();
        }

        public RectangleF GetBottomModsRect()
        {
            if (BottomMods?.Element == null)
            {
                throw new InvalidOperationException("Cannot get bottom mods rect - element is null");
            }

            if (!BottomMods.Element.IsValid)
            {
                throw new InvalidOperationException("Cannot get bottom mods rect - element is not valid");
            }

            return BottomMods.Element.GetClientRect();
        }

        // Legacy method that now directly returns rectangles without caching
        public (RectangleF topRect, RectangleF bottomRect) GetCachedRects()
        {
            return (GetTopModsRect(), GetBottomModsRect());
        }

        public void InvalidateCache()
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_cacheLock))
                {
                    _isValidCache = null;
                    _cachedWeights = null;
                }
            }
            else
            {
                _isValidCache = null;
                _cachedWeights = null;
            }
        }
    }
}
