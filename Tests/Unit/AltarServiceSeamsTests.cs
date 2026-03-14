using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Services;
using ExileCore.PoEMemory;
using Moq;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarServiceSeamsTests
    {
        [TestMethod]
        public void UpdateAltarComponentFromAdapter_Throws_WhenElementNull()
        {
            var primary = TestUtils.TestBuilders.BuildPrimary();

            FluentActions.Invoking(() => AltarService.UpdateAltarComponentFromAdapter(true, primary, null!, [], [], false))
                .Should().Throw<System.ArgumentNullException>();
        }

        [TestMethod]
        public void UpdateAltarComponentFromAdapter_SetsBottom_WhenTopFalse()
        {
            var primary = TestUtils.TestBuilders.BuildPrimary();

            var mockElementAdapter = new Mock<IElementAdapter>();
            var parentAdapter = new Mock<IElementAdapter>();
            mockElementAdapter.SetupGet(a => a.Parent).Returns(parentAdapter.Object);
            parentAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            var ups = new List<string> { "up1" };
            var downs = new List<string> { "down1" };

            AltarService.UpdateAltarComponentFromAdapter(false, primary, mockElementAdapter.Object, ups, downs, true);

            primary.BottomMods.Should().NotBeNull();
            primary.BottomButton.Should().NotBeNull();
            primary.BottomMods.HasUnmatchedMods.Should().BeTrue();
            primary.BottomMods.Upsides.Should().Contain("up1");
        }

        [TestMethod]
        public void RecordUnmatchedMod_AddsEntriesAndTrimsToFive()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var svc = new AltarService(clickIt, settings, null);

            var mi = typeof(AltarService).GetMethod("RecordUnmatchedMod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                     ?? throw new System.InvalidOperationException("RecordUnmatchedMod method not found.");
            mi.Should().NotBeNull();

            string neg = "N";
            _ = mi.Invoke(svc, ["a1", neg]);
            mi.Invoke(svc, ["b2", neg]);
            mi.Invoke(svc, ["c3", neg]);
            mi.Invoke(svc, ["d4", neg]);
            mi.Invoke(svc, ["e5", neg]);
            mi.Invoke(svc, ["f6", neg]);

            svc.DebugInfo.ModsUnmatched.Should().Be(6);

            svc.DebugInfo.RecentUnmatchedMods.Count.Should().Be(5);
            svc.DebugInfo.RecentUnmatchedMods[0].Should().StartWith("b ");
            svc.DebugInfo.RecentUnmatchedMods[4].Should().StartWith("f ");
        }

        [TestMethod]
        public void RecordUnmatchedMod_DoesNotDuplicateEntries_ButStillIncrementsCounter()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var svc = new AltarService(clickIt, settings, null);

            var mi = typeof(AltarService).GetMethod("RecordUnmatchedMod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                     ?? throw new System.InvalidOperationException("RecordUnmatchedMod method not found.");
            mi.Should().NotBeNull();

            string neg = "X";
            mi.Invoke(svc, ["dup1", neg]);
            mi.Invoke(svc, ["dup1", neg]);
            mi.Invoke(svc, ["other1", neg]);

            svc.DebugInfo.ModsUnmatched.Should().Be(3);
            svc.DebugInfo.RecentUnmatchedMods.Count.Should().Be(2);
            svc.DebugInfo.RecentUnmatchedMods.Should().Contain(s => s.StartsWith("dup"));
            svc.DebugInfo.RecentUnmatchedMods.Should().Contain(s => s.StartsWith("other"));
        }
    }
}
