using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using Moq;

namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarServiceBehaviorTests
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

            string neg = "N";
            svc.RecordUnmatchedMod("a1", neg);
            svc.RecordUnmatchedMod("b2", neg);
            svc.RecordUnmatchedMod("c3", neg);
            svc.RecordUnmatchedMod("d4", neg);
            svc.RecordUnmatchedMod("e5", neg);
            svc.RecordUnmatchedMod("f6", neg);

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

            string neg = "X";
            svc.RecordUnmatchedMod("dup1", neg);
            svc.RecordUnmatchedMod("dup1", neg);
            svc.RecordUnmatchedMod("other1", neg);

            svc.DebugInfo.ModsUnmatched.Should().Be(3);
            svc.DebugInfo.RecentUnmatchedMods.Count.Should().Be(2);
            svc.DebugInfo.RecentUnmatchedMods.Should().Contain(s => s.StartsWith("dup"));
            svc.DebugInfo.RecentUnmatchedMods.Should().Contain(s => s.StartsWith("other"));
        }
    }
}
