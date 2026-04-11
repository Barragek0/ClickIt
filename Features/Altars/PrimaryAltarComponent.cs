namespace ClickIt.Features.Altars
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

        private const int ValidationCacheKey = 0;
        private const int WeightCacheKey = 0;
        private readonly Stopwatch _cacheTimer;
        private const long CACHE_DURATION_MS = 1000;
        private const long WEIGHT_CACHE_DURATION_MS = 5000;
        private readonly TimedValueCache<int, bool> _validityCache = new(CACHE_DURATION_MS);
        private readonly TimedValueCache<int, AltarWeights> _weightsCache = new(WEIGHT_CACHE_DURATION_MS);

        // Thread safety lock for cache operations
        private readonly object _cacheLock = new();

        private T WithCacheLock<T>(Func<T> func)
        {
            using (LockManager.AcquireStatic(_cacheLock))
            {
                return func();
            }
        }

        private void WithCacheLock(Action action)
        {
            using (LockManager.AcquireStatic(_cacheLock))
            {
                action();
            }
        }

        public bool IsValidCached()
        {
            return WithCacheLock(() =>
            {
                long currentTime = _cacheTimer.ElapsedMilliseconds;
                if (_validityCache.TryGetValue(ValidationCacheKey, currentTime, out bool cachedValidity))
                    return cachedValidity;


                bool isValid = TopMods?.Element?.IsValid == true && BottomMods?.Element?.IsValid == true &&
                              !TopMods.HasUnmatchedMods && !BottomMods.HasUnmatchedMods;

                _validityCache.SetValue(ValidationCacheKey, currentTime, isValid);
                return isValid;
            });
        }

        public AltarWeights? GetCachedWeights(Func<PrimaryAltarComponent, AltarWeights> weightCalculator)
        {
            return WithCacheLock(() =>
            {
                long currentTime = _cacheTimer.ElapsedMilliseconds;
                if (_weightsCache.TryGetValue(WeightCacheKey, currentTime, out AltarWeights cachedWeights))
                    return cachedWeights;


                AltarWeights weights = weightCalculator(this);
                _weightsCache.SetValue(WeightCacheKey, currentTime, weights);
                return weights;
            });
        }

        private static RectangleF GetModsRect(SecondaryAltarComponent? mods, string name)
        {
            if (mods?.Element == null)
                throw new InvalidOperationException($"Cannot get {name} mods rect - element is null");


            if (!mods.Element.IsValid)
                throw new InvalidOperationException($"Cannot get {name} mods rect - element is not valid");


            return mods.Element.GetClientRect();
        }

        public RectangleF GetTopModsRect() => GetModsRect(TopMods, "top");

        public RectangleF GetBottomModsRect() => GetModsRect(BottomMods, "bottom");

        public void InvalidateCache()
        {
            WithCacheLock(() =>
            {
                _validityCache.Invalidate();
                _weightsCache.Invalidate();
            });
        }
    }
}
