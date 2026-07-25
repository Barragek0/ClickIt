namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class VisibleMechanicSelectionPolicyTests
    {
        [TestMethod]
        public void IsLostShipmentPath_MatchesKnownLostShipmentMarkers()
        {
            VisibleMechanicSelectionPolicy.IsLostShipmentPath("Metadata/Chests/LostShipmentCrate/Whatever").Should().BeTrue();
            VisibleMechanicSelectionPolicy.IsLostShipmentPath("Metadata/Terrain/Leagues/Settlers/LostShipment/Node").Should().BeTrue();
            VisibleMechanicSelectionPolicy.IsLostShipmentPath("Metadata/Chests/Strongbox").Should().BeFalse();
            VisibleMechanicSelectionPolicy.IsLostShipmentPath(null).Should().BeFalse();
        }

        [TestMethod]
        public void IsLostShipmentEntity_MatchesPathOrRenderNameMarkers()
        {
            VisibleMechanicSelectionPolicy.IsLostShipmentEntity("Metadata/Chests/LostShipmentCrate/Whatever", "Chest").Should().BeTrue();
            VisibleMechanicSelectionPolicy.IsLostShipmentEntity("Metadata/NotRelatedPath", "Lost Goods").Should().BeTrue();
            VisibleMechanicSelectionPolicy.IsLostShipmentEntity("Metadata/NotRelatedPath", "Lost Shipment").Should().BeTrue();
            VisibleMechanicSelectionPolicy.IsLostShipmentEntity("Metadata/NotRelatedPath", "Ordinary Chest").Should().BeFalse();
        }

        [TestMethod]
        public void IsHeistHazardsPath_MatchesHeistHazardsMarkers()
        {
            VisibleMechanicSelectionPolicy.IsHeistHazardsPath("Heist/Objects/Level/Hazards/Strength_SmashMarker").Should().BeTrue();
            VisibleMechanicSelectionPolicy.IsHeistHazardsPath("Metadata/Heist/Objects/Level/Hazards").Should().BeTrue();
            VisibleMechanicSelectionPolicy.IsHeistHazardsPath("Metadata/Heist/Objects/Level/Door_Basic").Should().BeFalse();
            VisibleMechanicSelectionPolicy.IsHeistHazardsPath(null).Should().BeFalse();
        }

        [TestMethod]
        public void SettlersSkipAndDistanceHelpers_PreserveSelectionMathContract()
        {
            VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(isValid: false, distance: 5f, clickDistance: 10).Should().BeTrue();
            VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(isValid: true, distance: 11f, clickDistance: 10).Should().BeTrue();
            VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(isValid: true, distance: 5f, clickDistance: 10).Should().BeFalse();

            VisibleMechanicSelectionPolicy.ArePlayerDistancesEquivalent(10f, 10.0005f).Should().BeTrue();
            VisibleMechanicSelectionPolicy.ArePlayerDistancesEquivalent(10f, 10.01f).Should().BeFalse();

            Vector2 cursorAbsolute = new(100f, 100f);
            Vector2 windowTopLeft = new(0f, 0f);
            Vector2 closer = new(105f, 100f);
            Vector2 farther = new(120f, 100f);
            VisibleMechanicSelectionPolicy.IsFirstCandidateCloserToCursor(closer, farther, cursorAbsolute, windowTopLeft).Should().BeTrue();
            VisibleMechanicSelectionPolicy.IsFirstCandidateCloserToCursor(farther, closer, cursorAbsolute, windowTopLeft).Should().BeFalse();
        }
    }
}