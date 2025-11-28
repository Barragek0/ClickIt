using ClickIt.Components;
using ClickIt;
using System;
using System.Collections.Generic;
using System.Linq;
namespace ClickIt.Utils
{
    internal static class WeightTypeConstants
    {
        public const string TopDownside = "topdownside";
        public const string BottomDownside = "bottomdownside";
        public const string TopUpside = "topupside";
        public const string BottomUpside = "bottomupside";
    }

    public class WeightCalculator(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public AltarWeights CalculateAltarWeights(PrimaryAltarComponent altar)
        {
            if (altar?.TopMods == null || altar.BottomMods == null)
            {
                throw new ArgumentException("Cannot calculate weights - altar components are null");
            }

            if (altar.TopMods.Element == null || altar.BottomMods.Element == null)
            {
                throw new ArgumentException("Cannot calculate weights - altar elements are null");
            }

            var topUpsideWeights = new decimal[8];
            var topDownsideWeights = new decimal[8];
            var bottomUpsideWeights = new decimal[8];
            var bottomDownsideWeights = new decimal[8];

            for (int i = 0; i < 8; i++)
            {
                string topDownMod = GetModString(altar.TopMods, i, true);
                string bottomDownMod = GetModString(altar.BottomMods, i, true);
                string topUpMod = GetModString(altar.TopMods, i, false);
                string bottomUpMod = GetModString(altar.BottomMods, i, false);

                topDownsideWeights[i] = GetModWeightFromString(topDownMod);
                bottomDownsideWeights[i] = GetModWeightFromString(bottomDownMod);
                topUpsideWeights[i] = GetModWeightFromString(topUpMod);
                bottomUpsideWeights[i] = GetModWeightFromString(bottomUpMod);
            }

            decimal topUpsideWeight = topUpsideWeights.Sum();
            decimal topDownsideWeight = topDownsideWeights.Sum();
            decimal bottomUpsideWeight = bottomUpsideWeights.Sum();
            decimal bottomDownsideWeight = bottomDownsideWeights.Sum();

            var result = new AltarWeights();
            result.InitializeFromArrays(topDownsideWeights, bottomDownsideWeights, topUpsideWeights, bottomUpsideWeights);
            result.TopUpsideWeight = topUpsideWeight;
            result.TopDownsideWeight = topDownsideWeight;
            result.BottomUpsideWeight = bottomUpsideWeight;
            result.BottomDownsideWeight = bottomDownsideWeight;

            // Safe division: avoid divide-by-zero
            result.TopWeight = Math.Round(topDownsideWeight == 0 ? 0 : topUpsideWeight / topDownsideWeight, 2);
            result.BottomWeight = Math.Round(bottomDownsideWeight == 0 ? 0 : bottomUpsideWeight / bottomDownsideWeight, 2);
            return result;
        }

        private static string GetModString(SecondaryAltarComponent component, int index, bool isDownside)
        {
            if (component == null)
                return "";

            return isDownside ? component.GetDownsideByIndex(index) : component.GetUpsideByIndex(index);
        }
        public decimal CalculateUpsideWeight(List<string> upsides)
        {
            return CalculateWeightFromList(upsides, false);
        }

        public decimal CalculateDownsideWeight(List<string> downsides)
        {
            return CalculateWeightFromList(downsides, true);
        }

        private decimal CalculateWeightFromList(List<string> mods, bool isDownside)
        {
            if (mods == null || mods.Count == 0) return isDownside ? 1m : 0m;
            decimal total = 0m;
            foreach (var mod in mods)
            {
                if (string.IsNullOrWhiteSpace(mod)) continue;
                total += GetModWeightFromString(mod);
            }
            return isDownside ? 1 + total : total;
        }

        private decimal GetModWeightFromString(string mod)
        {
            if (string.IsNullOrEmpty(mod)) return 0m;
            if (mod.Contains('|'))
            {
                var parts = mod.Split(['|'], 2);
                string type = parts[0];
                string id = parts.Length > 1 ? parts[1] : parts[0];
                return _settings.GetModTier(id, type);
            }
            return _settings.GetModTier(mod);
        }
    }
    public struct AltarWeights
    {
        // Core totals
        public decimal TopUpsideWeight { get; set; }
        public decimal TopDownsideWeight { get; set; }
        public decimal BottomUpsideWeight { get; set; }
        public decimal BottomDownsideWeight { get; set; }

        // Individual mod weights stored in arrays for cleaner management
        private decimal[] _topDownsideWeights;
        private decimal[] _bottomDownsideWeights;
        private decimal[] _topUpsideWeights;
        private decimal[] _bottomUpsideWeights;

        // Thread safety lock for weight array operations
        private readonly object _weightsLock = new();

        public AltarWeights()
        {
            _topDownsideWeights = new decimal[8];
            _bottomDownsideWeights = new decimal[8];
            _topUpsideWeights = new decimal[8];
            _bottomUpsideWeights = new decimal[8];
        }

        public decimal TopWeight { get; set; }
        public decimal BottomWeight { get; set; }

        // Indexers for convenient access - much cleaner than individual properties
        public decimal this[string weightType, int index]
        {
            get
            {
                var self = this;
                return WithWeightsLock(() => self.GetArrayElement(weightType, index));
            }
            set
            {
                var self = this;
                WithWeightsLock(() => self.SetArrayElement(weightType, index, value));
            }
        }

        // Helper to centralize LockManager usage for weight array operations
        private readonly T WithWeightsLock<T>(Func<T> func)
        {
            using (LockManager.AcquireStatic(_weightsLock))
            {
                return func();
            }
        }

        private readonly void WithWeightsLock(Action action)
        {
            using (LockManager.AcquireStatic(_weightsLock))
            {
                action();
            }
        }

        // Helper methods for cleaner access
        public readonly decimal[] GetTopDownsideWeights()
        {
            using (LockManager.AcquireStatic(_weightsLock))
            {
                return _topDownsideWeights ?? new decimal[8];
            }
        }
        public readonly decimal[] GetBottomDownsideWeights()
        {
            using (LockManager.AcquireStatic(_weightsLock))
            {
                return _bottomDownsideWeights ?? new decimal[8];
            }
        }
        public readonly decimal[] GetTopUpsideWeights()
        {
            using (LockManager.AcquireStatic(_weightsLock))
            {
                return _topUpsideWeights ?? new decimal[8];
            }
        }
        public readonly decimal[] GetBottomUpsideWeights()
        {
            using (LockManager.AcquireStatic(_weightsLock))
            {
                return _bottomUpsideWeights ?? new decimal[8];
            }
        }

        // Method to initialize all weights from arrays (for cleaner construction)
        public void InitializeFromArrays(decimal[] topDownside, decimal[] bottomDownside, decimal[] topUpside, decimal[] bottomUpside)
        {
            using (LockManager.AcquireStatic(_weightsLock))
            {
                _topDownsideWeights = topDownside ?? new decimal[8];
                _bottomDownsideWeights = bottomDownside ?? new decimal[8];
                _topUpsideWeights = topUpside ?? new decimal[8];
                _bottomUpsideWeights = bottomUpside ?? new decimal[8];
            }
        }

        private readonly decimal GetArrayElement(string weightType, int index)
        {
            string key = weightType ?? string.Empty;
            if (string.Equals(key, WeightTypeConstants.TopDownside, StringComparison.OrdinalIgnoreCase))
                return _topDownsideWeights?[index] ?? 0;
            if (string.Equals(key, WeightTypeConstants.BottomDownside, StringComparison.OrdinalIgnoreCase))
                return _bottomDownsideWeights?[index] ?? 0;
            if (string.Equals(key, WeightTypeConstants.TopUpside, StringComparison.OrdinalIgnoreCase))
                return _topUpsideWeights?[index] ?? 0;
            if (string.Equals(key, WeightTypeConstants.BottomUpside, StringComparison.OrdinalIgnoreCase))
                return _bottomUpsideWeights?[index] ?? 0;
            return 0;
        }

        private void SetArrayElement(string weightType, int index, decimal value)
        {
            string key = weightType ?? string.Empty;
            if (string.Equals(key, WeightTypeConstants.TopDownside, StringComparison.OrdinalIgnoreCase))
            {
                if (_topDownsideWeights == null) _topDownsideWeights = new decimal[8];
                _topDownsideWeights[index] = value;
            }
            else if (string.Equals(key, WeightTypeConstants.BottomDownside, StringComparison.OrdinalIgnoreCase))
            {
                if (_bottomDownsideWeights == null) _bottomDownsideWeights = new decimal[8];
                _bottomDownsideWeights[index] = value;
            }
            else if (string.Equals(key, WeightTypeConstants.TopUpside, StringComparison.OrdinalIgnoreCase))
            {
                if (_topUpsideWeights == null) _topUpsideWeights = new decimal[8];
                _topUpsideWeights[index] = value;
            }
            else if (string.Equals(key, WeightTypeConstants.BottomUpside, StringComparison.OrdinalIgnoreCase))
            {
                if (_bottomUpsideWeights == null) _bottomUpsideWeights = new decimal[8];
                _bottomUpsideWeights[index] = value;
            }
        }
    }
}
