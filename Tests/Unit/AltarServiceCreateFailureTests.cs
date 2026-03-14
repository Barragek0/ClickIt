using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Components;
using ClickIt.Services;
using ExileCore.PoEMemory;
using Moq;

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
            mockElementAdapter.SetupGet(a => a.Parent).Returns((IElementAdapter?)null);

            FluentActions.Invoking(() => service.CreateAltarComponentFromAdapter(mockElementAdapter.Object, AltarType.Unknown))
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

            var mockTopAltarAdapter = new Mock<IElementAdapter>();
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(0, 1)).Returns(mockTopAltarAdapter.Object);
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(1, 1)).Returns((IElementAdapter?)null);

            mockTopAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);

            mockTopAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns(string.Empty);
            mockTopAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            FluentActions.Invoking(() => service.CreateAltarComponentFromAdapter(mockElementAdapter.Object, AltarType.Unknown))
                .Should().Throw<System.InvalidOperationException>();
        }
    }
}
