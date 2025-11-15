#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Configuration
{
    [TestClass]
    public class DefaultWeightInitializationTests
    {
        [TestMethod]
        public void InitializeDefaultWeights_PopulatesCompositeKeys_ForAllUpsideAndDownside()
        {
            // Use the test stub ClickItSettings available in Tests/Shared/TestStubs.cs
            var settings = new ClickIt.ClickItSettings();

            // Ensure clear start
            settings.ModTiers.Clear();

            // Call initialization
            settings.InitializeDefaultWeights();

            // Verify all Upside composite keys
            foreach (var tuple in ClickIt.Constants.AltarModsConstants.UpsideMods)
            {
                var (Id, _, Type, DefaultValue) = tuple;
                var composite = $"{Type}|{Id}";
                settings.ModTiers.Should().ContainKey(composite);
                settings.ModTiers[composite].Should().Be(DefaultValue);
            }

            // Verify all Downside composite keys
            foreach (var tuple in ClickIt.Constants.AltarModsConstants.DownsideMods)
            {
                var (Id, _, Type, DefaultValue) = tuple;
                var composite = $"{Type}|{Id}";
                settings.ModTiers.Should().ContainKey(composite);
                settings.ModTiers[composite].Should().Be(DefaultValue);
            }
        }

        [TestMethod]
        public void EnsureAllModsHaveWeights_AfterInitialization_PopulatesCompositeKeys()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.ModTiers.Clear();

            // Act
            settings.InitializeDefaultWeights();

            // Assert - there should be at least one entry per constants list
            settings.ModTiers.Should().NotBeEmpty();
            // Known composite key example
            bool containsExample = settings.ModTiers.ContainsKey("Boss|Final Boss drops # additional Divine Orbs");
            containsExample.Should().BeTrue();
        }

        [TestMethod]
        public void InitializeDefaultWeights_PopulatesAtLeastConstantsCount()
        {
            var s = new ClickIt.ClickItSettings();
            s.ModTiers.Clear();
            s.InitializeDefaultWeights();

            int expected = ClickIt.Constants.AltarModsConstants.UpsideMods.Count + ClickIt.Constants.AltarModsConstants.DownsideMods.Count;
            s.ModTiers.Count.Should().BeGreaterOrEqualTo(expected);
        }
    }
}
#endif
