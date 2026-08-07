namespace ClickIt.Tests.Features.Pathfinding
{
    [TestClass]
    public class PathfindingOverlayTests
    {
        [DataTestMethod]
        [DataRow(30f, -30f, "NE")]
        [DataRow(30f, 30f, "SE")]
        [DataRow(-30f, -30f, "NW")]
        [DataRow(-30f, 30f, "SW")]
        [DataRow(0f, -30f, "N")]
        [DataRow(0f, 30f, "S")]
        [DataRow(30f, 0f, "E")]
        [DataRow(-30f, 0f, "W")]
        public void ToCompass_ReturnsExpectedDirection(float dx, float dy, string expected)
        {
            PathfindingOverlay.ToCompass(new Vector2(dx, dy)).Should().Be(expected);
        }

    }
}
