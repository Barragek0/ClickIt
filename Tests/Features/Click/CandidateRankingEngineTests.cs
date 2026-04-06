namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class CandidateRankingEngineTests
    {
        [TestMethod]
        public void BuildRank_AppliesPriorityPenalty_ToWeightedDistance()
        {
            MechanicPriorityContext context = CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.LostShipment] = 2,
            }, priorityDistancePenalty: 25);

            MechanicRank rank = CandidateRankingEngine.BuildRank(30f, MechanicIds.LostShipment, context);

            rank.PriorityIndex.Should().Be(2);
            rank.WeightedDistance.Should().Be(80f);
            rank.RawDistance.Should().Be(30f);
        }

        [TestMethod]
        public void ShouldPreferSettlersOreOverCandidates_PrefersIgnoredSettlersWithinThreshold()
        {
            MechanicPriorityContext context = CreateContext(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [MechanicIds.SettlersVerisium] = 1,
                    [MechanicIds.LostShipment] = 3,
                    [MechanicIds.Shrines] = 4,
                },
                ignoreDistanceSet: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    MechanicIds.SettlersVerisium,
                },
                ignoreDistanceWithin: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [MechanicIds.SettlersVerisium] = 20,
                });

            bool preferred = CandidateRankingEngine.ShouldPreferSettlersOreOverCandidates(
                new MechanicCandidateSignal(MechanicIds.SettlersVerisium, 10f, 8f),
                MechanicCandidateSignal.None,
                new MechanicCandidateSignal(MechanicIds.Shrines, 11f, 9f),
                new MechanicCandidateSignal(MechanicIds.LostShipment, 6f, 4f),
                context);

            preferred.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferLostShipmentOverCandidates_ReturnsFalse_WhenShrineRankIsBetter()
        {
            MechanicPriorityContext context = CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.LostShipment] = 3,
                [MechanicIds.Shrines] = 0,
            }, priorityDistancePenalty: 50);

            bool preferred = CandidateRankingEngine.ShouldPreferLostShipmentOverCandidates(
                new MechanicCandidateSignal(MechanicIds.LostShipment, 12f, 5f),
                MechanicCandidateSignal.None,
                new MechanicCandidateSignal(MechanicIds.Shrines, 10f, 4f),
                context);

            preferred.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferSettlersOreOverCandidates_ReturnsFalse_WhenLostShipmentRankIsBetter()
        {
            MechanicPriorityContext context = CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.LostShipment] = 0,
                [MechanicIds.SettlersVerisium] = 5,
                [MechanicIds.Shrines] = 8,
            }, priorityDistancePenalty: 100);

            bool preferred = CandidateRankingEngine.ShouldPreferSettlersOreOverCandidates(
                new MechanicCandidateSignal(MechanicIds.SettlersVerisium, 8f, 8f),
                MechanicCandidateSignal.None,
                new MechanicCandidateSignal(MechanicIds.Shrines, 50f, 50f),
                new MechanicCandidateSignal(MechanicIds.LostShipment, 30f, 30f),
                context);

            preferred.Should().BeFalse();
        }

        private static MechanicPriorityContext CreateContext(
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string>? ignoreDistanceSet = null,
            IReadOnlyDictionary<string, int>? ignoreDistanceWithin = null,
            int priorityDistancePenalty = 25)
        {
            return new MechanicPriorityContext(
                priorityIndexMap,
                ignoreDistanceSet ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                ignoreDistanceWithin ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                priorityDistancePenalty);
        }
    }
}