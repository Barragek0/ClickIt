using ClickIt.Features.Click.Selection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class MechanicCandidateResolverTests
    {
        [TestMethod]
        public void ShouldPromoteByDistanceAndCursor_ReturnsTrue_WhenDistanceIsCloser()
        {
            bool promoted = MechanicCandidateResolver.ShouldPromoteByDistanceAndCursor(
                candidateDistance: 10f,
                bestDistance: 12f,
                candidateClickPosition: new Vector2(200, 200),
                bestClickPosition: new Vector2(300, 300),
                cursorAbsolute: new Vector2(100, 100),
                windowTopLeft: Vector2.Zero);

            promoted.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPromoteByDistanceAndCursor_UsesCursorTieBreak_WhenDistancesEquivalent()
        {
            bool promoted = MechanicCandidateResolver.ShouldPromoteByDistanceAndCursor(
                candidateDistance: 10f,
                bestDistance: 10.05f,
                candidateClickPosition: new Vector2(120, 120),
                bestClickPosition: new Vector2(220, 220),
                cursorAbsolute: new Vector2(100, 100),
                windowTopLeft: Vector2.Zero);

            promoted.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPromoteByDistanceAndCursor_ReturnsFalse_WhenFurtherAndNoTieBreak()
        {
            bool promoted = MechanicCandidateResolver.ShouldPromoteByDistanceAndCursor(
                candidateDistance: 16f,
                bestDistance: 10f,
                candidateClickPosition: new Vector2(300, 300),
                bestClickPosition: new Vector2(150, 150),
                cursorAbsolute: new Vector2(100, 100),
                windowTopLeft: Vector2.Zero);

            promoted.Should().BeFalse();
        }
    }
}