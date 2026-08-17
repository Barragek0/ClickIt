namespace ClickIt.Tests.Shared.Math
{
    [TestClass]
    public class CoordinateSpaceTests
    {
        [TestMethod]
        public void ToClient_SubtractsWindowTopLeft()
        {
            Vector2 absolute = new(150f, 75f);
            Vector2 topLeft = new(100f, 50f);

            Vector2 client = CoordinateSpace.ToClient(absolute, topLeft);

            client.X.Should().Be(50f);
            client.Y.Should().Be(25f);
        }

        [TestMethod]
        public void DistanceSquared_ComputesSquaredEuclideanDistance()
        {
            Vector2 a = new(1f, 2f);
            Vector2 b = new(4f, 6f);

            float distanceSq = CoordinateSpace.DistanceSquared(a, b);

            // dx=3, dy=4 -> 3*3 + 4*4 = 25
            distanceSq.Should().Be(25f);
        }

        [TestMethod]
        public void DistanceSquared_ReturnsZero_ForIdenticalPoints()
        {
            Vector2 point = new(10f, -3f);

            CoordinateSpace.DistanceSquared(point, point).Should().Be(0f);
        }

        [TestMethod]
        public void DistanceSquaredInEitherSpace_ReturnsSmallerOfAbsoluteAndClientSpaces()
        {
            Vector2 cursorAbsolute = new(200f, 100f);
            Vector2 candidate = new(110f, 60f);
            Vector2 windowTopLeft = new(100f, 50f);

            // Absolute space: dx=90, dy=40 -> 8100 + 1600 = 9700
            // Client space: cursor client = (100, 50), dx=10, dy=10 -> 100 + 100 = 200
            float distanceSq = CoordinateSpace.DistanceSquaredInEitherSpace(cursorAbsolute, candidate, windowTopLeft);

            distanceSq.Should().Be(200f);
        }
    }
}
