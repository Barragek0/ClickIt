namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class MovementSkillCastPointResolverTests
    {
        [TestMethod]
        public void TryResolveCastPoint_ReturnsScaledCandidate_WhenCandidateIsClickable()
        {
            RectangleF window = new(0, 0, 1000, 800);
            Vector2 targetScreen = new(700, 400);
            Vector2 expected = new(830, 400);

            bool resolved = MovementSkillCastPointResolver.TryResolveCastPoint(
                window,
                targetScreen,
                "metadata/test",
                (point, _) => point == expected,
                out Vector2 castPoint);

            resolved.Should().BeTrue();
            castPoint.Should().Be(expected);
        }

        [TestMethod]
        public void TryResolveCastPoint_FallsBackToClampedTarget_WhenScaledCandidatesAreRejected()
        {
            RectangleF window = new(0, 0, 1000, 800);
            Vector2 targetScreen = new(980, 760);
            Vector2 expected = new(880, 704);

            bool resolved = MovementSkillCastPointResolver.TryResolveCastPoint(
                window,
                targetScreen,
                "metadata/test",
                (point, _) => point == expected,
                out Vector2 castPoint);

            resolved.Should().BeTrue();
            castPoint.Should().Be(expected);
        }

        [TestMethod]
        public void TryResolveCastPoint_ReturnsFalse_ForInvalidWindowOrCenterlineTarget()
        {
            bool invalidWindowResolved = MovementSkillCastPointResolver.TryResolveCastPoint(
                new RectangleF(0, 0, 0, 0),
                new Vector2(700, 400),
                "metadata/test",
                static (_, _) => true,
                out _);

            bool centerlineResolved = MovementSkillCastPointResolver.TryResolveCastPoint(
                new RectangleF(0, 0, 1000, 800),
                new Vector2(500, 400),
                "metadata/test",
                static (_, _) => true,
                out _);

            invalidWindowResolved.Should().BeFalse();
            centerlineResolved.Should().BeFalse();
        }
    }
}