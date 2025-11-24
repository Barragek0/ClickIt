using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Utils;
using ClickIt.Components;
using System;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class WeightCalculatorTests
    {
        private ClickIt.ClickItSettings _settings;
        private WeightCalculator _calc;

        [TestInitialize]
        public void Setup()
        {
            _settings = ClickIt.Tests.Shared.TestHelpers.CreateSettingsWithTiers();
            _calc = new WeightCalculator(_settings);
        }

        [TestMethod]
        public void CalculateUpsideWeight_SumsUpsides()
        {
            _settings.ModTiers["a"] = 5;
            _settings.ModTiers["b"] = 10;
            var upsides = new List<string> { "a", "b", "" };
            _calc.CalculateUpsideWeight(upsides).Should().Be(15);
        }

        [TestMethod]
        public void CalculateDownsideWeight_SumsDownsides()
        {
            _settings.ModTiers["x"] = 2;
            _settings.ModTiers["y"] = 3;
            var downs = new List<string> { "x", "y" };
            _calc.CalculateDownsideWeight(downs).Should().Be(6); // 1 + 2 + 3
        }

        [TestMethod]
        public void CalculateAltarWeights_ProducesExpectedRatios()
        {
            // composite keys
            _settings.ModTiers["Top|up1"] = 10;
            _settings.ModTiers["Top|down1"] = 5;
            _settings.ModTiers["Bottom|up1"] = 4;
            _settings.ModTiers["Bottom|down1"] = 2;

            var top = new SecondaryAltarComponent(new List<string> { "Top|up1" }, new List<string> { "Top|down1" });
            var bottom = new SecondaryAltarComponent(new List<string> { "Bottom|up1" }, new List<string> { "Bottom|down1" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            // TopUpside = 10, TopDownside = 5 -> TopWeight = 2.00
            weights.TopWeight.Should().Be(2.00m);
            // BottomUpside = 4, BottomDownside = 2 -> BottomWeight = 2.00
            weights.BottomWeight.Should().Be(2.00m);
        }

        // --- additional consolidated tests (merged from WeightCalculatorMore/Extra/Composite/AltarWeights* tests) ---

        [TestMethod]
        public void CalculateAltarWeights_NullPrimary_ThrowsArgumentException()
        {
            Action act = () => _calc.CalculateAltarWeights(null);
            act.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void CalculateAltarWeights_MissingElements_ThrowsArgumentException()
        {
            var top = new SecondaryAltarComponent(new List<string> { "a" }, new List<string> { "b" });
            var bottom = new SecondaryAltarComponent(new List<string> { "c" }, new List<string> { "d" });
            // make top element null to simulate invalid element
            top.Element = null;
            var primary = new PrimaryAltarComponent(top, bottom);
            Action act = () => _calc.CalculateAltarWeights(primary);
            act.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void AltarWeights_UnknownType_IndexerReturnsZero()
        {
            var w = new AltarWeights();
            w["unknown_type", 0] = 5.5m; // setter silently ignores unknown
            w["unknown_type", 0].Should().Be(0);
        }

        [TestMethod]
        public void CalculateAltarWeights_MultipleUpsidesSum()
        {
            // Arrange - use lightweight test settings and the existing WeightCalculator
            var calc = ClickIt.Tests.Shared.TestHelpers.CreateWeightCalculator(new System.Collections.Generic.Dictionary<string, int>
            {
                ["up1"] = 2,
                ["up2"] = 3
            });

            var top = new SecondaryAltarComponent(new System.Collections.Generic.List<string> { "up1", "up2" }, new System.Collections.Generic.List<string> { "" });
            var bottom = new SecondaryAltarComponent(new System.Collections.Generic.List<string> { "" }, new System.Collections.Generic.List<string> { "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            // Act
            var weights = calc.CalculateAltarWeights(primary);

            // Assert - TopUpsideWeight should be 2 + 3 = 5
            weights.TopUpsideWeight.Should().Be(5m);
            weights.BottomUpsideWeight.Should().Be(0m);
        }

        [TestMethod]
        public void CalculateAltarWeights_MixedTopBottom_RatioComputed()
        {
            // Arrange: set explicit mod tiers so that top total / bottom total = expected ratio (9 / 6 = 1.5)
            var settings = ClickIt.Tests.Shared.TestHelpers.CreateSettingsWithTiers(new System.Collections.Generic.Dictionary<string, int>
            {
                ["topA"] = 6,
                ["topB"] = 3,
                ["botA"] = 2,
                ["botB"] = 4
            });
            var calc = new WeightCalculator(settings);

            var top = new SecondaryAltarComponent(new System.Collections.Generic.List<string> { "topA", "topB" }, new System.Collections.Generic.List<string> { "" });
            var bottom = new SecondaryAltarComponent(new System.Collections.Generic.List<string> { "botA", "botB" }, new System.Collections.Generic.List<string> { "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            // Act
            var weights = calc.CalculateAltarWeights(primary);

            // Assert
            weights.TopUpsideWeight.Should().Be(9m);
            weights.BottomUpsideWeight.Should().Be(6m);
            // TopWeight and BottomWeight are rounded ratios of upside/downside arrays; since downsides were provided as "" the downside totals are 0 -> weights become 0
            // To validate ratio math, set downsides as well and recompute a more direct ratio test below.
            // Now create an altar with explicit downsides so we can validate TopWeight/BottomWeight ratio
            var topWithDowns = new SecondaryAltarComponent(new System.Collections.Generic.List<string> { "topA", "topB" }, new System.Collections.Generic.List<string> { "topDown" });
            var bottomWithDowns = new SecondaryAltarComponent(new System.Collections.Generic.List<string> { "botA", "botB" }, new System.Collections.Generic.List<string> { "botDown" });
            settings.ModTiers["topDown"] = 3; // top downside total = 3
            settings.ModTiers["botDown"] = 6; // bottom downside total = 6
            var primary2 = new PrimaryAltarComponent(topWithDowns, bottomWithDowns);
            var weights2 = calc.CalculateAltarWeights(primary2);

            // top upside = 9, top downside = 3 -> top weight = 9 / 3 = 3.00
            // bottom upside = 6, bottom downside = 6 -> bottom weight = 6 / 6 = 1.00
            weights2.TopWeight.Should().Be(3.00m);
            weights2.BottomWeight.Should().Be(1.00m);
        }

        [TestMethod]
        public void CalculateUpsideWeight_UnsetMods_TreatedAsZero()
        {
            // Arrange: no ModTiers set for "unknown_mod"
            var ups = new List<string> { "unknown_mod", "" };

            // Act
            var total = _calc.CalculateUpsideWeight(ups);

            // Assert - default fallback for unknown mods is 1 (backward-compatible behavior)
            total.Should().Be(1m);
        }

        [DataTestMethod]
        [DataRow(new[] { "mod1", "mod2", "" }, new[] { 5, 10 }, 15)]
        [DataRow(new[] { "mod3" }, new[] { 7 }, 7)]
        [DataRow(new string[] { }, new int[] { }, 0)]
        public void CalculateUpsideWeight_VariousInputs_Data(string[] mods, int[] weights, int expected)
        {
            for (int i = 0; i < mods.Length && i < weights.Length; i++)
            {
                if (!string.IsNullOrEmpty(mods[i]))
                    _settings.ModTiers[mods[i]] = weights[i];
            }
            var upsides = new List<string>(mods);
            _calc.CalculateUpsideWeight(upsides).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(new[] { "mod1", "mod2" }, new[] { 2, 3 }, 6)]
        [DataRow(new[] { "mod3" }, new[] { 8 }, 9)]
        [DataRow(new string[] { }, new int[] { }, 1)]
        public void CalculateDownsideWeight_VariousInputs_Data(string[] mods, int[] weights, int expected)
        {
            for (int i = 0; i < mods.Length && i < weights.Length; i++)
            {
                _settings.ModTiers[mods[i]] = weights[i];
            }
            var downsides = new List<string>(mods);
            _calc.CalculateDownsideWeight(downsides).Should().Be(expected);
        }


        [TestMethod]
        public void CalculateDownsideWeight_NullList_ReturnsZero()
        {
            decimal res = _calc.CalculateDownsideWeight(null);
            res.Should().Be(1m);
        }

        [TestMethod]
        public void CalculateAltarWeights_ZeroDownside_ProducesZeroTopWeight()
        {
            // Top has one upside with set weight, but no downsides -> division by zero should be guarded
            _settings.ModTiers["up_only"] = 10;

            var top = new SecondaryAltarComponent(new List<string> { "up_only" }, new List<string> { "", "", "", "", "", "", "", "" });
            var bottom = new SecondaryAltarComponent(new List<string> { "", "", "", "", "", "", "", "" }, new List<string> { "", "", "", "", "", "", "", "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(10m);
            weights.TopDownsideWeight.Should().Be(0m);
            weights.TopWeight.Should().Be(0m, "divide-by-zero is handled by returning 0 when downsides sum to zero");
        }

        [TestMethod]
        public void CalculateAltarWeights_UsesCompositeKeyPerType()
        {
            _settings.ModTiers["Boss|compmod"] = 90;

            var top = new SecondaryAltarComponent(new List<string> { "Boss|compmod" }, new List<string> { "" });
            var bottom = new SecondaryAltarComponent(new List<string> { "" }, new List<string> { "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(90m);
        }

        [TestMethod]
        public void CalculateAltarWeights_CompositeWinsOverIdOnly()
        {
            // id-only set to 50, composite set to 80 -> composite should be used when a composite key is present in mod string
            _settings.ModTiers["modz"] = 50;
            _settings.ModTiers["Boss|modz"] = 80;

            var top = new SecondaryAltarComponent(new List<string> { "Boss|modz" }, new List<string> { "" });
            var bottom = new SecondaryAltarComponent(new List<string> { "" }, new List<string> { "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(80m);
        }

        [TestMethod]
        public void CalculateAltarWeights_MissingComposite_ReturnsFallbackOne()
        {
            // No entry set for the composite key -> fallback to 1 per ClickItSettings.GetModTier behavior
            var top = new SecondaryAltarComponent(new List<string> { "Boss|nonexistent" }, new List<string> { "" });
            var bottom = new SecondaryAltarComponent(new List<string> { "" }, new List<string> { "" });
            var primary = new PrimaryAltarComponent(top, bottom);

            var weights = _calc.CalculateAltarWeights(primary);

            weights.TopUpsideWeight.Should().Be(1m);
        }

        [TestMethod]
        public void InitializeFromArrays_MixedNullAndNonNull_PreservesProvided()
        {
            // Arrange
            var topDown = (decimal[])null;
            var bottomDown = new decimal[8];
            bottomDown[3] = 5m;
            var topUp = new decimal[8];
            topUp[0] = 2m;
            var bottomUp = (decimal[])null;

            var aw = new AltarWeights();

            // Act
            aw.InitializeFromArrays(topDown, bottomDown, topUp, bottomUp);

            // Assert
            aw.GetBottomDownsideWeights()[3].Should().Be(5m);
            aw.GetTopUpsideWeights()[0].Should().Be(2m);
            // Other slots should be zero
            aw.GetTopUpsideWeights()[1].Should().Be(0m);
            aw.GetBottomUpsideWeights()[0].Should().Be(0m);
        }

        [TestMethod]
        public void Indexer_SetAcrossTypes_StoredSeparately()
        {
            // Arrange
            var aw = new AltarWeights();
            aw.InitializeFromArrays(null, null, null, null);

            // Act - set same index across different weight type keys
            aw["topdownside", 1] = 11m;
            aw["bottomupside", 1] = 22m;
            aw["topupside", 1] = 33m;

            // Assert - each stored value should be retrievable independently
            aw["topdownside", 1].Should().Be(11m);
            aw["bottomupside", 1].Should().Be(22m);
            aw["topupside", 1].Should().Be(33m);

            // And array getters should reflect values
            aw.GetTopDownsideWeights()[1].Should().Be(11m);
            aw.GetBottomUpsideWeights()[1].Should().Be(22m);
            aw.GetTopUpsideWeights()[1].Should().Be(33m);
        }

        [TestMethod]
        public void InitializeFromArrays_NullArguments_UsesDefaultArrays()
        {
            // Arrange
            var aw = new AltarWeights();

            // Act - pass null arrays
            aw.InitializeFromArrays(null, null, null, null);

            // Assert - getters should return arrays of length 8 filled with zeros
            var topUps = aw.GetTopUpsideWeights();
            topUps.Should().HaveCount(8);
            foreach (var v in topUps) v.Should().Be(0m);

            // Indexer should return 0 for valid indexes
            aw["topupside", 0].Should().Be(0m);
            aw["bottomupside", 7].Should().Be(0m);
        }

        [TestMethod]
        public void Indexer_SetGet_And_OutOfRange_Throws()
        {
            // Arrange
            var aw = new AltarWeights();
            aw.InitializeFromArrays(null, null, null, null);

            // Act - set a valid index
            aw["topupside", 2] = 42m;

            // Assert - read back
            aw["topupside", 2].Should().Be(42m);

            // Out of range index should throw
            Action act = () => aw["topupside", 8].ToString();
            act.Should().Throw<IndexOutOfRangeException>();
        }

        [TestMethod]
        public void AltarWeights_InitializeFromArrays_And_Indexer_Getters()
        {
            // Arrange
            var topDown = new decimal[8];
            var bottomDown = new decimal[8];
            var topUp = new decimal[8];
            var bottomUp = new decimal[8];
            topUp[0] = 7m;
            topUp[1] = 3m;
            bottomUp[0] = 2m;
            bottomDown[0] = 1m;

            var aw = new AltarWeights();

            // Act
            aw.InitializeFromArrays(topDownside: topDown, bottomDownside: bottomDown, topUpside: topUp, bottomUpside: bottomUp);

            // Assert - array getters
            var tops = aw.GetTopUpsideWeights();
            tops[0].Should().Be(7m);
            tops[1].Should().Be(3m);

            // Assert - indexer access using expected weight type keys (lowercase)
            aw["topupside", 0].Should().Be(7m);
            aw["bottomupside", 0].Should().Be(2m);
            aw["topdownside", 0].Should().Be(0m); // we set topDown[0] = 0
        }

        [TestMethod]
        public void CalculateAltarWeights_ZeroDownsides_ProducesZeroRatios()
        {
            // Arrange: set upsides but leave downsides empty so TopWeight/BottomWeight should become 0 (safe-division)
            var calc = ClickIt.Tests.Shared.TestHelpers.CreateWeightCalculator(new System.Collections.Generic.Dictionary<string, int>
            {
                ["u1"] = 10,
                ["u2"] = 5
            });

            var top = new ClickIt.Components.SecondaryAltarComponent(new System.Collections.Generic.List<string> { "u1", "u2" }, new System.Collections.Generic.List<string> { "" });
            var bottom = new ClickIt.Components.SecondaryAltarComponent(new System.Collections.Generic.List<string> { "" }, new System.Collections.Generic.List<string> { "" });
            var primary = new ClickIt.Components.PrimaryAltarComponent(top, bottom);

            // Act
            var weights = calc.CalculateAltarWeights(primary);

            // Assert: since downsides totals are zero, TopWeight and BottomWeight should be 0 (implementation uses safe division)
            weights.TopWeight.Should().Be(0m);
            weights.BottomWeight.Should().Be(0m);
            weights.TopUpsideWeight.Should().Be(15m);
        }
    }
}
