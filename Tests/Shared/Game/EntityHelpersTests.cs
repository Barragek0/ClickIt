namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class EntityHelpersTests
    {
        [TestMethod]
        public void IsRitualActive_IsCaseSensitive()
        {
            var lowerOnly = new System.Collections.Generic.List<string?> { "ritualblocker" };
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)lowerOnly).Should().BeFalse();

            var proper = new System.Collections.Generic.List<string?> { "Some/Prefix/RitualBlocker/Node" };
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)proper).Should().BeTrue();
        }

        [TestMethod]
        public void IsRitualActive_EventSet_DetectsBlockersSeededFromController()
        {
            Entity blocker = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(
                address: 700,
                path: "Metadata/Terrain/Ritual/RitualBlocker");
            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(blocker);

            EntityHelpers.IsRitualActive(gc).Should().BeTrue(
                "the event set seeds from the controller's entity list on first use");
        }

        [TestMethod]
        public void IsRitualActive_EventSet_RebindsWhenControllerChanges()
        {
            Entity blocker = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(
                address: 700,
                path: "Metadata/Terrain/Ritual/RitualBlocker");
            GameController gcWithBlocker = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(blocker);
            GameController emptyGc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities();

            // A different controller instance must re-seed, not reuse the previous controller's set.
            EntityHelpers.IsRitualActive(gcWithBlocker).Should().BeTrue();
            EntityHelpers.IsRitualActive(emptyGc).Should().BeFalse(
                "a different controller rebinds and re-seeds from its own entity list");
        }
    }
}
