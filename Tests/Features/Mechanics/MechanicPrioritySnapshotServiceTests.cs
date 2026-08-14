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
            snapshot.PriorityIndexMap[MechanicIds.BlightCyst].Should().Be(snapshot.PriorityIndexMap[MechanicIds.LeagueChests]);
            snapshot.PriorityIndexMap[MechanicIds.LegionChest].Should().Be(snapshot.PriorityIndexMap[MechanicIds.LeagueChests]);
            snapshot.PriorityIndexMap[MechanicIds.BreachGraspingCoffers].Should().Be(snapshot.PriorityIndexMap[MechanicIds.LeagueChests]);
            snapshot.PriorityIndexMap[MechanicIds.AllflameCursedTreasure].Should().Be(snapshot.PriorityIndexMap[MechanicIds.LeagueChests]);
            snapshot.PriorityIndexMap[MechanicIds.AllflameBrinerotPlunder].Should().Be(snapshot.PriorityIndexMap[MechanicIds.LeagueChests]);
            snapshot.PriorityIndexMap[MechanicIds.AllflameCoralNest].Should().Be(snapshot.PriorityIndexMap[MechanicIds.LeagueChests]);
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
            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.AllflameCursedTreasure);
            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.AllflameBrinerotPlunder);
            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.AllflameCoralNest);
            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.HeistDoors);
            snapshot.IgnoreDistanceSet.Should().Contain(MechanicIds.AlvaTempleDoors);

            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.HeistHazards].Should().Be(77);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.MirageGoldenDjinnCache].Should().Be(77);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.AllflameCursedTreasure].Should().Be(77);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.AllflameBrinerotPlunder].Should().Be(77);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.AllflameCoralNest].Should().Be(77);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.HeistDoors].Should().Be(33);
            snapshot.IgnoreDistanceWithinByMechanicId[MechanicIds.AlvaTempleDoors].Should().Be(33);
        }

        [TestMethod]
        public void Refresh_CoversEverySpecificLeagueChestAndDoorId_WithGroupPriority()
        {
            var service = new MechanicPrioritySnapshotService();

            MechanicPrioritySnapshot snapshot = service.Refresh(
                MechanicPriorityCatalog.DefaultOrderIds,
                [],
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

            int leagueChestsIndex = snapshot.PriorityIndexMap[MechanicIds.LeagueChests];
            int doorsIndex = snapshot.PriorityIndexMap[MechanicIds.Doors];

            string[] specificLeagueChestIds =
            [
                MechanicIds.MirageGoldenDjinnCache,
                MechanicIds.MirageSilverDjinnCache,
                MechanicIds.MirageBronzeDjinnCache,
                MechanicIds.HeistSecureLocker,
                MechanicIds.HeistSecureRepository,
                MechanicIds.HeistHazards,
                MechanicIds.BlightCyst,
                MechanicIds.LegionChest,
                MechanicIds.BreachGraspingCoffers,
                MechanicIds.SynthesisSynthesisedStash,
                MechanicIds.AllflameCursedTreasure,
                MechanicIds.AllflameBrinerotPlunder,
                MechanicIds.AllflameCoralNest
            ];

            foreach (string specificId in specificLeagueChestIds)
                snapshot.PriorityIndexMap[specificId].Should().Be(leagueChestsIndex, $"{specificId} must inherit the League Chests priority");

            snapshot.PriorityIndexMap[MechanicIds.HeistDoors].Should().Be(doorsIndex);
            snapshot.PriorityIndexMap[MechanicIds.AlvaTempleDoors].Should().Be(doorsIndex);
        }
    }
}