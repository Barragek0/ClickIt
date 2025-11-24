using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class TextHelpersTests
    {
        [TestMethod]
        public void GetLine_ReturnsCorrectLine_WhenTextAndIndexValid()
        {
            string text = "line1\nline2\nline3";
            TextHelpers.GetLine(text, 0).Should().Be("line1");
            TextHelpers.GetLine(text, 1).Should().Be("line2");
            TextHelpers.GetLine(text, 2).Should().Be("line3");
        }

        [TestMethod]
        public void GetLine_ReturnsEmpty_WhenIndexOutOfRangeOrTextNull()
        {
            TextHelpers.GetLine(null, 0).Should().Be(string.Empty);
            TextHelpers.GetLine("single", 5).Should().Be(string.Empty);
        }

        [TestMethod]
        public void CountLines_ReturnsCorrectCount_ForVariousInputs()
        {
            TextHelpers.CountLines(null).Should().Be(0);
            TextHelpers.CountLines(string.Empty).Should().Be(0);
            TextHelpers.CountLines("one").Should().Be(1);
            TextHelpers.CountLines("a\nb\nc").Should().Be(3);
        }
    }
}
