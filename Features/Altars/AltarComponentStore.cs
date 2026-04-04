namespace ClickIt.Features.Altars
{
    internal sealed class AltarComponentStore
    {
        private readonly List<PrimaryAltarComponent> _altarComponents = [];
        private readonly HashSet<string> _altarKeys = new(StringComparer.Ordinal);
        private readonly object _altarComponentsLock = new();
        private volatile PrimaryAltarComponent[] _altarSnapshot = [];
        private volatile ReadOnlyCollection<PrimaryAltarComponent> _altarReadOnlySnapshot = Array.AsReadOnly(Array.Empty<PrimaryAltarComponent>());

        internal List<PrimaryAltarComponent> GetComponents() => [.. _altarSnapshot];

        internal IReadOnlyList<PrimaryAltarComponent> GetComponentsReadOnly() => _altarReadOnlySnapshot;

        internal int GetComponentCount() => _altarSnapshot.Length;

        internal void Clear()
        {
            lock (_altarComponentsLock)
            {
                foreach (var component in _altarComponents)
                    component.InvalidateCache();

                _altarComponents.Clear();
                _altarKeys.Clear();
                RefreshSnapshotUnderLock();
            }
        }

        internal bool Add(PrimaryAltarComponent component)
        {
            if (component == null)
                return false;

            lock (_altarComponentsLock)
            {
                string newKey = BuildAltarKey(component);
                if (!_altarKeys.Add(newKey))
                    return false;

                _altarComponents.Add(component);
                RefreshSnapshotUnderLock();
                return true;
            }
        }

        internal void RemoveByElement(Element element)
        {
            if (element == null)
                return;

            RemoveWhere(altar => MatchesElement(altar, element));
        }

        internal void RemoveWhere(Func<PrimaryAltarComponent, bool> predicate)
        {
            if (predicate == null)
                return;

            lock (_altarComponentsLock)
            {
                int removed = _altarComponents.RemoveAll(component =>
                {
                    bool remove = predicate(component);
                    if (remove)
                        component.InvalidateCache();
                    return remove;
                });

                if (removed <= 0)
                    return;

                RebuildKeySnapshotUnderLock();
                RefreshSnapshotUnderLock();
            }
        }

        private static bool MatchesElement(PrimaryAltarComponent altar, Element element)
        {
            try
            {
                return ReferenceEquals(altar.TopButton?.Element, element)
                    || ReferenceEquals(altar.BottomButton?.Element, element)
                    || ReferenceEquals(altar.TopMods?.Element, element)
                    || ReferenceEquals(altar.BottomMods?.Element, element)
                    || ReferenceEquals(altar.TopButton?.Element?.Parent, element)
                    || ReferenceEquals(altar.BottomButton?.Element?.Parent, element);
            }
            catch
            {
                return false;
            }
        }

        private void RefreshSnapshotUnderLock()
        {
            PrimaryAltarComponent[] snapshot = [.. _altarComponents];
            _altarSnapshot = snapshot;
            _altarReadOnlySnapshot = Array.AsReadOnly(snapshot);
        }

        private void RebuildKeySnapshotUnderLock()
        {
            _altarKeys.Clear();
            for (int i = 0; i < _altarComponents.Count; i++)
                _altarKeys.Add(BuildAltarKey(_altarComponents[i]));
        }

        private static string BuildAltarKey(PrimaryAltarComponent component)
        {
            var topUpside = GetAltarModStrings(component.TopMods, false);
            var topDownside = GetAltarModStrings(component.TopMods, true);
            var bottomUpside = GetAltarModStrings(component.BottomMods, false);
            var bottomDownside = GetAltarModStrings(component.BottomMods, true);

            var allMods = topUpside.Concat(topDownside)
                .Concat(bottomUpside)
                .Concat(bottomDownside);

            return string.Join("|", allMods);
        }

        private static string[] GetAltarModStrings(SecondaryAltarComponent? component, bool isDownside)
        {
            if (component == null)
                return new string[8];

            string[] values = new string[8];
            for (int i = 0; i < values.Length; i++)
                values[i] = isDownside ? component.GetDownsideByIndex(i) : component.GetUpsideByIndex(i);

            return values;
        }
    }
}