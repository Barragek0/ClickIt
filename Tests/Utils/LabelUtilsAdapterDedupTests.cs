using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class LabelUtilsAdapterDedupTests
    {
        // Adapter that returns the same child instance for both container indexes
        private class DupAdapter : Services.IElementAdapter
        {
            private readonly Services.IElementAdapter _child;
            private readonly string _text;
            public DupAdapter(string text, Services.IElementAdapter child)
            {
                _text = text;
                _child = child;
            }

            public ExileCore.PoEMemory.Element? Underlying => null;
            public Services.IElementAdapter? Parent => null;
            public bool IsValid => true;
            public Services.IElementAdapter? GetChildFromIndices(int containerIndex, int childIndex)
            {
                // For index 0 and 1, return the same child for childIndex 0
                if (childIndex == 0) return _child;
                return null;
            }
            public SharpDX.RectangleF GetClientRect() => new(0, 0, 1, 1);
            public string GetText(int maxChars) => _text;
        }

        private class SimpleAdapter : Services.IElementAdapter
        {
            private readonly string _text;
            public SimpleAdapter(string text) => _text = text;
            public ExileCore.PoEMemory.Element? Underlying => null;
            public Services.IElementAdapter? Parent => null;
            public bool IsValid => true;
            public Services.IElementAdapter? GetChildFromIndices(int a, int b) => null;
            public SharpDX.RectangleF GetClientRect() => new(0, 0, 1, 1);
            public string GetText(int maxChars) => _text;
        }

        [TestMethod]
        public void GetElementsByStringContainsForTests_DeduplicatesRepeatedChildAcrossContainers()
        {
            var child = new SimpleAdapter("match-me");
            var root = new DupAdapter("root", child);

            var res = LabelUtils.GetElementsByStringContainsForTests(root, "match");

            // The repeated child should only appear once in the result list
            res.Should().HaveCount(1);
            res.Should().ContainSingle().Which.Should().Be(child);
        }

        [TestMethod]
        public void GetElementsByStringContainsForTests_YieldsDistinctChildren_WhenDifferent()
        {
            var childA = new SimpleAdapter("match-a");
            var childB = new SimpleAdapter("match-b");

            // Adapter that returns two distinct children via differing childIndex
            var root = new TwoChildAdapter("root", childA, childB);

            var res = LabelUtils.GetElementsByStringContainsForTests(root, "match");

            res.Should().HaveCount(2);
            res.Should().Contain(childA);
            res.Should().Contain(childB);
        }

        private class TwoChildAdapter : Services.IElementAdapter
        {
            private readonly Services.IElementAdapter _a;
            private readonly Services.IElementAdapter _b;
            private readonly string _text;
            public TwoChildAdapter(string text, Services.IElementAdapter a, Services.IElementAdapter b)
            {
                _text = text;
                _a = a;
                _b = b;
            }

            public ExileCore.PoEMemory.Element? Underlying => null;
            public Services.IElementAdapter? Parent => null;
            public bool IsValid => true;
            public Services.IElementAdapter? GetChildFromIndices(int containerIndex, int childIndex)
            {
                // For both container indices, childIndex 0 -> a, 1 -> b
                if (childIndex == 0) return _a;
                if (childIndex == 1) return _b;
                return null;
            }
            public SharpDX.RectangleF GetClientRect() => new(0, 0, 1, 1);
            public string GetText(int maxChars) => _text;
        }
    }
}
