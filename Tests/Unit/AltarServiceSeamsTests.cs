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
    public class AltarServiceSeamsTests
    {
        [TestMethod]
        public void UpdateAltarComponentFromAdapter_Throws_WhenElementNull()
        {
            var primary = Tests.TestUtils.TestBuilders.BuildPrimary();

            // Passing null for the element parameter should throw ArgumentNullException
            FluentActions.Invoking(() => AltarService.UpdateAltarComponentFromAdapter(true, primary, null, new List<string>(), new List<string>(), false))
                .Should().Throw<System.ArgumentNullException>();
        }

        [TestMethod]
        public void UpdateAltarComponentFromAdapter_SetsBottom_WhenTopFalse()
        {
            var primary = Tests.TestUtils.TestBuilders.BuildPrimary();

            var mockElementAdapter = new Mock<IElementAdapter>();
            var parentAdapter = new Mock<IElementAdapter>();
            mockElementAdapter.SetupGet(a => a.Parent).Returns(parentAdapter.Object);
            parentAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            var ups = new List<string> { "up1" };
            var downs = new List<string> { "down1" };

            // call for bottom part
            AltarService.UpdateAltarComponentFromAdapter(false, primary, mockElementAdapter.Object, ups, downs, true);

            primary.BottomMods.Should().NotBeNull();
            primary.BottomButton.Should().NotBeNull();
            primary.BottomMods.HasUnmatchedMods.Should().BeTrue();
            primary.BottomMods.Upsides.Should().Contain("up1");
        }
    }
}
