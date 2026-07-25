namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ManualCursorSelectionMathTests
    {
        [TestMethod]
        public void IsPointInsideRectInEitherSpace_MatchesAbsoluteOrClientCoordinates()
        {
            RectangleF rect = new RectangleF(50, 50, 100, 100);
            Vector2 windowTopLeft = new Vector2(10, 10);

            ManualCursorSelectionMath.IsPointInsideRectInEitherSpace(rect, new Vector2(60, 60), windowTopLeft).Should().BeTrue();
            ManualCursorSelectionMath.IsPointInsideRectInEitherSpace(rect, new Vector2(70, 70) + windowTopLeft, windowTopLeft).Should().BeTrue();
            ManualCursorSelectionMath.IsPointInsideRectInEitherSpace(rect, new Vector2(10, 10), windowTopLeft).Should().BeFalse();
        }

        [TestMethod]
        public void IsWithinManualCursorMatchDistanceInEitherSpace_UsesSquaredDistanceThreshold()
        {
            Vector2 cursor = new Vector2(100, 100);
            Vector2 candidate = new Vector2(120, 100);

            ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(cursor, candidate, Vector2.Zero, 21f).Should().BeTrue();
            ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(cursor, candidate, Vector2.Zero, 19f).Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        [DataRow(true, true, true)]
        public void ShouldAttemptManualCursorAltarClick_ReturnsExpected(bool isAltarLabel, bool hasClickableAltars, bool expected)
        {
            ManualCursorSelectionMath.ShouldAttemptManualCursorAltarClick(isAltarLabel, hasClickableAltars).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, false, true)]
        [DataRow(true, true, false)]
        public void ShouldUseManualGroundProjectionForCandidate_ReturnsExpected(bool hasBackingEntity, bool isWorldItem, bool expected)
        {
            ManualCursorSelectionMath.ShouldUseManualGroundProjectionForCandidate(hasBackingEntity, isWorldItem).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        public void ShouldTreatManualCursorAsHoveringCandidate_ReturnsExpected(bool cursorInsideLabelRect, bool cursorNearGroundProjection, bool expected)
        {
            ManualCursorSelectionMath.ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect, cursorNearGroundProjection).Should().Be(expected);
        }

        [TestMethod]
        public void IsWithinManualCursorMatchDistanceInEitherSpace_ReturnsFalse_WhenMaxDistanceIsNotPositive()
        {
            Vector2 cursor = new Vector2(100, 100);
            Vector2 candidate = new Vector2(100, 100);

            ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(cursor, candidate, Vector2.Zero, 0f).Should().BeFalse();
            ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(cursor, candidate, Vector2.Zero, -1f).Should().BeFalse();
        }

        [TestMethod]
        public void GetCursorDistanceSquaredToPoint_UsesAbsoluteOrClientCoordinates()
        {
            Vector2 windowTopLeft = new Vector2(50f, 25f);
            Vector2 point = new Vector2(20f, 30f);
            Vector2 absoluteCursor = point + windowTopLeft;

            float distance = ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(point, absoluteCursor, windowTopLeft);

            distance.Should().Be(0f);
        }

        [TestMethod]
        public void ShouldPreferHoveredEssenceLabel_PrefersOverlappingOrEssenceFallback()
        {
            ManualCursorSelectionMath.ShouldPreferHoveredEssenceLabel(true, true, false, true).Should().BeTrue();
            ManualCursorSelectionMath.ShouldPreferHoveredEssenceLabel(true, false, true, true).Should().BeTrue();
            ManualCursorSelectionMath.ShouldPreferHoveredEssenceLabel(true, false, false, true).Should().BeFalse();
            ManualCursorSelectionMath.ShouldPreferHoveredEssenceLabel(false, true, true, true).Should().BeFalse();
        }
    }
}