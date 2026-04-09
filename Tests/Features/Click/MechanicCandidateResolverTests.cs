namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class MechanicCandidateResolverTests
    {
        [TestMethod]
        public void ShouldPromoteByDistanceAndCursor_ReturnsTrue_WhenCandidateIsCloser()
        {
            bool promoted = MechanicCandidateResolver.ShouldPromoteByDistanceAndCursor(
                candidateDistance: 10f,
                bestDistance: 15f,
                candidateClickPosition: new Vector2(25f, 25f),
                bestClickPosition: new Vector2(40f, 40f),
                cursorAbsolute: new Vector2(10f, 10f),
                windowTopLeft: Vector2.Zero);

            promoted.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPromoteByDistanceAndCursor_UsesCursorTieBreak_ForEquivalentDistances()
        {
            bool promoted = MechanicCandidateResolver.ShouldPromoteByDistanceAndCursor(
                candidateDistance: 10f,
                bestDistance: 10.0005f,
                candidateClickPosition: new Vector2(12f, 12f),
                bestClickPosition: new Vector2(30f, 30f),
                cursorAbsolute: new Vector2(10f, 10f),
                windowTopLeft: Vector2.Zero);

            promoted.Should().BeTrue();
        }

        [TestMethod]
        public void TryPromoteLostShipmentCandidate_SetsBest_WhenBestIsEmpty()
        {
            LostShipmentCandidate? best = null;
            LostShipmentCandidate candidate = CreateLostShipmentCandidate(new Vector2(20f, 20f), distance: 14f);

            bool promoted = MechanicCandidateResolver.TryPromoteLostShipmentCandidate(
                ref best,
                candidate,
                cursorAbsolute: new Vector2(5f, 5f),
                windowTopLeft: Vector2.Zero);

            promoted.Should().BeTrue();
            best.Should().NotBeNull();
            best!.Value.ClickPosition.Should().Be(new Vector2(20f, 20f));
            best.Value.Distance.Should().Be(14f);
        }

        [TestMethod]
        public void TryPromoteLostShipmentCandidate_ReplacesBest_WhenDistancesAreEquivalentAndCursorIsCloser()
        {
            LostShipmentCandidate? best = CreateLostShipmentCandidate(new Vector2(30f, 30f), distance: 10f);
            LostShipmentCandidate candidate = CreateLostShipmentCandidate(new Vector2(12f, 12f), distance: 10.0004f);

            bool promoted = MechanicCandidateResolver.TryPromoteLostShipmentCandidate(
                ref best,
                candidate,
                cursorAbsolute: new Vector2(10f, 10f),
                windowTopLeft: Vector2.Zero);

            promoted.Should().BeTrue();
            best.Should().NotBeNull();
            best!.Value.ClickPosition.Should().Be(new Vector2(12f, 12f));
        }

        [TestMethod]
        public void TryPromoteSettlersCandidate_KeepsExistingBest_WhenCandidateIsWorse()
        {
            SettlersOreCandidate? best = CreateSettlersCandidate(new Vector2(14f, 14f), MechanicIds.SettlersCopper, distance: 8f);
            SettlersOreCandidate candidate = CreateSettlersCandidate(new Vector2(50f, 50f), MechanicIds.SettlersVerisium, distance: 12f);

            bool promoted = MechanicCandidateResolver.TryPromoteSettlersCandidate(
                ref best,
                candidate,
                cursorAbsolute: new Vector2(10f, 10f),
                windowTopLeft: Vector2.Zero);

            promoted.Should().BeFalse();
            best.Should().NotBeNull();
            best!.Value.MechanicId.Should().Be(MechanicIds.SettlersCopper);
            best.Value.ClickPosition.Should().Be(new Vector2(14f, 14f));
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
    }
}