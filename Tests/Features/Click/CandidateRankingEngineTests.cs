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
        public void BuildRank_UsesMaxDistance_WhenCandidateSignalHasNoDistanceOrCursor()
        {
            MechanicPriorityContext context = CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.Shrines] = 1,
            });

            MechanicRank rank = CandidateRankingEngine.BuildRank(float.MaxValue, null, context);

            rank.PriorityIndex.Should().Be(int.MaxValue);
            rank.WeightedDistance.Should().Be(float.MaxValue);
            rank.RawDistance.Should().Be(float.MaxValue);
            rank.CursorDistance.Should().Be(float.MaxValue);
        }

        [TestMethod]
        public void CompareRanks_PrefersIgnoredRank_WhenPriorityIsNotWorse()
        {
            MechanicRank ignored = new(ignored: true, priorityIndex: 1, weightedDistance: 999f, rawDistance: 50f, cursorDistance: 12f);
            MechanicRank nonIgnored = new(ignored: false, priorityIndex: 1, weightedDistance: 10f, rawDistance: 10f, cursorDistance: 4f);

            int comparison = CandidateRankingEngine.CompareRanks(ignored, nonIgnored);

            comparison.Should().BeLessThan(0);
        }

        [TestMethod]
        public void CompareRanks_UsesCursorDistanceAfterWeightedAndRawDistanceTie()
        {
            MechanicRank left = new(ignored: false, priorityIndex: 1, weightedDistance: 20f, rawDistance: 8f, cursorDistance: 5f);
            MechanicRank right = new(ignored: false, priorityIndex: 1, weightedDistance: 20f, rawDistance: 8f, cursorDistance: 9f);

            int comparison = CandidateRankingEngine.CompareRanks(left, right);

            comparison.Should().BeLessThan(0);
        }

        [TestMethod]
        public void Rank_ReturnsHiddenMechanicPreferences_WhenGroundItemsAreHidden()
        {
            MechanicPriorityContext context = CreateContext(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [MechanicIds.SettlersVerisium] = 0,
                    [MechanicIds.LostShipment] = 3,
                },
                ignoreDistanceSet: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    MechanicIds.SettlersVerisium,
                },
                ignoreDistanceWithin: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [MechanicIds.SettlersVerisium] = 20,
                });

            CandidateRankingEngine engine = CreateEngine();
            ClickCandidates candidates = new(
                LostShipment: CreateLostShipmentCandidate(new Vector2(15f, 18f), distance: 25f),
                SettlersOre: CreateSettlersCandidate(new Vector2(10f, 10f), MechanicIds.SettlersVerisium, distance: 10f),
                NextLabel: null,
                NextLabelMechanicId: null);

            RankingResult result = engine.Rank(new ClickTickContext(
                WindowTopLeft: Vector2.Zero,
                CursorAbsolute: new Vector2(5f, 5f),
                Now: 100,
                IsPostChestLootSettleBlocking: false,
                ChestLootSettleReason: string.Empty,
                AllLabels: null,
                NextShrine: null,
                MechanicPriorityContext: context,
                GroundItemsVisible: false),
                candidates);

            result.PreferSettlers.Should().BeTrue();
            result.PreferLostShipment.Should().BeTrue();
            result.PreferShrine.Should().BeFalse();
            result.GroundItemsVisible.Should().BeFalse();
        }

        [TestMethod]
        public void Rank_ReturnsVisibleLostShipmentPreference_WhenVisibleCandidatesBeatLabelAndShrine()
        {
            MechanicPriorityContext context = CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.LostShipment] = 0,
            });

            CandidateRankingEngine engine = CreateEngine();
            ClickCandidates candidates = new(
                LostShipment: CreateLostShipmentCandidate(new Vector2(11f, 11f), distance: 12f),
                SettlersOre: null,
                NextLabel: null,
                NextLabelMechanicId: null);

            RankingResult result = engine.Rank(new ClickTickContext(
                WindowTopLeft: Vector2.Zero,
                CursorAbsolute: new Vector2(5f, 5f),
                Now: 100,
                IsPostChestLootSettleBlocking: false,
                ChestLootSettleReason: string.Empty,
                AllLabels: [],
                NextShrine: null,
                MechanicPriorityContext: context,
                GroundItemsVisible: true),
                candidates);

            result.PreferSettlers.Should().BeFalse();
            result.PreferLostShipment.Should().BeTrue();
            result.PreferShrine.Should().BeFalse();
            result.GroundItemsVisible.Should().BeTrue();
        }

        [TestMethod]
        public void Rank_ReturnsFalsePreferences_WhenNoCandidatesExist()
        {
            CandidateRankingEngine engine = CreateEngine();

            RankingResult result = engine.Rank(new ClickTickContext(
                WindowTopLeft: Vector2.Zero,
                CursorAbsolute: Vector2.Zero,
                Now: 100,
                IsPostChestLootSettleBlocking: false,
                ChestLootSettleReason: string.Empty,
                AllLabels: [],
                NextShrine: null,
                MechanicPriorityContext: CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)),
                GroundItemsVisible: true),
                new ClickCandidates(null, null, null, null));

            result.PreferSettlers.Should().BeFalse();
            result.PreferLostShipment.Should().BeFalse();
            result.PreferShrine.Should().BeFalse();
            result.GroundItemsVisible.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferLostShipmentOverCandidates_ReturnsTrue_WhenOtherCandidatesAreMissing()
        {
            MechanicPriorityContext context = CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.LostShipment] = 0,
            });

            bool preferred = CandidateRankingEngine.ShouldPreferLostShipmentOverCandidates(
                new MechanicCandidateSignal(MechanicIds.LostShipment, 12f, 4f),
                MechanicCandidateSignal.None,
                MechanicCandidateSignal.None,
                context);

            preferred.Should().BeTrue();
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

        [TestMethod]
        public void ShouldPreferShrineOverLabel_ReturnsTrue_WhenShrineRankBeatsLabel()
        {
            MechanicPriorityContext context = CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.Shrines] = 0,
                [MechanicIds.Items] = 3,
            }, priorityDistancePenalty: 40);

            bool preferred = CandidateRankingEngine.ShouldPreferShrineOverLabel(
                new MechanicCandidateSignal(MechanicIds.Shrines, 10f, 5f),
                new MechanicCandidateSignal(MechanicIds.Items, 10f, 5f),
                context);

            preferred.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferShrineOverLabel_ReturnsFalse_WhenShrineSignalDoesNotExist()
        {
            MechanicPriorityContext context = CreateContext(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

            bool preferred = CandidateRankingEngine.ShouldPreferShrineOverLabel(
                MechanicCandidateSignal.None,
                new MechanicCandidateSignal(MechanicIds.Items, 5f, 2f),
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

        private static CandidateRankingEngine CreateEngine()
        {
            var settings = new ClickItSettings();
            ILabelInteractionPort port = new FakeLabelInteractionPort();

            GameController gameController = ExileCoreOpaqueFactory.CreateOpaqueGameController();
            var scanEngine = new LabelSelectionScanEngine(new LabelSelectionScanEngineDependencies(
                gameController,
                port,
                new LabelClickPointResolver(settings),
                ShouldSuppressLeverClick: static _ => false,
                ShouldSuppressInactiveUltimatumLabel: static _ => false,
                ClickTestServiceFactory.CreateLabelInteractionService(gameController: gameController, labelInteractionPort: port),
                new MechanicPriorityContextProvider(settings, new MechanicPrioritySnapshotService()),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
                DebugLog: static _ => { }));

            var coordinator = new LabelSelectionCoordinator(new LabelSelectionCoordinatorDependencies(
                GameController: gameController,
                ScanEngine: scanEngine,
                ManualCursorLabelSelector: null!,
                ManualCursorVisibleMechanicSelector: null!,
                SpecialLabelInteractionHandler: null!,
                ManualCursorLabelInteractionHandler: null!));

            return new CandidateRankingEngine(new CandidateRankingEngineDependencies(
                LabelSelection: coordinator,
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(gameController: gameController, labelInteractionPort: port)));
        }

        private static LostShipmentCandidate CreateLostShipmentCandidate(Vector2 clickPosition, float distance)
        {
            object boxed = default(LostShipmentCandidate);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.Entity), ExileCoreOpaqueFactory.CreateOpaqueEntity());
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.ClickPosition), clickPosition);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.Distance), distance);
            return (LostShipmentCandidate)boxed;
        }

        private static SettlersOreCandidate CreateSettlersCandidate(Vector2 clickPosition, string mechanicId, float distance)
        {
            object boxed = default(SettlersOreCandidate);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.Entity), ExileCoreOpaqueFactory.CreateOpaqueEntity());
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.ClickPosition), clickPosition);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.MechanicId), mechanicId);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.EntityPath), "Metadata/Test");
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.WorldScreenRaw), Vector2.Zero);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.WorldScreenAbsolute), Vector2.Zero);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.Distance), distance);
            return (SettlersOreCandidate)boxed;
        }

        private sealed class FakeLabelInteractionPort : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => default;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => null;

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => null;

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }
    }
}