using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarParserTests
    {
        [TestMethod]
        public void CleanAltarModsText_RemovesFormattingAndRgb()
        {
            string input = "<rgb(255,0,0)>{Gain: Test}\n<valuedefault> {mod} ";
            var cleaned = AltarParser.CleanAltarModsText_NoCache(input);
            cleaned.Should().NotBeNullOrEmpty();
            cleaned.Should().NotContain("<rgb");
            cleaned.Should().NotContain("<valuedefault>");
            cleaned.Should().NotContain("{");
            cleaned.Should().NotContain("}");
        }

        [TestMethod]
        public void ExtractModsFromText_ReturnsNegativeModAndModsList()
        {
            string text = "NegativeType\nmod1\nmod2\n";
            var (neg, mods) = AltarParser.ExtractModsFromText(text);
            neg.Should().Be("NegativeType");
            mods.Should().BeEquivalentTo(new List<string> { "mod1", "mod2" });
        }

        [TestMethod]
        public void ProcessMods_CategorizesModsWithMatcher()
        {
            var mods = new List<string> { "up1", "down1", "unknown" };
            string negativeType = "Negative";

            (List<string> ups, List<string> downs, List<string> unmatched) = AltarParser.ProcessMods(mods, negativeType, (mod, negType) =>
            {
                if (mod.StartsWith("up")) return (true, true, mod + "_id");
                if (mod.StartsWith("down")) return (true, false, mod + "_id");
                return (false, false, string.Empty);
            });

            ups.Should().BeEquivalentTo(new List<string> { "up1_id" });
            downs.Should().BeEquivalentTo(new List<string> { "down1_id" });
            unmatched.Should().BeEquivalentTo(new List<string> { "unknown" });
        }
    }
}
