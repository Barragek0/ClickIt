namespace ClickIt.Tests.Behavior.Click
{
    // Scenario tests for the REAL InteractionExecutionEngine with visible mechanics on screen: a shrine partially on screen just clickable, settlers ore / lost shipment present, and how the execution chain prefers them over (or falls through to) the ranked label.
    [TestClass]
    public class ClickPipelineMechanicScenarioTests
    {
        private const string StrongboxPath = "Metadata/Chests/StrongBoxes/Arcanist";
        private const string WorldItemPath = "Metadata/MiscellaneousObjects/WorldItem";

        private static ClickPipelineScenarioFactory.ScenarioConfig BaseConfig()
            => new()
            {
                ClickDistance = 100,
                ClickItems = true,
                ClickStrongboxes = true,
            };

        private static LabelOnGround Strongbox(float distance, long address, RectangleF rect)
            => ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance, address, locked: false),
                address + 0x1000);

        private static LabelOnGround WorldItem(float distance, long address, RectangleF rect)
            => ClickPipelineScenarioFactory.CreateLabel(
                rect,
                ClickPipelineScenarioFactory.CreateWorldItem(distance, address),
                address + 0x1000);

        private static Entity ShrineEntity(float distance, long address)
            => EntityProbeFactory.Create(
                path: "Metadata/Shrines/TestShrine",
                type: EntityType.Monster,
                distancePlayer: distance,
                address: address);

        private static SettlersOreCandidate SettlersCandidate(float distance, long address)
        {
            Entity ore = EntityProbeFactory.Create(
                path: "Metadata/Leagues/Settlers/VerisiumOre",
                type: EntityType.WorldItem,
                distancePlayer: distance,
                address: address);
            return new SettlersOreCandidate(ore, new Vector2(600f, 400f), MechanicIds.SettlersVerisium, "Metadata/Leagues/Settlers/VerisiumOre", default, default);
        }

        private static LostShipmentCandidate LostShipmentCandidate(float distance, long address)
        {
            Entity shipment = EntityProbeFactory.Create(
                path: "Metadata/Leagues/Settlers/LostShipment",
                type: EntityType.Monster,
                distancePlayer: distance,
                address: address);
            return new LostShipmentCandidate(shipment, new Vector2(700f, 500f));
        }

        private static DecisionResult ShrineDecision()
            => new(TrySettlers: false, TryLostShipment: false, TryShrine: true, GroundItemsVisible: true);

        private static DecisionResult SettlersDecision()
            => new(TrySettlers: true, TryLostShipment: false, TryShrine: false, GroundItemsVisible: true);

        private static DecisionResult LostShipmentDecision()
            => new(TrySettlers: false, TryLostShipment: true, TryShrine: false, GroundItemsVisible: true);

        [TestMethod]
        public void ShrineOnScreen_Clickable_ShrineClicked_LabelNotClicked()
        {
            // A shrine on screen, just clickable, with a strongbox label present: the shrine interaction owns the tick - the label is not clicked.
            var config = BaseConfig();
            config.ShrineCandidate = ShrineEntity(distance: 10f, address: 0x51);
            config.ShrineClickable = true;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround box = Strongbox(30f, 0x01, new RectangleF(500f, 300f, 160f, 40f));
            var labels = new List<LabelOnGround> { box };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels, nextShrine: config.ShrineCandidate),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                ShrineDecision());

            result.DidActionableWork.Should().BeTrue("the shrine click is actionable");
            result.ShouldRunPostActions.Should().BeFalse("mechanic interactions do not run the label post-click aftermath");
            harness.VisibleMechanics.ShrineClicks.Should().Be(1, "the clickable shrine is clicked");
            harness.InteractionsExecuted.Should().Be(0, "the shrine click replaces the label click");
        }

        [TestMethod]
        public void ShrineOnScreen_NotClickable_LabelClicked()
        {
            // The shrine is present but its interaction fails at click time: the tick falls through to the ranked label, which is clicked.
            var config = BaseConfig();
            config.ShrineCandidate = ShrineEntity(distance: 10f, address: 0x52);
            config.ShrineClickable = false;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround box = Strongbox(30f, 0x01, new RectangleF(500f, 300f, 160f, 40f));
            var labels = new List<LabelOnGround> { box };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels, nextShrine: config.ShrineCandidate),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                ShrineDecision());

            result.ShouldRunPostActions.Should().BeTrue("when the shrine is not clickable the label is clicked");
            harness.VisibleMechanics.ShrineClicks.Should().Be(0);
            harness.InteractionsExecuted.Should().Be(1);
        }

        [TestMethod]
        public void SettlersOreOnScreen_PreferredWhenDecisionWins_SettlersClicked()
        {
            // Settlers ore on screen ranks ahead of the label: the ore interaction owns the tick.
            var config = BaseConfig();
            config.SettlersCandidate = SettlersCandidate(distance: 10f, address: 0x61);
            config.SettlersClickable = true;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround item = WorldItem(50f, 0x01, new RectangleF(500f, 300f, 60f, 20f));
            var labels = new List<LabelOnGround> { item };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(item, MechanicIds.Items, config.SettlersCandidate, null),
                SettlersDecision());

            result.DidActionableWork.Should().BeTrue();
            harness.VisibleMechanics.SettlersClicks.Should().Be(1, "the settlers ore candidate is clicked");
            harness.InteractionsExecuted.Should().Be(0, "the ore click replaces the item click");
        }

        [TestMethod]
        public void SettlersOreOnScreen_NotClickable_LabelFallsThrough()
        {
            var config = BaseConfig();
            config.SettlersCandidate = SettlersCandidate(distance: 10f, address: 0x62);
            config.SettlersClickable = false;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround item = WorldItem(50f, 0x01, new RectangleF(500f, 300f, 60f, 20f));
            var labels = new List<LabelOnGround> { item };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(item, MechanicIds.Items, config.SettlersCandidate, null),
                SettlersDecision());

            result.ShouldRunPostActions.Should().BeTrue("when the ore click fails the label is clicked");
            harness.VisibleMechanics.SettlersClicks.Should().Be(0);
            harness.InteractionsExecuted.Should().Be(1);
        }

        [TestMethod]
        public void LostShipmentOnScreen_PreferredWhenDecisionWins_LostShipmentClicked()
        {
            var config = BaseConfig();
            config.LostShipmentCandidate = LostShipmentCandidate(distance: 12f, address: 0x71);
            config.LostShipmentClickable = true;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround box = Strongbox(40f, 0x01, new RectangleF(500f, 300f, 160f, 40f));
            var labels = new List<LabelOnGround> { box };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreateContext(groundItemsVisible: true, allLabels: labels),
                harness.CreateCandidates(box, MechanicIds.Strongboxes, null, config.LostShipmentCandidate),
                LostShipmentDecision());

            result.DidActionableWork.Should().BeTrue();
            harness.VisibleMechanics.LostShipmentClicks.Should().Be(1, "the lost shipment interaction is clicked");
            harness.InteractionsExecuted.Should().Be(0);
        }

        [TestMethod]
        public void PostChestSettle_BlocksShrineClick()
        {
            // During the chest-loot settle watch, a shrine candidate is blocked even when it ranks ahead (spec 15: only within-distance interactions are allowed during the watch).
            var config = BaseConfig();
            config.ShrineCandidate = ShrineEntity(distance: 10f, address: 0x53);
            config.ShrineClickable = true;
            var harness = new ClickPipelineScenarioFactory.ScenarioHarness(config);
            LabelOnGround box = Strongbox(30f, 0x01, new RectangleF(500f, 300f, 160f, 40f));
            var labels = new List<LabelOnGround> { box };
            harness.CurrentLabels = labels;

            ExecutionResult result = harness.ExecutionEngine.Execute(
                harness.CreatePostChestSettleContext(labels),
                harness.CreateCandidates(box, MechanicIds.Strongboxes),
                ShrineDecision());

            result.ShouldRunPostActions.Should().BeFalse("clicks are paused while chest loot settles");
            harness.VisibleMechanics.ShrineClicks.Should().Be(0);
            harness.InteractionsExecuted.Should().Be(0);
        }
    }
}
