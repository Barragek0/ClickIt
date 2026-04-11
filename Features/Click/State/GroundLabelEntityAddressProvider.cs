namespace ClickIt.Features.Click.State
{
    internal sealed class GroundLabelEntityAddressProvider
    {
        private const int CacheWindowMs = 150;
        private const int UninitializedLabelCount = -1;

        private readonly Func<IList<LabelOnGround>?> _getLabels;
        private readonly HashSet<long> _cachedGroundLabelEntityAddresses = [];
        private long _cachedGroundLabelEntityAddressesTimestampMs;
        private int _cachedGroundLabelEntityLabelCount = UninitializedLabelCount;

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
                    _cachedGroundLabelEntityLabelCount = 0;
                    _cachedGroundLabelEntityAddressesTimestampMs = Environment.TickCount64;
                    return _cachedGroundLabelEntityAddresses;
                }

                long now = Environment.TickCount64;
                if (VisibleMechanicCachePolicy.ShouldReuseTimedLabelCountCache(
                        now,
                        _cachedGroundLabelEntityAddressesTimestampMs,
                        _cachedGroundLabelEntityLabelCount,
                        labelCount,
                        CacheWindowMs))
                    return _cachedGroundLabelEntityAddresses;


                _cachedGroundLabelEntityAddresses.Clear();
                _cachedGroundLabelEntityAddresses.EnsureCapacity(labelCount);

                for (int i = 0; i < labelCount; i++)
                {
                    long address = TryResolveEntityAddress(labels[i]);
                    if (address != 0)
                        _cachedGroundLabelEntityAddresses.Add(address);
                }

                _cachedGroundLabelEntityAddressesTimestampMs = now;
                _cachedGroundLabelEntityLabelCount = labelCount;
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