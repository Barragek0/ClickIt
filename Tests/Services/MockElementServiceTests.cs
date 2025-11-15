using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class MockElementServiceTests
    {
        [TestMethod]
        public void GetElementByString_ReturnsElementWhenContains()
        {
            var el = MockElementService.CreateBasicElement();
            el.Text = "someprefix target_token suffix";

            var found = MockElementService.GetElementByString(el, "target_token");
            found.Should().BeSameAs(el);

            var notFound = MockElementService.GetElementByString(el, "missing");
            notFound.Should().BeNull();
        }

        [TestMethod]
        public void GetElementsByStringContains_ReturnsListWhenMatches()
        {
            var el = MockElementService.CreateBasicElement();
            el.Text = "contains_one two";

            var results = MockElementService.GetElementsByStringContains(el, "contains_one");
            results.Should().ContainSingle().Which.Should().BeSameAs(el);

            var none = MockElementService.GetElementsByStringContains(el, "absent");
            none.Should().BeEmpty();
        }

        [TestMethod]
        public void IsValidElement_RejectsNullOrEmptyText()
        {
            MockElementService.IsValidElement(null).Should().BeFalse();
            MockElementService.IsValidElement(new MockElement { Text = "" }).Should().BeFalse();
            MockElementService.IsValidElement(new MockElement { Text = "ok" }).Should().BeTrue();
        }

        [TestMethod]
        public void CreateAltarElement_IncludesModsInText()
        {
            var el = MockElementService.CreateAltarElement("PlayerDrops", new[] { "up1", "up2" }, new[] { "down1" });
            el.Text.Should().Contain("PlayerDrops");
            el.Text.Should().Contain("up1");
            el.Text.Should().Contain("down1");
        }

        [TestMethod]
        public void CreateElementAtPosition_And_GetElementCenter_ReturnsPosition()
        {
            var el = MockElementService.CreateElementAtPosition(12.5f, 99.9f);
            var center = MockElementService.GetElementCenter(el);
            center.X.Should().BeApproximately(12.5f, 0.001f);
            center.Y.Should().BeApproximately(99.9f, 0.001f);
        }

        [TestMethod]
        public void IsElementVisible_BasedOnText()
        {
            MockElementService.IsElementVisible(null).Should().BeFalse();
            MockElementService.IsElementVisible(new MockElement { Text = "" }).Should().BeFalse();
            MockElementService.IsElementVisible(new MockElement { Text = "visible" }).Should().BeTrue();
        }
    }
}
