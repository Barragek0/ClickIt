using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class TextHelpersTests
    {
        [DataTestMethod]
        [DataRow("line1\nline2\nline3", 0, "line1")]
        [DataRow("line1\nline2\nline3", 1, "line2")]
        [DataRow("line1\nline2\nline3", 2, "line3")]
        [DataRow("line1\nline2\nline3", 3, "")] // out of range
        [DataRow(null, 0, "")] // null input
        [DataRow("singleline", 0, "singleline")]
        [DataRow("trailing\n", 1, "")] // trailing newline => empty second line
        [DataRow("\r\ncrlf\r\nsecond", 1, "crlf")] // CRLF handling - index 1 is the middle line after CR removal
        public void GetLine_VariousInputs_ReturnsExpected(string text, int index, string expected)
        {
            TextHelpers.GetLine(text, index).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(null, 0)]
        [DataRow("", 0)]
        [DataRow("a\n b\n c", 3)]
        [DataRow("a\r\nb\r\nc", 3)]
        [DataRow("onlyone", 1)]
        [DataRow("\n\n", 3)] // 3 lines when there are two newlines
        public void CountLines_VariousInputs_ReturnsExpected(string text, int expected)
        {
            TextHelpers.CountLines(text).Should().Be(expected);
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
