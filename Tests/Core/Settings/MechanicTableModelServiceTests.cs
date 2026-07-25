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
            var secureRepository = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.HeistSecureRepository).Subject;
            var heistHazards = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.HeistHazards).Subject;
            var regularDoors = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.Doors).Subject;
            var heistDoors = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.HeistDoors).Subject;
            var alvaTempleDoors = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.AlvaTempleDoors).Subject;

            ritualCompleted.Node.Should().BeSameAs(settings.ClickRitualCompleted);
            ritualCompleted.DefaultEnabled.Should().BeTrue();
            secureRepository.Node.Should().BeSameAs(settings.ClickHeistSecureRepository);
            secureRepository.GroupId.Should().Be("heist");
            heistHazards.Node.Should().BeSameAs(settings.ClickHeistHazards);
            heistHazards.GroupId.Should().Be("heist");
            heistHazards.Subgroup.Should().Be("Hazards");
            heistHazards.DefaultEnabled.Should().BeFalse();
            regularDoors.Node.Should().BeSameAs(settings.ClickDoors);
            regularDoors.GroupId.Should().Be("doors");
            heistDoors.Node.Should().BeSameAs(settings.ClickHeistDoors);
            heistDoors.GroupId.Should().Be("heist");
            heistDoors.DefaultEnabled.Should().BeFalse();
            alvaTempleDoors.Node.Should().BeSameAs(settings.ClickAlvaTempleDoors);
            alvaTempleDoors.GroupId.Should().Be("doors");
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