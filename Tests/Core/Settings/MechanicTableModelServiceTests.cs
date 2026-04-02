using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExileCore.Shared.Nodes;
using System.Collections.Generic;

namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class MechanicTableModelServiceTests
    {
        [TestMethod]
        public void GetTableEntries_BuildsCachedEntryListFromSettingsNodes()
        {
            var settings = new global::ClickIt.ClickItSettings();
            settings.ClickRitualCompleted.Value = false;

            var entries = global::ClickIt.MechanicTableModelService.GetTableEntries(settings);
            var ritualCompleted = entries.Should().ContainSingle(entry => entry.Id == global::ClickIt.Definitions.MechanicIds.RitualCompleted).Subject;

            ritualCompleted.Node.Should().BeSameAs(settings.ClickRitualCompleted);
            ritualCompleted.DefaultEnabled.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRenderEntry_UsesTrimmedSharedSearchMatcher()
        {
            var entry = new global::ClickIt.MechanicToggleTableEntry(
                Id: "ritual-completed",
                DisplayName: "Completed Altars",
                Node: new ToggleNode(true));

            global::ClickIt.MechanicTableModelService.ShouldRenderEntry(entry, moveToClick: false, filter: "  altar  ").Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRenderGroup_ReturnsTrue_WhenChildMatchesTrimmedSharedSearchMatcher()
        {
            var group = new global::ClickIt.MechanicToggleGroupEntry("ritual", "Ritual Altars");
            IReadOnlyList<global::ClickIt.MechanicToggleTableEntry> entries =
            [
                new global::ClickIt.MechanicToggleTableEntry(
                    Id: "ritual-completed",
                    DisplayName: "Completed Altars",
                    Node: new ToggleNode(true),
                    GroupId: "ritual")
            ];

            global::ClickIt.MechanicTableModelService.ShouldRenderGroup(group, entries, moveToClick: false, filter: "  completed  ").Should().BeTrue();
        }
    }
}