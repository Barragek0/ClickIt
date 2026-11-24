namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class MechanicTableModelServiceTests
    {
        [TestMethod]
        [TestCategory("Settings")]
        public void LeagueMechanicsHaveRequestedDefaultsAndTableEntries()
        {
            var settings = new ClickItSettings();
            var entries = MechanicTableModelService.GetTableEntries(settings);
            var beyond = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.Beyond).Subject;
            var sentinel = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.Sentinel).Subject;
            var fates = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.Fates).Subject;
            var memories = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.Memories).Subject;
            beyond.Node.Should().BeSameAs(settings.ClickBeyond);
            beyond.Node.Value.Should().BeFalse();
            beyond.DefaultEnabled.Should().BeFalse();
            sentinel.Node.Should().BeSameAs(settings.ClickSentinel);
            sentinel.Node.Value.Should().BeTrue();
            sentinel.DefaultEnabled.Should().BeTrue();
            fates.Node.Should().BeSameAs(settings.ClickFates);
            fates.Node.Value.Should().BeFalse();
            fates.DisplayName.Should().Be("Fates");
            fates.DefaultEnabled.Should().BeFalse();
            memories.Node.Should().BeSameAs(settings.ClickMemories);
            memories.Node.Value.Should().BeTrue();
            memories.DisplayName.Should().Be("Memories");
            memories.DefaultEnabled.Should().BeTrue();
        }
        [TestMethod]
        public void GetTableEntries_BuildsCachedEntryListFromSettingsNodes()
        {
            var settings = new ClickItSettings();
            settings.ClickRitualCompleted.Value = false;

            var entries = MechanicTableModelService.GetTableEntries(settings);
            var ritualCompleted = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.RitualCompleted).Subject;
            var secureRepository = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.HeistSecureRepository).Subject;
            var heistHazards = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.HeistHazards).Subject;
            var legionChest = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.LegionChest).Subject;
            var resonator = entries.Should().ContainSingle(entry => entry.Id == MechanicIds.DelveResonatorStash).Subject;
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
            legionChest.Node.Should().BeSameAs(settings.ClickLegionChest);
            legionChest.GroupId.Should().Be("league-chests");
            legionChest.Subgroup.Should().Be("Legion");
            legionChest.DefaultEnabled.Should().BeTrue();
            resonator.Node.Should().BeSameAs(settings.ClickDelveResonatorStash);
            resonator.Subgroup.Should().Be("Delve");
            resonator.DisplayName.Should().Be("Resonator");
            var delve = entries.Where(entry => entry.Subgroup == "Delve").ToArray();
            delve.Select(entry => entry.DisplayName).Should().Equal("Currency", "Resonator", "Fossil", "Maps", "Flares", "Dynamite", "Special Armour", "Special Jewellery", "Special Weapon", "Armour", "Jewellery", "Weapons");
            delve.Select(entry => entry.DefaultEnabled).Should().Equal(true, true, true, false, true, true, true, true, false, false, false, false);
            delve.Select(entry => entry.Node.Value).Should().Equal(true, true, true, false, true, true, true, true, false, false, false, false);
            entries.Where(entry => entry.Subgroup == "Delve" && entry.IsFallback).Select(entry => entry.DisplayName).Should().Equal("Armour", "Jewellery", "Weapons");
            entries.Where(entry => entry.Subgroup == "Delve" && !entry.IsFallback).Should().HaveCount(9).And.OnlyContain(entry => !string.IsNullOrWhiteSpace(entry.Tooltip));
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
