namespace ClickIt.Tests.Features.Mechanics
{
    [TestClass]
    public class MechanicPrioritySnapshotServiceTests
    {
        [TestMethod]
        public void Refresh_MapsGroupPriorityIndexToSpecificMechanicIds()
        {
            var service = new MechanicPrioritySnapshotService();

            MechanicPrioritySnapshot snapshot = service.Refresh(
                new[] { MechanicIds.LeagueChests, MechanicIds.Doors, MechanicIds.Items },
                [],
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

            snapshot.PriorityIndexMap[MechanicIds.HeistHazards].Should().Be(snapshot.PriorityIndexMap[MechanicIds.LeagueChests]);
            snapshot.PriorityIndexMap[MechanicIds.BreachGraspingCoffers].Should().Be(snapshot.PriorityIndexMap[MechanicIds.LeagueChests]);
            snapshot.PriorityIndexMap[MechanicIds.HeistDoors].Should().Be(snapshot.PriorityIndexMap[MechanicIds.Doors]);
            snapshot.PriorityIndexMap[MechanicIds.AlvaTempleDoors].Should().Be(snapshot.PriorityIndexMap[MechanicIds.Doors]);
        }

        [TestMethod]
        public void Refresh_MapsGroupIgnoreDistanceSettingsToSpecificMechanicIds()
        {
            var service = new MechanicPrioritySnapshotService();
            var ignoreDistanceWithin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.LeagueChests] = 77,
                [MechanicIds.Doors] = 33
            };

            MechanicPrioritySnapshot snapshot = service.Refresh(
                new[] { MechanicIds.LeagueChests, MechanicIds.Doors },
                new HashSet<string>([MechanicIds.LeagueChests, MechanicIds.Doors], StringComparer.OrdinalIgnoreCase),
                ignoreDistanceWithin);

            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.HeistHazards);
            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.SynthesisSynthesisedStash);
            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.HeistDoors);
            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.AlvaTempleDoors);

            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.HeistHazards].Should().Be(77);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.MirageGoldenDjinnCache].Should().Be(77);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.HeistDoors].Should().Be(33);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.AlvaTempleDoors].Should().Be(33);
        }
    }
}