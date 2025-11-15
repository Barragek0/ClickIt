using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Text;

namespace ClickIt.Tests.Parser
{
    [TestClass]
    public class ParserFuzzTests
    {
        private static readonly Random Rng = new Random(0);

        private static string CleanAltarModsText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            // Minimal robust cleaning used by production parsers: remove angle-bracket markup and collapse spaces
            var cleaned = text.Replace("<valuedefault>", "").Replace("{", "").Replace("}", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "<[^>]+>", string.Empty);
            // Remove any remaining stray angle brackets and whitespace
            cleaned = cleaned.Replace("<", string.Empty).Replace(">", string.Empty).Replace(" ", string.Empty);
            return cleaned;
        }

        [TestMethod]
        public void RandomizedMarkup_Cleaning_ShouldRemoveMarkupAndBeIdempotent()
        {
            for (int i = 0; i < 500; i++)
            {
                var s = GenerateRandomMarkupString(Rng.Next(5, 80));
                var first = CleanAltarModsText(s);
                var second = CleanAltarModsText(first);
                // No remaining markup characters
                first.Should().NotContain("<").And.NotContain(">");
                // Cleaning should be idempotent
                second.Should().Be(first);
            }
        }

        private static string GenerateRandomMarkupString(int len)
        {
            var sb = new StringBuilder();
            var choices = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 <>/{}()#%:-,";
            for (int i = 0; i < len; i++)
            {
                sb.Append(choices[Rng.Next(choices.Length)]);
            }
            // Inject some common tags randomly
            if (Rng.NextDouble() < 0.5)
                sb.Insert(Rng.Next(sb.Length + 1), "<rgb(255,128,0)>");
            if (Rng.NextDouble() < 0.3)
                sb.Insert(Rng.Next(sb.Length + 1), "</rgb>");
            if (Rng.NextDouble() < 0.2)
                sb.Insert(Rng.Next(sb.Length + 1), "<enchanted>");
            return sb.ToString();
        }
    }
}
