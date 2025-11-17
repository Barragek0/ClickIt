using static ClickIt.ClickIt;
using SharpDX;
using System;
using System.Diagnostics;
using ClickIt.Utils;
using RectangleF = SharpDX.RectangleF;
namespace ClickIt.Components
{
#nullable enable
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

        private bool? _isValidCache;
        private long _lastValidationTime;
        private AltarWeights? _cachedWeights;
        private long _lastWeightCalculationTime;
        private readonly Stopwatch _cacheTimer;
        private const long CACHE_DURATION_MS = 1000;

        // Thread safety lock for cache operations
        private readonly object _cacheLock = new object();

        // Helper to execute a Func<T> under the configured LockManager if present
        private T WithCacheLock<T>(Func<T> func)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_cacheLock))
                {
                    return func();
                }
            }

            return func();
        }

        // Helper to execute an Action under the configured LockManager if present
        private void WithCacheLock(Action action)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_cacheLock))
                {
                    action();
                }
            }
            else
            {
                action();
            }
        }

        public bool IsValidCached()
        {
            return WithCacheLock(() =>
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
            });
        }

        public AltarWeights? GetCachedWeights(Func<PrimaryAltarComponent, AltarWeights> weightCalculator)
        {
            return WithCacheLock(() =>
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
            });
        }

        private RectangleF GetModsRect(SecondaryAltarComponent? mods, string name)
        {
            if (mods?.Element == null)
            {
                throw new InvalidOperationException($"Cannot get {name} mods rect - element is null");
            }

            if (!mods.Element.IsValid)
            {
                throw new InvalidOperationException($"Cannot get {name} mods rect - element is not valid");
            }

            return mods.Element.GetClientRect();
        }

        public RectangleF GetTopModsRect() => GetModsRect(TopMods, "top");

        public RectangleF GetBottomModsRect() => GetModsRect(BottomMods, "bottom");

        public void InvalidateCache()
        {
            WithCacheLock(() =>
            {
                _isValidCache = null;
                _cachedWeights = null;
            });
        }
    }
}
