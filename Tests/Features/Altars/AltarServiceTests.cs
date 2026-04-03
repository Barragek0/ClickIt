using System.Collections.Generic;
using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarServiceTests
    {
        [TestMethod]
        public void ProcessAltarScanningLogic_ClearsComponents_WhenNoLabels()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();

            var service = new AltarService(clickIt, settings, null);

            var topMods = new SecondaryAltarComponent(null, [], []);
            var bottomMods = new SecondaryAltarComponent(null, [], []);
            var topButton = new AltarButton(null);
            var bottomButton = new AltarButton(null);

            var component = new PrimaryAltarComponent(AltarType.Unknown, topMods, topButton, bottomMods, bottomButton);

            bool added = service.AddAltarComponent(component);
            added.Should().BeTrue();
            service.GetAltarComponentsReadOnly().Should().Contain(component);

            service.ProcessAltarScanningLogic();

            service.GetAltarComponentsReadOnly().Should().BeEmpty();
        }

        [TestMethod]
        public void DetermineAltarType_PrivateMethod_ReturnsExpected()
        {
            AltarType searing = AltarService.DetermineAltarType("SomePath/CleansingFireAltar/Other");
            searing.Should().Be(AltarType.SearingExarch);

            AltarType eater = AltarService.DetermineAltarType("prefix/TangleAltar/suffix");
            eater.Should().Be(AltarType.EaterOfWorlds);

            AltarType unknown = AltarService.DetermineAltarType(string.Empty);
            unknown.Should().Be(AltarType.Unknown);
        }

        [TestMethod]
        public void CreateAltarComponent_WithMockedElements_CreatesComponent()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var service = new AltarService(clickIt, settings, null);

            var mockElementAdapter = new Mock<IElementAdapter>();
            var mockParentAdapter = new Mock<IElementAdapter>();
            var mockAltarParentAdapter = new Mock<IElementAdapter>();
            var mockTopAltarAdapter = new Mock<IElementAdapter>();
            var mockBottomAltarAdapter = new Mock<IElementAdapter>();

            mockElementAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockParentAdapter.SetupGet(a => a.Parent).Returns(mockAltarParentAdapter.Object);

            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(0, 1)).Returns(mockTopAltarAdapter.Object);
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(1, 1)).Returns(mockBottomAltarAdapter.Object);

            mockTopAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockBottomAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);

            mockTopAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns(string.Empty);
            mockBottomAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns(string.Empty);

            mockTopAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);
            mockBottomAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            var created = service.CreateAltarComponentFromAdapter(mockElementAdapter.Object, AltarType.SearingExarch);

            created.Should().NotBeNull();
            created.AltarType.Should().Be(AltarType.SearingExarch);
            created.TopMods.Should().NotBeNull();
            created.BottomMods.Should().NotBeNull();
        }

        [TestMethod]
        public void UpdateAltarComponentFromAdapter_SetsModsAndButtons()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();

            var primary = TestUtils.TestBuilders.BuildPrimary();

            var mockElementAdapter = new Mock<IElementAdapter>();
            var parentAdapter = new Mock<IElementAdapter>();
            mockElementAdapter.SetupGet(a => a.Parent).Returns(parentAdapter.Object);
            parentAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            var ups = new List<string> { "up1" };
            var downs = new List<string> { "down1" };

            AltarService.UpdateAltarComponentFromAdapter(true, primary, mockElementAdapter.Object, ups, downs, true);

            primary.TopMods.Should().NotBeNull();
            primary.TopButton.Should().NotBeNull();
            primary.TopMods.HasUnmatchedMods.Should().BeTrue();
            primary.TopMods.Upsides.Should().Contain("up1");
        }

        [TestMethod]
        public void WarmAddedAltarData_DoesNotPrecache_WhenComponentNotAdded()
        {
            var primary = TestUtils.TestBuilders.BuildPrimary();

            Action noWarmup = () => AltarService.WarmAddedAltarData(primary, false);
            noWarmup.Should().NotThrow();

            Action warmup = () => AltarService.WarmAddedAltarData(primary, true);
            warmup.Should().Throw<InvalidOperationException>();
        }
    }
}

