using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using global::ClickIt.Services;
using global::ClickIt.Components;
using ExileCore.PoEMemory;
using Moq;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarServiceCreateFailureTests
    {
        [TestMethod]
        public void CreateAltarComponentFromAdapter_Throws_WhenParentOrGrandparentMissing()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var service = new AltarService(clickIt, settings, null);

            var mockElementAdapter = new Mock<IElementAdapter>();
            // Parent is null -> should throw
            mockElementAdapter.SetupGet(a => a.Parent).Returns((IElementAdapter?)null);

            FluentActions.Invoking(() => service.CreateAltarComponentFromAdapter(mockElementAdapter.Object, ClickIt.AltarType.Unknown))
                .Should().Throw<System.InvalidOperationException>();
        }

        [TestMethod]
        public void CreateAltarComponentFromAdapter_Throws_WhenTopOrBottomMissing()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var service = new AltarService(clickIt, settings, null);

            var mockElementAdapter = new Mock<IElementAdapter>();
            var mockParentAdapter = new Mock<IElementAdapter>();
            var mockAltarParentAdapter = new Mock<IElementAdapter>();

            mockElementAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockParentAdapter.SetupGet(a => a.Parent).Returns(mockAltarParentAdapter.Object);

            // Fake the case where top child is present but bottom child is missing -> should throw
            var mockTopAltarAdapter = new Mock<IElementAdapter>();
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(0, 1)).Returns(mockTopAltarAdapter.Object);
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(1, 1)).Returns((IElementAdapter?)null);

            // parent link for top child (the implementation expects Parent references)
            mockTopAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);

            // Top/bottom adapters return empty text and null underlying for simplicity
            mockTopAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns(string.Empty);
            mockTopAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            FluentActions.Invoking(() => service.CreateAltarComponentFromAdapter(mockElementAdapter.Object, ClickIt.AltarType.Unknown))
                .Should().Throw<System.InvalidOperationException>();
        }
    }
}
