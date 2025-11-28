using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelServiceTests
    {
        [TestMethod]
        public void GetElementsByStringContains_NullLabel_ReturnsEmpty()
        {
            var result = Services.LabelService.GetElementsByStringContains(null, "anything");
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
    }
}
