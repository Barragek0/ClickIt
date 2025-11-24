
using ClickIt.Components;

namespace ClickIt.Services
{
    public class AltarRepository
    {
        private readonly List<PrimaryAltarComponent> _altarComponents = new();
        private readonly object _altarComponentsLock = new();

        public List<PrimaryAltarComponent> GetAltarComponents() => _altarComponents.ToList();
        public IReadOnlyList<PrimaryAltarComponent> GetAltarComponentsReadOnly() => _altarComponents.AsReadOnly();

        public void ClearAltarComponents()
        {
            lock (_altarComponentsLock)
            {
                foreach (var component in _altarComponents)
                    component.InvalidateCache();
                _altarComponents.Clear();
            }
        }

        public void RemoveAltarComponentsByElement(Func<PrimaryAltarComponent, bool> predicate)
        {
            if (predicate == null) return;
            lock (_altarComponentsLock)
            {
                _altarComponents.RemoveAll(c =>
                {
                    bool remove = predicate(c);
                    if (remove)
                        c.InvalidateCache();
                    return remove;
                });
            }
        }

        public bool AddAltarComponent(PrimaryAltarComponent component)
        {
            if (component == null) return false;
            lock (_altarComponentsLock)
            {
                string newKey = BuildAltarKey(component);
                bool exists = _altarComponents.Any(existingComp => BuildAltarKey(existingComp) == newKey);
                if (!exists)
                {
                    _altarComponents.Add(component);
                    return true;
                }
                return false;
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

            var arr = new string[8];
            for (int i = 0; i < 8; i++)
            {
                arr[i] = isDownside ? component.GetDownsideByIndex(i) : component.GetUpsideByIndex(i);
            }
            return arr;
        }
    }
}
