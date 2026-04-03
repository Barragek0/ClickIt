namespace ClickIt
{
    public partial class ClickItSettings
    {
        internal void InitializeDefaultWeights()
            => AltarWeightSettingsService.InitializeDefaultWeights(this);

        internal static string BuildCompositeKey(string type, string id)
            => AltarWeightSettingsService.BuildCompositeKey(type, id);

        internal void EnsureAllModsHaveWeights()
            => AltarWeightSettingsService.EnsureAllModsHaveWeights(this);

        internal int GetModTier(string modId)
            => AltarWeightSettingsService.GetModTier(this, modId);

        internal int GetModTier(string modId, string type)
            => AltarWeightSettingsService.GetModTier(this, modId, type);

        internal bool GetModAlert(string modId, string type)
            => AltarWeightSettingsService.GetModAlert(this, modId, type);

        public Dictionary<string, int> ModTiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> ModAlerts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}