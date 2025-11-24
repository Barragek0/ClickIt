namespace ClickIt
{
    // Minimal test shim for ClickItSettings used by test builds when Core types are unavailable.
    public class ToggleNode<T>
    {
        public T Value { get; set; }
        public ToggleNode(T value) { Value = value; }
    }

    public class ClickItSettings
    {

        public Dictionary<string, int> ModTiers { get; set; } = new Dictionary<string, int>();
        public int GetModTier(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1;
            return ModTiers.TryGetValue(id, out var v) ? v : 1;
        }

        public int GetModTier(string id, string type)
        {
            if (string.IsNullOrEmpty(id)) return 1;
            var composite = $"{type}|{id}";
            return ModTiers.TryGetValue(composite, out var v) ? v : 1;
        }

        // Populate ModTiers from AltarModsConstants so tests that expect defaults to be present succeed
        public void InitializeDefaultWeights()
        {
            // Use the constants compiled into the test project
            try
            {
                foreach (var t in ClickIt.Constants.AltarModsConstants.DownsideMods)
                {
                    if (!ModTiers.ContainsKey(t.Id)) ModTiers[t.Id] = t.DefaultValue;
                    var composite = $"{t.Type}|{t.Id}";
                    if (!ModTiers.ContainsKey(composite)) ModTiers[composite] = t.DefaultValue;
                }
                foreach (var t in ClickIt.Constants.AltarModsConstants.UpsideMods)
                {
                    if (!ModTiers.ContainsKey(t.Id)) ModTiers[t.Id] = t.DefaultValue;
                    var composite = $"{t.Type}|{t.Id}";
                    if (!ModTiers.ContainsKey(composite)) ModTiers[composite] = t.DefaultValue;
                }
            }
            catch
            {
                // If constants are not available for some reason in the test environment, leave ModTiers as-is.
            }
        }

        public void EnsureAllModsHaveWeights() => InitializeDefaultWeights();
    }
}
