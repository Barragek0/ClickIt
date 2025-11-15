using System;

namespace ClickIt
{
    // Minimal ToggleNode replacement used only in tests to avoid pulling ExileCore types
    public class ToggleNode<T>
    {
        public T Value { get; set; }
        public ToggleNode(T value) { Value = value; }
    }

    // Minimal ClickItSettings stub used by LockManager in test builds
    public class ClickItSettings
    {
        public ToggleNode<bool> UseLocking { get; set; } = new ToggleNode<bool>(true);
        // Minimal ModTiers support used by WeightCalculator tests
        public System.Collections.Generic.Dictionary<string, int> ModTiers { get; set; } = new System.Collections.Generic.Dictionary<string, int>();

        public int GetModTier(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1;
            return ModTiers.TryGetValue(id, out var v) ? v : 1;
        }

        public int GetModTier(string id, string type)
        {
            if (string.IsNullOrEmpty(id)) return 1;
            string composite = $"{type}|{id}";
            return ModTiers.TryGetValue(composite, out var v) ? v : 1;
        }

        public void InitializeDefaultWeights()
        {
            // Populate composite keys from constants in tests
            foreach (var tuple in ClickIt.Constants.AltarModsConstants.UpsideMods)
            {
                var (Id, _, Type, DefaultValue) = tuple;
                string composite = $"{Type}|{Id}";
                if (!ModTiers.ContainsKey(composite)) ModTiers[composite] = DefaultValue;
            }
            foreach (var tuple in ClickIt.Constants.AltarModsConstants.DownsideMods)
            {
                var (Id, _, Type, DefaultValue) = tuple;
                string composite = $"{Type}|{Id}";
                if (!ModTiers.ContainsKey(composite)) ModTiers[composite] = DefaultValue;
            }
        }

        public void EnsureAllModsHaveWeights() => InitializeDefaultWeights();
    }
}
