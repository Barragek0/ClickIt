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

            public void ResolveVisibleMechanicCandidates(
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate,
                IReadOnlyList<LabelOnGround>? labelsOverride = null)
            {
                (LostShipmentCandidate? lostShipment, SettlersOreCandidate? settlers) = resolveVisibleMechanicCandidates();
                lostShipmentCandidate = lostShipment;
                settlersOreCandidate = settlers;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

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

            public bool TryClickSettlersOre(SettlersOreCandidate candidate)
                => false;

            public void TryClickLostShipment(LostShipmentCandidate candidate)
            {
            }

            public void TryClickShrine(Entity shrine)
            {
            }
        }
    }
}