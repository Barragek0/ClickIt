using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class TextHelpersTests
    {
        [TestMethod]
        public void GetLine_ReturnsCorrectLine()
        {
            string text = "line1\nline2\nline3";
            TextHelpers.GetLine(text, 0).Should().Be("line1");
            TextHelpers.GetLine(text, 1).Should().Be("line2");
            TextHelpers.GetLine(text, 2).Should().Be("line3");
            TextHelpers.GetLine(text, 3).Should().Be(string.Empty);
            TextHelpers.GetLine(null, 0).Should().Be(string.Empty);
        }

        [TestMethod]
        public void CountLines_WorksWithDifferentLineEndings()
        {
            TextHelpers.CountLines(null).Should().Be(0);
            TextHelpers.CountLines(string.Empty).Should().Be(0);
            TextHelpers.CountLines("a\n b\n c").Should().Be(3);
            TextHelpers.CountLines("a\r\nb\r\nc").Should().Be(3);
        }

        [TestMethod]
        public void GetLine_OutOfRange_ReturnsEmpty()
        {
            var text = "a\nb\nc";
            TextHelpers.GetLine(text, -1).Should().BeEmpty();
            TextHelpers.GetLine(text, 10).Should().BeEmpty();
        }
    }
}
