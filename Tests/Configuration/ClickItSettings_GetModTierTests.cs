using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Configuration
{
    [TestClass]
    public class ClickItSettings_GetModTierTests
    {
        [DataTestMethod]
        [DataRow("")]
        [DataRow(null)]
        [DataRow("unknown_mod")]
        public void GetModTier_IdOnly_ReturnsDefaultForUnknown(string id)
        {
            var s = new ClickItSettings();
            s.GetModTier(id).Should().Be(1);
        }

        [TestMethod]
        public void GetModTier_CompositeKey_ResolvesComposite()
        {
            var s = new ClickItSettings();
            s.ModTiers["Player|modA"] = 42;
            s.GetModTier("modA", "Player").Should().Be(42);
            s.GetModTier("modA").Should().Be(1); // id-only not present
        }

        [TestMethod]
        public void GetModTier_IdOnly_TakesPrecedenceOverComposite()
        {
            var s = new ClickItSettings();
            // both composite and id-only present; id-only should be returned by id-only lookup
            s.ModTiers["Boss|modB"] = 9;
            s.ModTiers["modB"] = 5;

            s.GetModTier("modB").Should().Be(5);
            s.GetModTier("modB", "Boss").Should().Be(9);
        }

        [DataTestMethod]
        [DataRow("modA", "Player", 7)]
        [DataRow("MODa", "player", 1)]
        [DataRow("moda", "Player", 1)]
        public void GetModTier_CompositeKey_CaseSensitivityBehavior(string id, string target, int expected)
        {
            var s = new ClickItSettings();
            s.ModTiers[$"Player|modA"] = 7; // stored with canonical casing

            // Current implementation uses exact composite key lookup (case-sensitive)
            s.GetModTier(id, target).Should().Be(expected);
        }
    }
}
