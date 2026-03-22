using System.Collections.Generic;
using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ClickIt.Services;
using ClickIt.Components;
using ExileCore.PoEMemory;

namespace ClickIt.Tests.Unit
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
            var mi = typeof(AltarService).GetMethod("DetermineAltarType", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var searingObj = mi!.Invoke(null, ["SomePath/CleansingFireAltar/Other"]);
            searingObj.Should().NotBeNull();
            var searing = (AltarType)searingObj!;
            searing.Should().Be(AltarType.SearingExarch);

            var eaterObj = mi.Invoke(null, ["prefix/TangleAltar/suffix"]);
            eaterObj.Should().NotBeNull();
            var eater = (AltarType)eaterObj!;
            eater.Should().Be(AltarType.EaterOfWorlds);

            var unknownObj = mi.Invoke(null, [string.Empty]);
            unknownObj.Should().NotBeNull();
            var unknown = (AltarType)unknownObj!;
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
            var mi = typeof(AltarService).GetMethod("WarmAddedAltarData", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var primary = TestUtils.TestBuilders.BuildPrimary();

            Action noWarmup = () => mi!.Invoke(null, [primary, false]);
            noWarmup.Should().NotThrow();

            Action warmup = () => mi!.Invoke(null, [primary, true]);
            warmup.Should().Throw<TargetInvocationException>()
                .WithInnerException<InvalidOperationException>();
        }
    }
}

