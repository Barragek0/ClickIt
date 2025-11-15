// Disabled: duplicate of the test in DefaultWeightInitializationTests.cs
#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using ClickIt;

namespace ClickIt.Tests.Configuration
{
    [TestClass]
    public class DefaultWeightCountsTests
    {
        [TestMethod]
        public void InitializeDefaultWeights_PopulatesAtLeastConstantsCount()
        {
            var s = new ClickItSettings();
            s.ModTiers.Clear();
            s.InitializeDefaultWeights();

            int expected = AltarModsConstants.UpsideMods.Count + AltarModsConstants.DownsideMods.Count;
            s.ModTiers.Count.Should().BeGreaterOrEqualTo(expected);
        }
    }
}
#endif
