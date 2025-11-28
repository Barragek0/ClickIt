using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ClickIt.Services;
using ClickIt.Components;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;

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

            // Provide null cached labels so GetAltarLabels returns empty
            var service = new AltarService(clickIt, settings, null);

            // Create a minimal PrimaryAltarComponent using nullable-friendly constructors
            var topMods = new SecondaryAltarComponent(null, new List<string>(), new List<string>());
            var bottomMods = new SecondaryAltarComponent(null, new List<string>(), new List<string>());
            var topButton = new AltarButton(null);
            var bottomButton = new AltarButton(null);

            var component = new PrimaryAltarComponent(global::ClickIt.ClickIt.AltarType.Unknown, topMods, topButton, bottomMods, bottomButton);

            // Add and ensure it's present
            bool added = service.AddAltarComponent(component);
            added.Should().BeTrue();
            service.GetAltarComponentsReadOnly().Should().Contain(component);

            // When no labels are present ProcessAltarScanningLogic should clear the repository
            service.ProcessAltarScanningLogic();

            service.GetAltarComponentsReadOnly().Should().BeEmpty();
        }

        [TestMethod]
        public void DetermineAltarType_PrivateMethod_ReturnsExpected()
        {
            // Use reflection to invoke the private static DetermineAltarType method
            var mi = typeof(AltarService).GetMethod("DetermineAltarType", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var searing = (ClickIt.AltarType)mi.Invoke(null, ["SomePath/CleansingFireAltar/Other"]);
            searing.Should().Be(global::ClickIt.ClickIt.AltarType.SearingExarch);

            var eater = (ClickIt.AltarType)mi.Invoke(null, ["prefix/TangleAltar/suffix"]);
            eater.Should().Be(global::ClickIt.ClickIt.AltarType.EaterOfWorlds);

            var unknown = (ClickIt.AltarType)mi.Invoke(null, [string.Empty]);
            unknown.Should().Be(global::ClickIt.ClickIt.AltarType.Unknown);
        }

        [TestMethod]
        public void CreateAltarComponent_WithMockedElements_CreatesComponent()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var service = new AltarService(clickIt, settings, null);

            // Build a mock adapter graph: element -> parent -> altarParent -> (topAltar, bottomAltar)
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

            // Underlying elements can be null for adapter-based tests (SecondaryAltarComponent accepts Element?)
            mockTopAltarAdapter.SetupGet(a => a.Underlying).Returns((Element)null);
            mockBottomAltarAdapter.SetupGet(a => a.Underlying).Returns((Element)null);

            // Call the internal adapter-based creator directly
            var created = service.CreateAltarComponentFromAdapter(mockElementAdapter.Object, global::ClickIt.ClickIt.AltarType.SearingExarch);

            created.Should().NotBeNull();
            created.AltarType.Should().Be(global::ClickIt.ClickIt.AltarType.SearingExarch);
            created.TopMods.Should().NotBeNull();
            created.BottomMods.Should().NotBeNull();
        }

        [TestMethod]
        public void UpdateAltarComponentFromAdapter_SetsModsAndButtons()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var service = new AltarService(clickIt, settings, null);

            var primary = Tests.TestUtils.TestBuilders.BuildPrimary();

            var mockElementAdapter = new Mock<IElementAdapter>();
            // Parent used by AltarButton is retrieved from adapter.Parent.Underlying; provide a parent adapter with null underlying
            var parentAdapter = new Mock<IElementAdapter>();
            mockElementAdapter.SetupGet(a => a.Parent).Returns(parentAdapter.Object);
            parentAdapter.SetupGet(a => a.Underlying).Returns((Element)null);

            var ups = new List<string> { "up1" };
            var downs = new List<string> { "down1" };

            // Call the internal static helper
            AltarService.UpdateAltarComponentFromAdapter(true, primary, mockElementAdapter.Object, ups, downs, true);

            primary.TopMods.Should().NotBeNull();
            primary.TopButton.Should().NotBeNull();
            primary.TopMods.HasUnmatchedMods.Should().BeTrue();
            primary.TopMods.Upsides.Should().Contain("up1");
        }
    }
}

