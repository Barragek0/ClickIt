namespace ClickIt.Features.Click.State
{
    internal sealed class GroundLabelEntityAddressProvider
    {
        private const int CacheWindowMs = 150;

        private readonly Func<IList<LabelOnGround>?> _getLabels;
        private readonly HashSet<long> _cachedGroundLabelEntityAddresses = [];
        private readonly TimedValueCache<int, IReadOnlySet<long>> _cachedGroundLabelEntityAddressesCache = new(
            CacheWindowMs,
            settings: new TimedValueCacheSettings(
                RequireNonNegativeAge: true,
                RequirePositiveCachedTimestamp: true));

        internal GroundLabelEntityAddressProvider(GameController gameController)
            : this(() => gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels)
        {
        }

        internal GroundLabelEntityAddressProvider(Func<IList<LabelOnGround>?> getLabels)
        {
            _getLabels = getLabels ?? throw new ArgumentNullException(nameof(getLabels));
        }

        internal IReadOnlySet<long> Collect()
        {
            try
            {
                IList<LabelOnGround>? labels = _getLabels();
                int labelCount = labels?.Count ?? 0;
                if (labels == null || labelCount == 0)
                {
                    _cachedGroundLabelEntityAddresses.Clear();
                    _cachedGroundLabelEntityAddressesCache.SetValue(0, Environment.TickCount64, _cachedGroundLabelEntityAddresses);
                    return _cachedGroundLabelEntityAddresses;
                }

                long now = Environment.TickCount64;
                if (_cachedGroundLabelEntityAddressesCache.TryGetValue(labelCount, now, out IReadOnlySet<long> cachedAddresses))
                {
                    if (!ReferenceEquals(cachedAddresses, _cachedGroundLabelEntityAddresses))
                    {
                        _cachedGroundLabelEntityAddresses.Clear();
                        _cachedGroundLabelEntityAddresses.UnionWith(cachedAddresses);
                    }

                    return _cachedGroundLabelEntityAddresses;
                }


                _cachedGroundLabelEntityAddresses.Clear();
                _cachedGroundLabelEntityAddresses.EnsureCapacity(labelCount);

                for (int i = 0; i < labelCount; i++)
                {
                    long address = TryResolveEntityAddress(labels[i]);
                    if (address != 0)
                        _cachedGroundLabelEntityAddresses.Add(address);
                }

                _cachedGroundLabelEntityAddressesCache.SetValue(labelCount, now, _cachedGroundLabelEntityAddresses);
            }
            catch
            {
            }

            return _cachedGroundLabelEntityAddresses;
        }

        private static long TryResolveEntityAddress(LabelOnGround? label)
        {
            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                || rawItem == null
                || !DynamicAccess.TryGetDynamicValue(rawItem, DynamicAccessProfiles.Address, out object? rawAddress)
                || rawAddress == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt64(rawAddress, global::System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }
    }
}