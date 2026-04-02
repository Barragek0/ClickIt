using ClickIt.Services.Click.Runtime;
using ExileCore.PoEMemory.Elements;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Tests.Click
{
    [TestClass]
    public class ClickLabelSelectionMathTests
    {
        [TestMethod]
        public void ShouldContinuePathingForSpecialAltarLabel_RequiresAllGates()
        {
            ClickLabelSelectionMath.ShouldContinuePathingForSpecialAltarLabel(
                    walkTowardOffscreenLabelsEnabled: true,
                    hasBackingEntity: true,
                    isBackingEntityHidden: false,
                    hasClickableAltars: false)
                .Should()
                .BeTrue();

            ClickLabelSelectionMath.ShouldContinuePathingForSpecialAltarLabel(
                    walkTowardOffscreenLabelsEnabled: false,
                    hasBackingEntity: true,
                    isBackingEntityHidden: false,
                    hasClickableAltars: false)
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public void GetGroundLabelSearchLimit_ClampsNegativeValuesToZero()
        {
            ClickLabelSelectionMath.GetGroundLabelSearchLimit(-5).Should().Be(0);
            ClickLabelSelectionMath.GetGroundLabelSearchLimit(7).Should().Be(7);
        }

        [TestMethod]
        public void IndexOfLabelReference_UsesReferenceEqualityWithinBounds()
        {
            LabelOnGround a = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            LabelOnGround b = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            LabelOnGround c = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

            IReadOnlyList<LabelOnGround> labels = new[] { a, b, c };

            ClickLabelSelectionMath.IndexOfLabelReference(labels, b, start: 0, endExclusive: labels.Count)
                .Should()
                .Be(1);

            ClickLabelSelectionMath.IndexOfLabelReference(labels, a, start: 1, endExclusive: labels.Count)
                .Should()
                .Be(-1);
        }

        [TestMethod]
        public void IsLeverClickSuppressedByCooldown_HonorsIdentityAndWindow()
        {
            ClickLabelSelectionMath.IsLeverClickSuppressedByCooldown(
                    lastLeverKey: 42,
                    lastLeverClickTimestampMs: 1_000,
                    currentLeverKey: 42,
                    now: 1_200,
                    cooldownMs: 500)
                .Should()
                .BeTrue();

            ClickLabelSelectionMath.IsLeverClickSuppressedByCooldown(
                    lastLeverKey: 42,
                    lastLeverClickTimestampMs: 1_000,
                    currentLeverKey: 42,
                    now: 1_500,
                    cooldownMs: 500)
                .Should()
                .BeFalse();

            ClickLabelSelectionMath.IsLeverClickSuppressedByCooldown(
                    lastLeverKey: 7,
                    lastLeverClickTimestampMs: 1_000,
                    currentLeverKey: 8,
                    now: 1_200,
                    cooldownMs: 500)
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public void NullSafetyHelpers_ReturnFalseOrNull()
        {
            ClickLabelSelectionMath.IsEssenceLabel(null!).Should().BeFalse();
            ClickLabelSelectionMath.IsLeverLabel(null).Should().BeFalse();
            ClickLabelSelectionMath.FindLabelByAddress(new List<LabelOnGround>(), address: 123).Should().BeNull();
        }

        [TestMethod]
        public void ShouldAttemptSpecialEssenceCorruption_RequiresWindowAndClickable()
        {
            ClickLabelSelectionMath.ShouldAttemptSpecialEssenceCorruption(
                    corruptionPointInWindow: true,
                    corruptionPointClickable: true)
                .Should()
                .BeTrue();

            ClickLabelSelectionMath.ShouldAttemptSpecialEssenceCorruption(
                    corruptionPointInWindow: false,
                    corruptionPointClickable: true)
                .Should()
                .BeFalse();

            ClickLabelSelectionMath.ShouldAttemptSpecialEssenceCorruption(
                    corruptionPointInWindow: true,
                    corruptionPointClickable: false)
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public void IsInsideWindowInEitherSpace_AcceptsClientOrScreenCoordinates()
        {
            RectangleF window = new RectangleF(100, 200, 300, 200);

            ClickLabelSelectionMath.IsInsideWindowInEitherSpace(new Vector2(50, 50), window).Should().BeTrue();
            ClickLabelSelectionMath.IsInsideWindowInEitherSpace(new Vector2(150, 250), window).Should().BeTrue();
            ClickLabelSelectionMath.IsInsideWindowInEitherSpace(new Vector2(-1, 50), window).Should().BeFalse();
            ClickLabelSelectionMath.IsInsideWindowInEitherSpace(new Vector2(450, 450), window).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressPathfindingLabel_ReturnsTrue_WhenAnySuppressionSignalIsTrue()
        {
            ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(suppressLeverClick: true, suppressInactiveUltimatum: false).Should().BeTrue();
            ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(suppressLeverClick: false, suppressInactiveUltimatum: true).Should().BeTrue();
            ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(suppressLeverClick: false, suppressInactiveUltimatum: false).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveVisibleLabelsWithoutForcedCopy_ReturnsExpectedShape_ForListAndEnumerable()
        {
            LabelOnGround label = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

            IReadOnlyList<LabelOnGround>? fromList = ClickLabelSelectionMath.ResolveVisibleLabelsWithoutForcedCopy(new List<LabelOnGround> { label });
            fromList.Should().NotBeNull();
            fromList!.Count.Should().Be(1);

            IEnumerable<LabelOnGround> enumerableOnly = new[] { label }.Where(_ => true);
            IReadOnlyList<LabelOnGround>? fromEnumerable = ClickLabelSelectionMath.ResolveVisibleLabelsWithoutForcedCopy(enumerableOnly);
            fromEnumerable.Should().NotBeNull();
            fromEnumerable!.Count.Should().Be(1);

            ClickLabelSelectionMath.ResolveVisibleLabelsWithoutForcedCopy(new List<LabelOnGround>()).Should().BeNull();
            ClickLabelSelectionMath.ResolveVisibleLabelsWithoutForcedCopy(null).Should().BeNull();
        }
    }
}