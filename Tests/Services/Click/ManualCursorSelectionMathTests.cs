using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Tests.Click
{
    [TestClass]
    public class ManualCursorSelectionMathTests
    {
        [TestMethod]
        public void ShouldTreatManualCursorAsHoveringCandidate_ReturnsTrue_WhenEitherSignalIsTrue()
        {
            ManualCursorSelectionMath.ShouldTreatManualCursorAsHoveringCandidate(true, false).Should().BeTrue();
            ManualCursorSelectionMath.ShouldTreatManualCursorAsHoveringCandidate(false, true).Should().BeTrue();
            ManualCursorSelectionMath.ShouldTreatManualCursorAsHoveringCandidate(false, false).Should().BeFalse();
        }

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