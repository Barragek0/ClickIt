using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt.Services;
using SharpDX;
using ExileCore.PoEMemory;
using System.Collections.Generic;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class LabelUtilsAdapterTests
    {
        private class FakeAdapter : IElementAdapter
        {
            public Element? Underlying => null;
            public IElementAdapter? Parent { get; }
            private readonly string _text;
            private readonly List<IElementAdapter> _children = new();
            public bool IsValid => true;
            public FakeAdapter(string text)
            {
                _text = text;
            }

            public IElementAdapter? GetChildFromIndices(int a, int b)
            {
                if (b < 0 || b >= _children.Count) return null;
                return _children[b];
            }

            public void AddChild(FakeAdapter c) => _children.Add(c);

            public RectangleF GetClientRect() => new RectangleF(0, 0, 10, 10);

            public string GetText(int maxChars) => _text;
        }

        [TestMethod]
        public void FakeAdapter_Basics_WorkAsExpected()
        {
            var root = new FakeAdapter("root text");
            var child1 = new FakeAdapter("child1 text");
            var child2 = new FakeAdapter("child2 text");
            root.AddChild(child1);
            root.AddChild(child2);

            // Basic properties
            root.IsValid.Should().BeTrue();
            root.GetText(512).Should().Be("root text");

            // Children access
            var child = root.GetChildFromIndices(0, 0);
            child.Should().NotBeNull();
            child!.GetText(512).Should().Be("child1 text");

            var childMissing = root.GetChildFromIndices(0, 5);
            childMissing.Should().BeNull();

            var r = root.GetClientRect();
            r.Width.Should().BeGreaterThan(0);
            r.Height.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void IsPathForClickableObject_MatchesKnownPatterns()
        {
            LabelUtils.IsPathForClickableObject("Some/DelveMineral/Path").Should().BeTrue();
            LabelUtils.IsPathForClickableObject("This/contains/Harvest/Extractor").Should().BeTrue();
            LabelUtils.IsPathForClickableObject("CleansingFireAltar").Should().BeTrue();
            LabelUtils.IsPathForClickableObject("Random/NotRelevant").Should().BeFalse();
        }
    }
}
