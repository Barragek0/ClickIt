namespace ClickIt.Tests.Shared.Game
{
    // The shared EntityEventHub is a static singleton, so this class resets it and must not run in
    // parallel with any other hub consumer.
    [TestClass]
    [DoNotParallelize]
    public class EntityEventHubTests
    {
        // Reset the static singleton so each test starts with empty categories and no subscription.
        [TestInitialize]
        public void ResetHub()
            => EntityEventHub.Instance.Dispose();

        [TestMethod]
        public void Reseed_ClassifiesBlightPathway_IntoBlightOnly()
        {
            Entity pathway = EntityProbeFactory.Create(path: "Metadata/Terrain/Leagues/Blight/Objects/BlightPathway");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(pathway);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            EntityEventHub.Instance.Blight.Count.Should().Be(1);
            EntityEventHub.Instance.OffscreenStructures.Count.Should().Be(0);
            EntityEventHub.Instance.SettlersOre.Count.Should().Be(0);
            EntityEventHub.Instance.Shrines.Count.Should().Be(0);
            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(0);
        }

        [TestMethod]
        public void Reseed_ClassifiesAltar_IntoOffscreenStructuresOnly()
        {
            Entity altar = EntityProbeFactory.Create(path: "Metadata/Terrain/CleansingFireAltar");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(altar);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            EntityEventHub.Instance.OffscreenStructures.Count.Should().Be(1);
            EntityEventHub.Instance.Blight.Count.Should().Be(0);
            EntityEventHub.Instance.Shrines.Count.Should().Be(0);
            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(0);
        }

        [TestMethod]
        public void Reseed_ClassifiesDarkShrine_IntoShrinesAndOffscreenStructures()
        {
            Entity shrine = EntityProbeFactory.Create(path: "Metadata/MiscellaneousObjects/DarkShrine");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(shrine);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            EntityEventHub.Instance.Shrines.Count.Should().Be(1);
            EntityEventHub.Instance.OffscreenStructures.Count.Should().Be(1);
            EntityEventHub.Instance.Blight.Count.Should().Be(0);
            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(0);
        }

        [TestMethod]
        public void Reseed_ClassifiesShroudedShrine_IntoShrinesAndOffscreenStructures()
        {
            // Current-league shrines (Shrouded Shrine) carry the Shrine component but their path has
            // no "DarkShrine" marker; the hub must retain them as shrines and offscreen structures.
            Entity shrine = EntityProbeFactory.Create(path: "Metadata/MiscellaneousObjects/ShroudedShrine");
            EntityProbeFactory.WithComponent<Shrine>(shrine, (Shrine)RuntimeHelpers.GetUninitializedObject(typeof(Shrine)));
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(shrine);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            EntityEventHub.Instance.Shrines.Count.Should().Be(1);
            EntityEventHub.Instance.OffscreenStructures.Count.Should().Be(1);
            EntityEventHub.Instance.Blight.Count.Should().Be(0);
            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(0);
        }

        [TestMethod]
        public void Reseed_ShrineHintedPath_WithoutShrineComponent_IsNotClassified()
        {
            // A monster whose path merely mentions "Shrine" (e.g. Shrine Daemon) has no Shrine
            // component and must not be retained as a shrine or offscreen structure.
            Entity daemon = EntityProbeFactory.Create(path: "Metadata/Monsters/ShrineDaemon");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(daemon);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            EntityEventHub.Instance.Shrines.Count.Should().Be(0);
            EntityEventHub.Instance.OffscreenStructures.Count.Should().Be(0);
            EntityEventHub.Instance.Blight.Count.Should().Be(0);
            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(0);
        }

        [TestMethod]
        public void Reseed_ClassifiesRitualBlocker_IntoRitualBlockers()
        {
            Entity blocker = EntityProbeFactory.Create(path: "Metadata/MiscellaneousObjects/RitualBlocker");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(blocker);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(1);
            EntityEventHub.Instance.Blight.Count.Should().Be(0);
            EntityEventHub.Instance.Shrines.Count.Should().Be(0);
            EntityEventHub.Instance.OffscreenStructures.Count.Should().Be(0);
        }

        [TestMethod]
        public void Reseed_ClassifiesSettlersOre_IntoSettlersOre()
        {
            Entity ore = EntityProbeFactory.Create(path: "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(ore);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            EntityEventHub.Instance.SettlersOre.Count.Should().Be(1);
            EntityEventHub.Instance.Blight.Count.Should().Be(0);
            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(0);
        }

        [TestMethod]
        public void Reseed_IgnoresUnrelatedEntities()
        {
            Entity monster = EntityProbeFactory.Create(path: "Metadata/Monsters/Test");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(monster);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            EntityEventHub.Instance.Blight.Count.Should().Be(0);
            EntityEventHub.Instance.OffscreenStructures.Count.Should().Be(0);
            EntityEventHub.Instance.SettlersOre.Count.Should().Be(0);
            EntityEventHub.Instance.Shrines.Count.Should().Be(0);
            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(0);
        }

        [TestMethod]
        public void Reseed_AccumulatesCost_TakePendingCostReturnsAndResets()
        {
            Entity pathway = EntityProbeFactory.Create(path: "Metadata/Terrain/Leagues/Blight/Objects/BlightPathway");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(pathway);

            EntityEventHub.Instance.EnsureSubscribed(gc);

            (long Bytes, double Ms) pending = EntityEventHub.Instance.TakePendingCost();
            pending.Bytes.Should().BeGreaterThanOrEqualTo(0, "the reseed's entity-path work is accumulated");
            pending.Ms.Should().BeGreaterThanOrEqualTo(0);

            EntityEventHub.Instance.TakePendingCost().Should().Be((0L, 0d), "a second poll returns the reset accumulator");
        }

        [TestMethod]
        public void Dispose_ClearsAllCategories()
        {
            Entity shrine = EntityProbeFactory.Create(path: "Metadata/MiscellaneousObjects/DarkShrine");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(shrine);

            EntityEventHub.Instance.EnsureSubscribed(gc);
            EntityEventHub.Instance.Shrines.Count.Should().Be(1);

            EntityEventHub.Instance.Dispose();

            EntityEventHub.Instance.Shrines.Count.Should().Be(0);
            EntityEventHub.Instance.OffscreenStructures.Count.Should().Be(0);
            EntityEventHub.Instance.Blight.Count.Should().Be(0);
            EntityEventHub.Instance.RitualBlockers.Count.Should().Be(0);
            EntityEventHub.Instance.SettlersOre.Count.Should().Be(0);
        }
    }
}
