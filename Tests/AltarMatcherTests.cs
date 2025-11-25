using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests
{
    [TestClass]
    public class AltarMatcherTests
    {
        [TestMethod]
        public void CleanAltarModsText_RemovesArbitraryTag()
        {
            var matcher = new ClickIt.Services.AltarMatcher();

            string cleaned = matcher.CleanAltarModsText("<divine>");

            cleaned.Should().Be(string.Empty);
        }

        [TestMethod]
        public void CleanAltarModsText_RemovesRgbAndClosingTag()
        {
            var matcher = new ClickIt.Services.AltarMatcher();

            string dirty = "<rgb(255,128,0)>Final Boss drops # additional Divine Orbs</rgb>";
            string cleaned = matcher.CleanAltarModsText(dirty);

            // Spaces are removed by the cleaning logic and both opening/closing tags should be stripped
            cleaned.Should().Contain("FinalBossdrops");
            cleaned.Should().Contain("DivineOrbs");
            cleaned.Should().NotContain("<");
            cleaned.Should().NotContain(">");
        }

        [TestMethod]
        public void CleanAltarModsText_MultipleTags_NotGreedy_RemovesEachTagSeparately()
        {
            var matcher = new ClickIt.Services.AltarMatcher();

            string dirty = "<divine>some <scarab>thing</scarab> end";
            string cleaned = matcher.CleanAltarModsText(dirty);

            // After cleaning, spaces are stripped and tags removed
            cleaned.Should().Be("somethingend");
        }

        [TestMethod]
        public void CleanAltarModsText_RemovesAdjacentTagsAndKeepsContent()
        {
            var matcher = new ClickIt.Services.AltarMatcher();

            string dirty = "<a>one</a><b>two</b>";
            string cleaned = matcher.CleanAltarModsText(dirty);

            // Spaces are removed as part of cleaning; expected concatenation
            cleaned.Should().Be("onetwo");
        }
    }
}
