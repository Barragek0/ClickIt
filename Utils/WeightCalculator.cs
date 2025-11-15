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

    public class WeightCalculator
    {
        private readonly ClickItSettings _settings;
        public WeightCalculator(ClickItSettings settings)
        {
            _settings = settings;
        }
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

        private string GetModString(SecondaryAltarComponent component, int index, bool isDownside)
        {
            if (component == null)
                return "";

            return isDownside ? component.GetDownsideByIndex(index) : component.GetUpsideByIndex(index);
        }
        public decimal CalculateUpsideWeight(List<string> upsides)
        {
            return CalculateWeightFromList(upsides);
        }

        public decimal CalculateDownsideWeight(List<string> downsides)
        {
            return CalculateWeightFromList(downsides);
        }

        private decimal CalculateWeightFromList(List<string> mods)
        {
            if (mods == null) return 0m;
            decimal total = 0m;
            foreach (var mod in mods)
            {
                if (string.IsNullOrEmpty(mod)) continue;
                total += GetModWeightFromString(mod);
            }
            return total;
        }

        private decimal GetModWeightFromString(string mod)
        {
            if (string.IsNullOrEmpty(mod)) return 0m;
            if (mod.Contains('|'))
            {
                var parts = mod.Split(new[] { '|' }, 2);
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
        private readonly object _weightsLock = new object();

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
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return func();
                }
            }
            return func();
        }

        private readonly void WithWeightsLock(Action action)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    action();
                }
                return;
            }
            action();
        }

        // Helper methods for cleaner access
        public readonly decimal[] GetTopDownsideWeights()
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return _topDownsideWeights ?? new decimal[8];
                }
            }
            return _topDownsideWeights ?? new decimal[8];
        }
        public readonly decimal[] GetBottomDownsideWeights()
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return _bottomDownsideWeights ?? new decimal[8];
                }
            }
            return _bottomDownsideWeights ?? new decimal[8];
        }
        public readonly decimal[] GetTopUpsideWeights()
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return _topUpsideWeights ?? new decimal[8];
                }
            }
            return _topUpsideWeights ?? new decimal[8];
        }
        public readonly decimal[] GetBottomUpsideWeights()
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return _bottomUpsideWeights ?? new decimal[8];
                }
            }
            return _bottomUpsideWeights ?? new decimal[8];
        }

        // Method to initialize all weights from arrays (for cleaner construction)
        public void InitializeFromArrays(decimal[] topDownside, decimal[] bottomDownside, decimal[] topUpside, decimal[] bottomUpside)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    _topDownsideWeights = topDownside ?? new decimal[8];
                    _bottomDownsideWeights = bottomDownside ?? new decimal[8];
                    _topUpsideWeights = topUpside ?? new decimal[8];
                    _bottomUpsideWeights = bottomUpside ?? new decimal[8];
                }
            }
            else
            {
                _topDownsideWeights = topDownside ?? new decimal[8];
                _bottomDownsideWeights = bottomDownside ?? new decimal[8];
                _topUpsideWeights = topUpside ?? new decimal[8];
                _bottomUpsideWeights = bottomUpside ?? new decimal[8];
            }
        }

        private readonly decimal GetArrayElement(string weightType, int index)
        {
            string key = (weightType ?? string.Empty).ToLower();
            return key switch
            {
                WeightTypeConstants.TopDownside => _topDownsideWeights?[index] ?? 0,
                WeightTypeConstants.BottomDownside => _bottomDownsideWeights?[index] ?? 0,
                WeightTypeConstants.TopUpside => _topUpsideWeights?[index] ?? 0,
                WeightTypeConstants.BottomUpside => _bottomUpsideWeights?[index] ?? 0,
                _ => 0
            };
        }

        private void SetArrayElement(string weightType, int index, decimal value)
        {
            string key = (weightType ?? string.Empty).ToLower();
            switch (key)
            {
                case WeightTypeConstants.TopDownside:
                    if (_topDownsideWeights == null) _topDownsideWeights = new decimal[8];
                    _topDownsideWeights[index] = value;
                    break;
                case WeightTypeConstants.BottomDownside:
                    if (_bottomDownsideWeights == null) _bottomDownsideWeights = new decimal[8];
                    _bottomDownsideWeights[index] = value;
                    break;
                case WeightTypeConstants.TopUpside:
                    if (_topUpsideWeights == null) _topUpsideWeights = new decimal[8];
                    _topUpsideWeights[index] = value;
                    break;
                case WeightTypeConstants.BottomUpside:
                    if (_bottomUpsideWeights == null) _bottomUpsideWeights = new decimal[8];
                    _bottomUpsideWeights[index] = value;
                    break;
            }
        }
    }
}
