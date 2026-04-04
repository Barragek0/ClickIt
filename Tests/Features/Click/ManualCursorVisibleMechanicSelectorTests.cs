namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ManualCursorVisibleMechanicSelectorTests
    {
        [TestMethod]
        public void TryClick_ReturnsFalse_WhenNoVisibleCandidateMatchesCursor()
        {
            var selector = CreateSelector(
                resolveVisibleMechanicCandidates: () => (null, null),
                performManualCursorInteraction: static (_, _) => true);

            selector.TryClick(Vector2.Zero, Vector2.Zero).Should().BeFalse();
        }

        [TestMethod]
        public void TryClick_PrefersCloserVisibleMechanic_AndPassesHoldState()
        {
            Entity selectedEntity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            Entity fartherEntity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            bool? usedHoldClick = null;
            Entity? handledEntity = null;

            var selector = CreateSelector(
                resolveVisibleMechanicCandidates: () => (
                    new LostShipmentCandidate(fartherEntity, new Vector2(20, 0)),
                    new SettlersOreCandidate(selectedEntity, new Vector2(10, 0), MechanicIds.SettlersVerisium, "path", Vector2.Zero, Vector2.Zero)),
                performManualCursorInteraction: (_, holdClick) =>
                {
                    usedHoldClick = holdClick;
                    return true;
                },
                handleSuccessfulMechanicEntityClick: entity => handledEntity = entity);

            selector.TryClick(Vector2.Zero, Vector2.Zero).Should().BeTrue();
            usedHoldClick.Should().BeTrue();
            handledEntity.Should().BeSameAs(selectedEntity);
        }

        private static ManualCursorVisibleMechanicSelector CreateSelector(
            Func<(LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers)> resolveVisibleMechanicCandidates,
            Func<Vector2, bool, bool> performManualCursorInteraction,
            Action<Entity?>? handleSuccessfulMechanicEntityClick = null)
        {
            GameController gameController = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            ClickLabelInteractionService labelInteraction = ClickTestServiceFactory.CreateLabelInteractionService(
                gameController: gameController,
                executeInteraction: request => performManualCursorInteraction(request.ClickPosition, request.UseHoldClick));
            return new ManualCursorVisibleMechanicSelector(new ManualCursorVisibleMechanicSelectorDependencies(
                gameController,
                VisibleMechanics: new StubVisibleMechanicSelectionSource(resolveVisibleMechanicCandidates, handleSuccessfulMechanicEntityClick),
                LabelInteraction: labelInteraction));
        }

        private sealed class StubVisibleMechanicSelectionSource(
            Func<(LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers)> resolveVisibleMechanicCandidates,
            Action<Entity?>? handleSuccessfulMechanicEntityClick = null) : IVisibleMechanicManualInteractionPort
        {
            public Entity? ResolveNextShrineCandidate()
                => null;

            public bool HasClickableShrine()
                => false;

            public (LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers) GetVisibleMechanicCandidates()
                => resolveVisibleMechanicCandidates();

            public (bool HasLostShipment, bool HasSettlers) GetVisibleMechanicAvailability()
            {
                (LostShipmentCandidate? lostShipment, SettlersOreCandidate? settlers) = resolveVisibleMechanicCandidates();
                return (lostShipment.HasValue, settlers.HasValue);
            }

            public void HandleSuccessfulMechanicEntityClick(Entity? entity)
                => (handleSuccessfulMechanicEntityClick ?? (_ => { }))(entity);

            public void HandleSuccessfulShrineClick(Entity? shrine)
                => (handleSuccessfulMechanicEntityClick ?? (_ => { }))(shrine);
        }
    }
}