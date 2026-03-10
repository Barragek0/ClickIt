using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelServiceTests
    {
        [TestMethod]
        public void Constructor_InitializesCachedLabels()
        {
            var gc = (ExileCore.GameController)RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController));
            var service = new Services.LabelService(gc, _ => true);

            service.CachedLabels.Should().NotBeNull();
        }

        [TestMethod]
        public void GetElementsByStringContains_NullLabel_ReturnsEmpty()
        {
            var result = Services.LabelService.GetElementsByStringContains(null, "anything");
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
    }
}
