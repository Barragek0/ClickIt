using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumModifierPriorityMatcherTests
    {
        [TestMethod]
        public void GetModifierPriorityIndex_ReturnsExactMatchBeforeOtherChecks()
        {
            int index = UltimatumModifierPriorityMatcher.GetModifierPriorityIndex(
                "Ruin",
                ["Impending Doom", "Ruin", "Stalking Ruin"]);

            index.Should().Be(1);
        }

        [TestMethod]
        public void GetModifierPriorityIndex_ReturnsPrefixMatch_WhenModifierStartsWithPriorityToken()
        {
            int index = UltimatumModifierPriorityMatcher.GetModifierPriorityIndex(
                "Ruin stacks faster",
                ["Impending Doom", "Ruin", "Stalking Ruin"]);

            index.Should().Be(1);
        }

        [TestMethod]
        public void GetModifierPriorityIndex_ReturnsContainsMatch_WhenPriorityAppearsInsideModifier()
        {
            int index = UltimatumModifierPriorityMatcher.GetModifierPriorityIndex(
                "Monsters gain Ruin over time",
                ["Impending Doom", "Ruin", "Stalking Ruin"]);

            index.Should().Be(1);
        }

        [TestMethod]
        public void GetModifierPriorityIndex_ReturnsMaxValue_WhenNoPriorityMatches()
        {
            int index = UltimatumModifierPriorityMatcher.GetModifierPriorityIndex(
                "Unrelated modifier",
                ["Impending Doom", "Ruin", "Stalking Ruin"]);

            index.Should().Be(int.MaxValue);
        }
    }
}