namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenProjectionMathTests
    {
        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_ReturnsFalse_WhenWindowIsInvalid()
        {
            bool result = OffscreenProjectionMath.TryResolveDirectionalWalkClickPosition(
                new RectangleF(10f, 20f, 0f, 100f),
                new Vector2(200f, 50f),
                "Metadata/TestTarget",
                static (_, _) => true,
                out Vector2 clickPos);

            result.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_ReturnsFalse_WhenTargetIsAtWindowCenter()
        {
            RectangleF windowRect = new RectangleF(10f, 20f, 100f, 80f);
            Vector2 center = new Vector2(windowRect.X + (windowRect.Width * 0.5f), windowRect.Y + (windowRect.Height * 0.5f));

            bool result = OffscreenProjectionMath.TryResolveDirectionalWalkClickPosition(
                windowRect,
                center,
                "Metadata/TestTarget",
                static (_, _) => true,
                out Vector2 clickPos);

            result.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_ReturnsFirstSafeClickableCandidate()
        {
            RectangleF windowRect = new RectangleF(0f, 0f, 100f, 100f);
            List<(Vector2 Point, string Path)> calls = [];

            bool result = OffscreenProjectionMath.TryResolveDirectionalWalkClickPosition(
                windowRect,
                new Vector2(80f, 50f),
                "Metadata/TestTarget",
                (point, path) =>
                {
                    calls.Add((point, path));
                    return true;
                },
                out Vector2 clickPos);

            result.Should().BeTrue();
            clickPos.X.Should().BeApproximately(69.5f, 0.01f);
            clickPos.Y.Should().BeApproximately(50f, 0.01f);
            calls.Should().ContainSingle();
            calls[0].Path.Should().Be("Metadata/TestTarget");
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_FallsBackToClampedPoint_WhenLoopCandidatesAreRejected()
        {
            RectangleF windowRect = new RectangleF(0f, 0f, 100f, 100f);
            Vector2 expectedClamped = new Vector2(72f, 50f);

            bool result = OffscreenProjectionMath.TryResolveDirectionalWalkClickPosition(
                windowRect,
                new Vector2(150f, 50f),
                "Metadata/TestTarget",
                (point, _) => Math.Abs(point.X - expectedClamped.X) < 0.01f && Math.Abs(point.Y - expectedClamped.Y) < 0.01f,
                out Vector2 clickPos);

            result.Should().BeTrue();
            clickPos.Should().Be(expectedClamped);
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_ReturnsFalse_WhenNoCandidateOrClampedPointIsClickable()
        {
            bool result = OffscreenProjectionMath.TryResolveDirectionalWalkClickPosition(
                new RectangleF(0f, 0f, 100f, 100f),
                new Vector2(150f, 50f),
                "Metadata/TestTarget",
                static (_, _) => false,
                out Vector2 clickPos);

            result.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
        }
    }
}