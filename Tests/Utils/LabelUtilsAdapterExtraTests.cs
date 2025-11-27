using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class LabelUtilsAdapterExtraTests
    {
        private class FakeAdapter : Services.IElementAdapter
        {
            private readonly string _text;
            public FakeAdapter(string text) => _text = text;
            public ExileCore.PoEMemory.Element? Underlying => null;
            public Services.IElementAdapter? Parent => null;
            public bool IsValid => true;
            public Services.IElementAdapter? GetChildFromIndices(int a, int b) => null;
            public SharpDX.RectangleF GetClientRect() => new SharpDX.RectangleF(0, 0, 1, 1);
            public string GetText(int maxChars) => _text;
        }

        [TestMethod]
        public void ElementContainsAnyStringsForTests_EmptyPatternMatchesNonEmptyText()
        {
            var root = new FakeAdapter("root text");
            // pattern empty string should match any non-empty text (string.Contains("") == true)
            var ok = LabelUtils.ElementContainsAnyStringsForTests(root, new[] { string.Empty });
            ok.Should().BeTrue();
        }

        [TestMethod]
        public void ElementContainsAnyStringsForTests_EmptyPatternDoesNotMatchEmptyText()
        {
            var root = new FakeAdapter(string.Empty);
            var ok = LabelUtils.ElementContainsAnyStringsForTests(root, new[] { string.Empty });
            ok.Should().BeFalse();
        }
    }
}
