namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OnscreenMechanicPathingBlockerTests
    {
        [TestMethod]
        public void ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable_PublishesStage_WhenVisibleMechanicBlocks()
        {
            var settings = new ClickItSettings
            {
                PrioritizeOnscreenClickableMechanicsOverPathfinding = new ToggleNode(true),
                ClickShrines = new ToggleNode(true),
                ClickLostShipmentCrates = new ToggleNode(true),
                ClickSettlersOre = new ToggleNode(true),
                ClickEaterAltars = new ToggleNode(false),
                ClickExarchAltars = new ToggleNode(false)
            };

            string? stage = null;
            string? notes = null;
            var blocker = new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                settings,
                ClickTestServiceFactory.CreateAltarAutomationService(settings),
                new StubVisibleMechanicSelectionSource(hasClickableShrine: false, hasLostShipment: true, hasSettlers: false),
                ClickTestDebugPublisherFactory.Create(
                    () => true,
                    snapshot =>
                    {
                        stage = snapshot.Stage;
                        notes = snapshot.Notes;
                    })));

            bool blocked = blocker.ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable();

            blocked.Should().BeTrue();
            stage.Should().Be("OffscreenPathingBlocked");
            notes.Should().Contain("lost=True").And.Contain("settlers=False");
        }

        [TestMethod]
        public void ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable_ReturnsFalse_WhenPriorityDisabled()
        {
            var settings = new ClickItSettings
            {
                PrioritizeOnscreenClickableMechanicsOverPathfinding = new ToggleNode(false),
                ClickShrines = new ToggleNode(true),
                ClickLostShipmentCrates = new ToggleNode(true),
                ClickSettlersOre = new ToggleNode(true),
                ClickEaterAltars = new ToggleNode(true),
                ClickExarchAltars = new ToggleNode(true)
            };

            bool published = false;
            var blocker = new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                settings,
                ClickTestServiceFactory.CreateAltarAutomationService(settings),
                new StubVisibleMechanicSelectionSource(hasClickableShrine: true, hasLostShipment: true, hasSettlers: true),
                ClickTestDebugPublisherFactory.Create(() => true, _ => published = true)));

            bool blocked = blocker.ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable();

            blocked.Should().BeFalse();
            published.Should().BeFalse();
        }
        private sealed class StubVisibleMechanicSelectionSource(bool hasClickableShrine, bool hasLostShipment, bool hasSettlers) : IVisibleMechanicQueryPort
        {
            public Entity? ResolveNextShrineCandidate()
                => null;

            public bool HasClickableShrine()
                => hasClickableShrine;

            public void ResolveVisibleMechanicCandidates(
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate,
                IReadOnlyList<LabelOnGround>? labelsOverride = null)
            {
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

            public VisibleMechanicAvailabilitySnapshot GetVisibleMechanicAvailabilitySnapshot()
                => new(hasLostShipment, hasSettlers);
        }
    }
}