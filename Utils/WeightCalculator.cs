using ClickIt.Components;
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
                topDownsideWeights[i] = CalculateDownsideWeight([GetModString(altar.TopMods, i, true)]);
                bottomDownsideWeights[i] = CalculateDownsideWeight([GetModString(altar.BottomMods, i, true)]);
                topUpsideWeights[i] = CalculateUpsideWeight([GetModString(altar.TopMods, i, false)]);
                bottomUpsideWeights[i] = CalculateUpsideWeight([GetModString(altar.BottomMods, i, false)]);
            }

            decimal topUpsideWeight = topUpsideWeights.Sum();
            decimal topDownsideWeight = topDownsideWeights.Sum();
            decimal bottomUpsideWeight = bottomUpsideWeights.Sum();
            decimal bottomDownsideWeight = bottomDownsideWeights.Sum();

            return new AltarWeights
            {
                TopUpsideWeight = topUpsideWeight,
                TopDownsideWeight = topDownsideWeight,
                BottomUpsideWeight = bottomUpsideWeight,
                BottomDownsideWeight = bottomDownsideWeight,
                // Support for 8 mods on top and bottom (downsides) - now much cleaner
                TopDownside1Weight = topDownsideWeights[0],
                TopDownside2Weight = topDownsideWeights[1],
                TopDownside3Weight = topDownsideWeights[2],
                TopDownside4Weight = topDownsideWeights[3],
                TopDownside5Weight = topDownsideWeights[4],
                TopDownside6Weight = topDownsideWeights[5],
                TopDownside7Weight = topDownsideWeights[6],
                TopDownside8Weight = topDownsideWeights[7],
                BottomDownside1Weight = bottomDownsideWeights[0],
                BottomDownside2Weight = bottomDownsideWeights[1],
                BottomDownside3Weight = bottomDownsideWeights[2],
                BottomDownside4Weight = bottomDownsideWeights[3],
                BottomDownside5Weight = bottomDownsideWeights[4],
                BottomDownside6Weight = bottomDownsideWeights[5],
                BottomDownside7Weight = bottomDownsideWeights[6],
                BottomDownside8Weight = bottomDownsideWeights[7],
                // Support for 8 mods on top and bottom (upsides) - now much cleaner
                TopUpside1Weight = topUpsideWeights[0],
                TopUpside2Weight = topUpsideWeights[1],
                TopUpside3Weight = topUpsideWeights[2],
                TopUpside4Weight = topUpsideWeights[3],
                TopUpside5Weight = topUpsideWeights[4],
                TopUpside6Weight = topUpsideWeights[5],
                TopUpside7Weight = topUpsideWeights[6],
                TopUpside8Weight = topUpsideWeights[7],
                BottomUpside1Weight = bottomUpsideWeights[0],
                BottomUpside2Weight = bottomUpsideWeights[1],
                BottomUpside3Weight = bottomUpsideWeights[2],
                BottomUpside4Weight = bottomUpsideWeights[3],
                BottomUpside5Weight = bottomUpsideWeights[4],
                BottomUpside6Weight = bottomUpsideWeights[5],
                BottomUpside7Weight = bottomUpsideWeights[6],
                BottomUpside8Weight = bottomUpsideWeights[7],
                TopWeight = Math.Round(topUpsideWeight / topDownsideWeight, 2),
                BottomWeight = Math.Round(bottomUpsideWeight / bottomDownsideWeight, 2)
            };
        }

        private string GetModString(SecondaryAltarComponent component, int index, bool isDownside)
        {
            if (component == null) return "";

            // For downsides, indices 0-3 map to component.GetDownsideByIndex(0-3)
            // For upsides, indices 0-3 map to component.GetUpsideByIndex(0-3)
            return isDownside ? component.GetDownsideByIndex(index) : component.GetUpsideByIndex(index);
        }
        public decimal CalculateUpsideWeight(List<string> upsides)
        {
            decimal totalWeight = 1;  // Start with 1 to avoid division by zero
            if (upsides == null) return 0;
            foreach (string upside in upsides)
            {
                if (string.IsNullOrEmpty(upside)) continue;
                // Upside may be stored as composite "Type|Id" (new format) or legacy id-only.
                if (upside.Contains('|'))
                {
                    var parts = upside.Split(new[] { '|' }, 2);
                    string type = parts[0];
                    string id = parts.Length > 1 ? parts[1] : parts[0];
                    int weight = _settings.GetModTier(id, type);
                    totalWeight += weight;
                }
                else
                {
                    int weight = _settings.GetModTier(upside);
                    totalWeight += weight;
                }
            }
            return totalWeight - 1;  // Subtract the initial 1 to maintain consistency
        }
        public decimal CalculateDownsideWeight(List<string> downsides)
        {
            decimal totalWeight = 1;  // Start with 1 to avoid division by zero
            if (downsides == null) return 0;
            foreach (string downside in downsides)
            {
                if (string.IsNullOrEmpty(downside)) continue;
                if (downside.Contains('|'))
                {
                    var parts = downside.Split(new[] { '|' }, 2);
                    string type = parts[0];
                    string id = parts.Length > 1 ? parts[1] : parts[0];
                    int weight = _settings.GetModTier(id, type);
                    totalWeight += weight;
                }
                else
                {
                    int weight = _settings.GetModTier(downside);
                    totalWeight += weight;
                }
            }
            return totalWeight - 1;  // Subtract the initial 1 to fix +8 issue
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
                var gm = global::ClickIt.Utils.LockManager.Instance;
                if (gm != null)
                {
                    using (gm.Acquire(_weightsLock))
                    {
                        return weightType.ToLower() switch
                        {
                            WeightTypeConstants.TopDownside => _topDownsideWeights?[index] ?? 0,
                            WeightTypeConstants.BottomDownside => _bottomDownsideWeights?[index] ?? 0,
                            WeightTypeConstants.TopUpside => _topUpsideWeights?[index] ?? 0,
                            WeightTypeConstants.BottomUpside => _bottomUpsideWeights?[index] ?? 0,
                            _ => 0
                        };
                    }
                }
                return weightType.ToLower() switch
                {
                    WeightTypeConstants.TopDownside => _topDownsideWeights?[index] ?? 0,
                    WeightTypeConstants.BottomDownside => _bottomDownsideWeights?[index] ?? 0,
                    WeightTypeConstants.TopUpside => _topUpsideWeights?[index] ?? 0,
                    WeightTypeConstants.BottomUpside => _bottomUpsideWeights?[index] ?? 0,
                    _ => 0
                };
            }
            set
            {
                var gm = global::ClickIt.Utils.LockManager.Instance;
                if (gm != null)
                {
                    using (gm.Acquire(_weightsLock))
                    {
                        switch (weightType.ToLower())
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
                    return;
                }

                switch (weightType.ToLower())
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

        // Backward compatibility properties - delegate to arrays using constants
        public decimal TopDownside1Weight { get => _topDownsideWeights?[0] ?? 0; set => SetWeight(WeightTypeConstants.TopDownside, 0, value); }
        public decimal TopDownside2Weight { get => _topDownsideWeights?[1] ?? 0; set => SetWeight(WeightTypeConstants.TopDownside, 1, value); }
        public decimal TopDownside3Weight { get => _topDownsideWeights?[2] ?? 0; set => SetWeight(WeightTypeConstants.TopDownside, 2, value); }
        public decimal TopDownside4Weight { get => _topDownsideWeights?[3] ?? 0; set => SetWeight(WeightTypeConstants.TopDownside, 3, value); }
        public decimal TopDownside5Weight { get => _topDownsideWeights?[4] ?? 0; set => SetWeight(WeightTypeConstants.TopDownside, 4, value); }
        public decimal TopDownside6Weight { get => _topDownsideWeights?[5] ?? 0; set => SetWeight(WeightTypeConstants.TopDownside, 5, value); }
        public decimal TopDownside7Weight { get => _topDownsideWeights?[6] ?? 0; set => SetWeight(WeightTypeConstants.TopDownside, 6, value); }
        public decimal TopDownside8Weight { get => _topDownsideWeights?[7] ?? 0; set => SetWeight(WeightTypeConstants.TopDownside, 7, value); }

        public decimal BottomDownside1Weight { get => _bottomDownsideWeights?[0] ?? 0; set => SetWeight(WeightTypeConstants.BottomDownside, 0, value); }
        public decimal BottomDownside2Weight { get => _bottomDownsideWeights?[1] ?? 0; set => SetWeight(WeightTypeConstants.BottomDownside, 1, value); }
        public decimal BottomDownside3Weight { get => _bottomDownsideWeights?[2] ?? 0; set => SetWeight(WeightTypeConstants.BottomDownside, 2, value); }
        public decimal BottomDownside4Weight { get => _bottomDownsideWeights?[3] ?? 0; set => SetWeight(WeightTypeConstants.BottomDownside, 3, value); }
        public decimal BottomDownside5Weight { get => _bottomDownsideWeights?[4] ?? 0; set => SetWeight(WeightTypeConstants.BottomDownside, 4, value); }
        public decimal BottomDownside6Weight { get => _bottomDownsideWeights?[5] ?? 0; set => SetWeight(WeightTypeConstants.BottomDownside, 5, value); }
        public decimal BottomDownside7Weight { get => _bottomDownsideWeights?[6] ?? 0; set => SetWeight(WeightTypeConstants.BottomDownside, 6, value); }
        public decimal BottomDownside8Weight { get => _bottomDownsideWeights?[7] ?? 0; set => SetWeight(WeightTypeConstants.BottomDownside, 7, value); }

        public decimal TopUpside1Weight { get => _topUpsideWeights?[0] ?? 0; set => SetWeight(WeightTypeConstants.TopUpside, 0, value); }
        public decimal TopUpside2Weight { get => _topUpsideWeights?[1] ?? 0; set => SetWeight(WeightTypeConstants.TopUpside, 1, value); }
        public decimal TopUpside3Weight { get => _topUpsideWeights?[2] ?? 0; set => SetWeight(WeightTypeConstants.TopUpside, 2, value); }
        public decimal TopUpside4Weight { get => _topUpsideWeights?[3] ?? 0; set => SetWeight(WeightTypeConstants.TopUpside, 3, value); }
        public decimal TopUpside5Weight { get => _topUpsideWeights?[4] ?? 0; set => SetWeight(WeightTypeConstants.TopUpside, 4, value); }
        public decimal TopUpside6Weight { get => _topUpsideWeights?[5] ?? 0; set => SetWeight(WeightTypeConstants.TopUpside, 5, value); }
        public decimal TopUpside7Weight { get => _topUpsideWeights?[6] ?? 0; set => SetWeight(WeightTypeConstants.TopUpside, 6, value); }
        public decimal TopUpside8Weight { get => _topUpsideWeights?[7] ?? 0; set => SetWeight(WeightTypeConstants.TopUpside, 7, value); }

        public decimal BottomUpside1Weight { get => _bottomUpsideWeights?[0] ?? 0; set => SetWeight(WeightTypeConstants.BottomUpside, 0, value); }
        public decimal BottomUpside2Weight { get => _bottomUpsideWeights?[1] ?? 0; set => SetWeight(WeightTypeConstants.BottomUpside, 1, value); }
        public decimal BottomUpside3Weight { get => _bottomUpsideWeights?[2] ?? 0; set => SetWeight(WeightTypeConstants.BottomUpside, 2, value); }
        public decimal BottomUpside4Weight { get => _bottomUpsideWeights?[3] ?? 0; set => SetWeight(WeightTypeConstants.BottomUpside, 3, value); }
        public decimal BottomUpside5Weight { get => _bottomUpsideWeights?[4] ?? 0; set => SetWeight(WeightTypeConstants.BottomUpside, 4, value); }
        public decimal BottomUpside6Weight { get => _bottomUpsideWeights?[5] ?? 0; set => SetWeight(WeightTypeConstants.BottomUpside, 5, value); }
        public decimal BottomUpside7Weight { get => _bottomUpsideWeights?[6] ?? 0; set => SetWeight(WeightTypeConstants.BottomUpside, 6, value); }
        public decimal BottomUpside8Weight { get => _bottomUpsideWeights?[7] ?? 0; set => SetWeight(WeightTypeConstants.BottomUpside, 7, value); }

        // Helper methods for cleaner access
        public decimal[] GetTopDownsideWeights()
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return _topDownsideWeights ?? new decimal[8];
                }
            }
            return _topDownsideWeights ?? new decimal[8];
        }
        public decimal[] GetBottomDownsideWeights()
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return _bottomDownsideWeights ?? new decimal[8];
                }
            }
            return _bottomDownsideWeights ?? new decimal[8];
        }
        public decimal[] GetTopUpsideWeights()
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return _topUpsideWeights ?? new decimal[8];
                }
            }
            return _topUpsideWeights ?? new decimal[8];
        }
        public decimal[] GetBottomUpsideWeights()
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_weightsLock))
                {
                    return _bottomUpsideWeights ?? new decimal[8];
                }
            }
            return _bottomUpsideWeights ?? new decimal[8];
        }

        // Set weight helper method
        private void SetWeight(string type, int index, decimal value)
        {
            this[type, index] = value;
        }

        // Method to initialize all weights from arrays (for cleaner construction)
        public void InitializeFromArrays(decimal[] topDownside, decimal[] bottomDownside, decimal[] topUpside, decimal[] bottomUpside)
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
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
    }
}
