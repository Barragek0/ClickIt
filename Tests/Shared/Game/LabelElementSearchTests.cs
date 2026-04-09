namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class LabelElementSearchTests
    {
        [TestMethod]
        public void GetElementByStringCore_ReturnsNestedMatch()
        {
            var root = new ElementAdapterStub("root");
            var child = new ElementAdapterStub("child");
            var target = new ElementAdapterStub("target");
            child.AddChild(target);
            root.AddChild(child);

            IElementAdapter? match = LabelElementSearch.GetElementByStringCore(root, "target");

            match.Should().BeSameAs(target);
        }

        [TestMethod]
        public void GetElementByStringCore_ReturnsNull_WhenRootIsNullOrNoMatchExists()
        {
            LabelElementSearch.GetElementByStringCore(null, "target").Should().BeNull();

            var root = new ElementAdapterStub("root");
            root.AddChild(new ElementAdapterStub("child"));

            LabelElementSearch.GetElementByStringCore(root, "missing").Should().BeNull();
        }

        [TestMethod]
        public void ElementContainsAnyStringsCore_ReturnsTrue_WhenAnyPatternMatches()
        {
            var root = new ElementAdapterStub("alpha");
            root.AddChild(new ElementAdapterStub("beta-marker"));

            bool result = LabelElementSearch.ElementContainsAnyStringsCore(root, ["zzz", "marker"]);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ElementContainsAnyStringsCore_ReturnsFalse_WhenRootIsNull_PatternsAreEmpty_OrNothingMatches()
        {
            LabelElementSearch.ElementContainsAnyStringsCore(null, ["marker"]).Should().BeFalse();

            var root = new ElementAdapterStub("alpha");
            root.AddChild(new ElementAdapterStub("beta"));

            LabelElementSearch.ElementContainsAnyStringsCore(root, Array.Empty<string>()).Should().BeFalse();
            LabelElementSearch.ElementContainsAnyStringsCore(root, ["zzz", "yyy"]).Should().BeFalse();
        }

        [TestMethod]
        public void GetElementsByStringContainsCore_ReturnsRootAndMatchingChildren()
        {
            var root = new ElementAdapterStub("value:valuedefault");
            root.AddChild(new ElementAdapterStub("not-it"));
            root.AddChild(new ElementAdapterStub("valuedefault-second"));

            var results = LabelElementSearch.GetElementsByStringContainsCore(root, "valuedefault");

            results.Should().HaveCount(2);
            results[0].Should().BeSameAs(root);
        }

        [TestMethod]
        public void GetElementsByStringContainsCore_ReturnsEmpty_WhenRootIsNull_AndDoesNotDuplicateSharedChildren()
        {
            LabelElementSearch.GetElementsByStringContainsCore(null, "value").Should().BeEmpty();

            var root = new ElementAdapterStub("root");
            var child = new ElementAdapterStub("marker-child");
            root.AddChild(child);

            var results = LabelElementSearch.GetElementsByStringContainsCore(root, "marker");

            results.Should().ContainSingle().Which.Should().BeSameAs(child);
        }

        [TestMethod]
        public void GetElementsByStringContainsCore_DeduplicatesChildrenReturnedFromBothAdapterContainers()
        {
            var root = new ElementAdapterStub("root");
            var first = new ElementAdapterStub("marker-one");
            var second = new ElementAdapterStub("marker-two");
            root.AddChild(first);
            root.AddChild(second);

            var results = LabelElementSearch.GetElementsByStringContainsCore(root, "marker");

            results.Should().HaveCount(2);
            results.Should().ContainInOrder(first, second);
        }

        [TestMethod]
        public void ElementContainsAnyStringsCore_SkipsEmptyText_AndContinuesTraversingChildren()
        {
            var root = new ElementAdapterStub(string.Empty);
            var emptyChild = new ElementAdapterStub(string.Empty);
            var target = new ElementAdapterStub("marker-child");
            emptyChild.AddChild(target);
            root.AddChild(emptyChild);

            bool result = LabelElementSearch.ElementContainsAnyStringsCore(root, ["marker"]);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ClearThreadLocalStorage_RemovesPreviouslyTrackedElements()
        {
            LabelElementSearch.ClearThreadLocalStorage();
            LabelElementSearch.AddNullElementToThreadLocal();

            LabelElementSearch.GetThreadLocalElementsCount().Should().Be(1);

            LabelElementSearch.ClearThreadLocalStorage();

            LabelElementSearch.GetThreadLocalElementsCount().Should().Be(0);
        }

    }
}