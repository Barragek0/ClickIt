namespace ClickIt.Shared.Game
{
    internal readonly record struct TimedValueCacheSettings(
        bool RequireNonNegativeAge = false,
        bool RequirePositiveCachedTimestamp = false);

    internal sealed class TimedValueCache<TKey, TValue>(
        long windowMs,
        IEqualityComparer<TKey>? keyComparer = null,
        TimedValueCacheSettings settings = default)
    {
        private readonly Lock _gate = new();
        private readonly long _windowMs = windowMs;
        private readonly IEqualityComparer<TKey> _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
        private readonly TimedValueCacheSettings _settings = settings;

        private bool _hasCachedValue;
        private TKey _cachedKey = default!;
        private long _cachedAtMs;
        private TValue _cachedValue = default!;

        public bool TryGetValue(TKey key, long nowMs, out TValue value)
        {
            lock (_gate)
            {
                if (!_hasCachedValue || _windowMs <= 0)
                {
                    value = default!;
                    return false;
                }

                if (!_keyComparer.Equals(_cachedKey, key))
                {
                    value = default!;
                    return false;
                }

                if (_settings.RequirePositiveCachedTimestamp && _cachedAtMs <= 0)
                {
                    value = default!;
                    return false;
                }

                long ageMs = nowMs - _cachedAtMs;
                bool isFresh = (!_settings.RequireNonNegativeAge || ageMs >= 0)
                    && ageMs <= _windowMs;
                if (!isFresh)
                {
                    value = default!;
                    return false;
                }

                value = _cachedValue;
                return true;
            }
        }

        public void SetValue(TKey key, long nowMs, TValue value)
        {
            lock (_gate)
            {
                _hasCachedValue = true;
                _cachedKey = key;
                _cachedAtMs = nowMs;
                _cachedValue = value;
            }
        }

        public void Invalidate()
        {
            lock (_gate)
            {
                _hasCachedValue = false;
                _cachedKey = default!;
                _cachedAtMs = 0;
                _cachedValue = default!;
            }
        }

        public bool HasCachedValue
        {
            get
            {
                lock (_gate)
                {
                    return _hasCachedValue;
                }
            }
        }
    }
}