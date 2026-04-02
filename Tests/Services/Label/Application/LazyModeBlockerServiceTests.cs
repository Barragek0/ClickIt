using ClickIt.Services.Label.Application;
using ExileCore.PoEMemory.Elements;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Label.Application
{
    [TestClass]
    public class LazyModeBlockerServiceTests
    {
        [TestMethod]
        public void HasRestrictedItemsOnScreen_DelegatesToCoreCheck()
        {
            IReadOnlyList<LabelOnGround>? capturedLabels = null;
            IReadOnlyList<LabelOnGround> labels = [];
            var service = new LazyModeBlockerService(allLabels =>
            {
                capturedLabels = allLabels;
                return true;
            });

            bool result = service.HasRestrictedItemsOnScreen(labels);

            result.Should().BeTrue();
            capturedLabels.Should().BeSameAs(labels);
        }
    }
}