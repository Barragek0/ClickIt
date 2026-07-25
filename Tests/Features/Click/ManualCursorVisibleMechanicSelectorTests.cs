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
        public void TryClick_ClicksLostShipmentCandidate_WhenItMatchesCursor()
        {
            Entity entity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            Vector2 clickPosition = new(12f, 16f);
            Vector2? clickedPosition = null;
            bool? usedHoldClick = null;
            Entity? handledEntity = null;

            var selector = CreateSelector(
                resolveVisibleMechanicCandidates: () => (CreateLostShipmentCandidate(entity, clickPosition, distance: 15f), null),
                performManualCursorInteraction: (clickPos, useHoldClick) =>
                {
                    clickedPosition = clickPos;
                    usedHoldClick = useHoldClick;
                    return true;
                },
                handleSuccessfulMechanicEntityClick: handled => handledEntity = handled);

            bool clicked = selector.TryClick(new Vector2(10f, 10f), Vector2.Zero);

            clicked.Should().BeTrue();
            clickedPosition.Should().Be(clickPosition);
            usedHoldClick.Should().BeFalse();
            handledEntity.Should().BeSameAs(entity);
        }

        [TestMethod]
        public void TryClick_UsesHoldClick_ForVerisiumSettlersCandidate()
        {
            Entity entity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            Vector2 clickPosition = new(14f, 18f);
            bool? usedHoldClick = null;
            Entity? handledEntity = null;

            var selector = CreateSelector(
                resolveVisibleMechanicCandidates: () =>
                    (null, CreateSettlersOreCandidate(entity, clickPosition, MechanicIds.SettlersVerisium, "Metadata/Test/Verisium", distance: 22f)),
                performManualCursorInteraction: (_, useHoldClick) =>
                {
                    usedHoldClick = useHoldClick;
                    return true;
                },
                handleSuccessfulMechanicEntityClick: handled => handledEntity = handled);

            bool clicked = selector.TryClick(new Vector2(10f, 10f), Vector2.Zero);

            clicked.Should().BeTrue();
            usedHoldClick.Should().BeTrue();
            handledEntity.Should().BeSameAs(entity);
        }

        [TestMethod]
        public void TryClick_PrefersNearestVisibleMechanicCandidate()
        {
            Entity lostShipmentEntity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            Entity settlersEntity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            Vector2? clickedPosition = null;
            Entity? handledEntity = null;

            var selector = CreateSelector(
                resolveVisibleMechanicCandidates: () =>
                    (
                        CreateLostShipmentCandidate(lostShipmentEntity, new Vector2(28f, 10f), distance: 18f),
                        CreateSettlersOreCandidate(settlersEntity, new Vector2(13f, 10f), MechanicIds.SettlersCopper, "Metadata/Test/Copper", distance: 20f)),
                performManualCursorInteraction: (clickPos, _) =>
                {
                    clickedPosition = clickPos;
                    return true;
                },
                handleSuccessfulMechanicEntityClick: handled => handledEntity = handled);

            bool clicked = selector.TryClick(new Vector2(10f, 10f), Vector2.Zero);

            clicked.Should().BeTrue();
            clickedPosition.Should().Be(new Vector2(13f, 10f));
            handledEntity.Should().BeSameAs(settlersEntity);
        }

        [TestMethod]
        public void TryClick_ReturnsFalse_AndDoesNotDispatch_WhenInteractionFails()
        {
            Entity entity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            bool callbackCalled = false;

            var selector = CreateSelector(
                resolveVisibleMechanicCandidates: () => (CreateLostShipmentCandidate(entity, new Vector2(8f, 6f), distance: 12f), null),
                performManualCursorInteraction: static (_, _) => false,
                handleSuccessfulMechanicEntityClick: _ => callbackCalled = true);

            bool clicked = selector.TryClick(new Vector2(10f, 10f), Vector2.Zero);

            clicked.Should().BeFalse();
            callbackCalled.Should().BeFalse();
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

        private static LostShipmentCandidate CreateLostShipmentCandidate(Entity entity, Vector2 clickPosition, float distance)
        {
            object boxed = default(LostShipmentCandidate);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.Entity), entity);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.ClickPosition), clickPosition);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.Distance), distance);
            return (LostShipmentCandidate)boxed;
        }

        private static SettlersOreCandidate CreateSettlersOreCandidate(Entity entity, Vector2 clickPosition, string mechanicId, string entityPath, float distance)
        {
            object boxed = default(SettlersOreCandidate);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.Entity), entity);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.ClickPosition), clickPosition);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.MechanicId), mechanicId);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.EntityPath), entityPath);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.WorldScreenRaw), Vector2.Zero);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.WorldScreenAbsolute), Vector2.Zero);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.Distance), distance);
            return (SettlersOreCandidate)boxed;
        }

        private sealed class StubVisibleMechanicSelectionSource(
            Func<(LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers)> resolveVisibleMechanicCandidates,
            Action<Entity?>? handleSuccessfulMechanicEntityClick = null) : IVisibleMechanicRuntimePort
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

            public void HandleSuccessfulMechanicEntityClick(Entity? entity)
                => (handleSuccessfulMechanicEntityClick ?? (_ => { }))(entity);

            public void HandleSuccessfulShrineClick(Entity? shrine)
                => (handleSuccessfulMechanicEntityClick ?? (_ => { }))(shrine);

            public bool TryClickSettlersOre(SettlersOreCandidate candidate)
                => false;

            public bool TryClickLostShipmentInteraction(LostShipmentCandidate candidate)
                => false;

            public bool TryClickShrineInteraction(Entity shrine)
                => false;
        }
    }
}