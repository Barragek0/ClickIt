using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class MechanicTableModelServiceTests
    {
        [TestMethod]
        public void GetTableEntries_BuildsCachedEntryListFromSettingsNodes()
        {
            var settings = new ClickItSettings();
            settings.ClickRitualCompleted.Value = false;

            var entries = MechanicTableModelService.GetTableEntries(settings);
            var ritualCompleted = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.RitualCompleted).Subject;

            ritualCompleted.Node.Should().BeSameAs(settings.ClickRitualCompleted);
            ritualCompleted.DefaultEnabled.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRenderEntry_UsesTrimmedSharedSearchMatcher()
        {
            var entry = new MechanicToggleTableEntry(
                Id: "ritual-completed",
                DisplayName: "Completed Altars",
                Node: new ToggleNode(true));

            MechanicTableModelService.ShouldRenderEntry(entry, moveToClick: false, filter: "  altar  ").Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRenderGroup_ReturnsTrue_WhenChildMatchesTrimmedSharedSearchMatcher()
        {
            var group = new MechanicToggleGroupEntry("ritual", "Ritual Altars");
            IReadOnlyList<MechanicToggleTableEntry> entries =
            [
                new MechanicToggleTableEntry(
                    Id: "ritual-completed",
                    DisplayName: "Completed Altars",
                    Node: new ToggleNode(true),
                    GroupId: "ritual")
            ];

            MechanicTableModelService.ShouldRenderGroup(group, entries, moveToClick: false, filter: "  completed  ").Should().BeTrue();
        }
    }
}