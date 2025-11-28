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
            private readonly List<IElementAdapter> _children = [];
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

            public RectangleF GetClientRect() => new(0, 0, 10, 10);

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

        [TestMethod]
        public void GetElementsByStringContainsForTests_FindsRootAndChildren()
        {
            var root = new FakeAdapter("root contains match");
            var child1 = new FakeAdapter("child1 match");
            var child2 = new FakeAdapter("child2");
            root.AddChild(child1);
            root.AddChild(child2);

            var res = LabelUtils.GetElementsByStringContainsForTests(root, "match");
            res.Should().HaveCount(2);
            res.Should().Contain(child1);
            res.Should().Contain(root);
        }

        [TestMethod]
        public void GetElementByStringForTests_ReturnsFirstExactMatch()
        {
            var root = new FakeAdapter("root");
            var child1 = new FakeAdapter("findme");
            var child2 = new FakeAdapter("findme");
            root.AddChild(child1);
            root.AddChild(child2);

            var found = LabelUtils.GetElementByStringForTests(root, "findme");
            // The depth-first LIFO traversal will find the last child pushed first (child2)
            found.Should().Be(child2);
        }

        [TestMethod]
        public void ElementContainsAnyStringsForTests_ReturnsTrueWhenAnyPatternMatches()
        {
            var root = new FakeAdapter("root text");
            var child1 = new FakeAdapter("hello world");
            root.AddChild(child1);

            var ok = LabelUtils.ElementContainsAnyStringsForTests(root, new[] { "nomatch", "world" });
            ok.Should().BeTrue();
        }

        [TestMethod]
        public void ElementContainsAnyStringsForTests_ReturnsFalseWhenNoPatternMatches()
        {
            var root = new FakeAdapter("root text");
            var child = new FakeAdapter("nothing here");
            root.AddChild(child);

            var ok = LabelUtils.ElementContainsAnyStringsForTests(root, new[] { "abc", "def" });
            ok.Should().BeFalse();
        }

        [TestMethod]
        public void ElementContainsAnyStringsForTests_EmptyPatternMatchesNonEmptyText()
        {
            var root = new FakeAdapter("root text");
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

        [TestMethod]
        public void GetElementsByStringContainsForTests_ReturnsEmpty_WhenNoMatches()
        {
            var root = new FakeAdapter("root");
            var child = new FakeAdapter("child");
            root.AddChild(child);

            var res = LabelUtils.GetElementsByStringContainsForTests(root, "nomatch");
            res.Should().BeEmpty();
        }

        [TestMethod]
        public void GetElementByStringForTests_ReturnsNull_WhenNotFound()
        {
            var root = new FakeAdapter("root");
            var child = new FakeAdapter("child");
            root.AddChild(child);

            var found = LabelUtils.GetElementByStringForTests(root, "missing");
            found.Should().BeNull();
        }

        [TestMethod]
        public void GetElementByStringForTests_FindsDeepNestedChild()
        {
            var root = new FakeAdapter("root");
            var level1 = new FakeAdapter("level1");
            var level2 = new FakeAdapter("level2");
            var target = new FakeAdapter("target");

            root.AddChild(level1);
            level1.AddChild(level2);
            level2.AddChild(target);

            var found = LabelUtils.GetElementByStringForTests(root, "target");
            found.Should().Be(target);
        }

        [TestMethod]
        public void GetElementsByStringContainsForTests_NullLabel_ReturnsEmpty()
        {
            var res = LabelUtils.GetElementsByStringContainsForTests(null, "whatever");
            res.Should().NotBeNull();
            res.Should().BeEmpty();
        }

        [TestMethod]
        public void ElementContainsAnyStringsForTests_EmptyPatterns_ReturnsFalse()
        {
            var root = new FakeAdapter("root text");
            var ok = LabelUtils.ElementContainsAnyStringsForTests(root, new string[0]);
            ok.Should().BeFalse();
        }
    }
}
