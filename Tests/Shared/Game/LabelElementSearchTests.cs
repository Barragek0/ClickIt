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
        public void ElementContainsAnyStringsCore_ReturnsTrue_WhenAnyPatternMatches()
        {
            var root = new ElementAdapterStub("alpha");
            root.AddChild(new ElementAdapterStub("beta-marker"));

            bool result = LabelElementSearch.ElementContainsAnyStringsCore(root, ["zzz", "marker"]);

            result.Should().BeTrue();
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

    }
}