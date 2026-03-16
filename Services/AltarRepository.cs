using ClickIt.Components;
using System.Collections.ObjectModel;

namespace ClickIt.Services
{
    public class AltarRepository
    {
        private readonly List<PrimaryAltarComponent> _altarComponents = [];
        private readonly HashSet<string> _altarKeys = new(StringComparer.Ordinal);
        private readonly object _altarComponentsLock = new();
        private volatile PrimaryAltarComponent[] _altarSnapshot = [];
        private volatile ReadOnlyCollection<PrimaryAltarComponent> _altarReadOnlySnapshot = Array.AsReadOnly(Array.Empty<PrimaryAltarComponent>());

        public List<PrimaryAltarComponent> GetAltarComponents() => [.. _altarSnapshot];
        public IReadOnlyList<PrimaryAltarComponent> GetAltarComponentsReadOnly() => _altarReadOnlySnapshot;
        public int GetAltarComponentCount() => _altarSnapshot.Length;

        private void RefreshSnapshotUnderLock()
        {
            PrimaryAltarComponent[] snapshot = [.. _altarComponents];
            _altarSnapshot = snapshot;
            _altarReadOnlySnapshot = Array.AsReadOnly(snapshot);
        }

        public void ClearAltarComponents()
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

        public void RemoveAltarComponentsByElement(Func<PrimaryAltarComponent, bool> predicate)
        {
            if (predicate == null) return;
            lock (_altarComponentsLock)
            {
                int removed = _altarComponents.RemoveAll(c =>
                {
                    bool remove = predicate(c);
                    if (remove)
                        c.InvalidateCache();
                    return remove;
                });

                if (removed > 0)
                {
                    RebuildKeySnapshotUnderLock();
                }

                RefreshSnapshotUnderLock();
            }
        }

        public bool AddAltarComponent(PrimaryAltarComponent component)
        {
            if (component == null) return false;
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

        private void RebuildKeySnapshotUnderLock()
        {
            _altarKeys.Clear();
            for (int i = 0; i < _altarComponents.Count; i++)
            {
                _altarKeys.Add(BuildAltarKey(_altarComponents[i]));
            }
        }

        private static string BuildAltarKey(PrimaryAltarComponent comp)
        {
            var topUpside = GetModStrings(comp.TopMods, false);
            var topDownside = GetModStrings(comp.TopMods, true);
            var bottomUpside = GetModStrings(comp.BottomMods, false);
            var bottomDownside = GetModStrings(comp.BottomMods, true);

            var allMods = topUpside.Concat(topDownside)
                                  .Concat(bottomUpside)
                                  .Concat(bottomDownside);

            return string.Join("|", allMods);
        }

        private static string[] GetModStrings(SecondaryAltarComponent? component, bool isDownside)
        {
            if (component == null)
                return new string[8];

            string[] arr = new string[8];
            for (int i = 0; i < 8; i++)
            {
                arr[i] = isDownside ? component.GetDownsideByIndex(i) : component.GetUpsideByIndex(i);
            }
            return arr;
        }
    }
}

