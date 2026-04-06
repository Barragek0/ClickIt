namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ClickLabelSelectionMathTests
    {
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
        public void IsInsideWindowInEitherSpace_AcceptsClientOrScreenCoordinates()
        {
            RectangleF window = new RectangleF(100, 200, 300, 200);

            ClickLabelSelectionMath.IsInsideWindowInEitherSpace(new Vector2(50, 50), window).Should().BeTrue();
            ClickLabelSelectionMath.IsInsideWindowInEitherSpace(new Vector2(150, 250), window).Should().BeTrue();
            ClickLabelSelectionMath.IsInsideWindowInEitherSpace(new Vector2(-1, 50), window).Should().BeFalse();
            ClickLabelSelectionMath.IsInsideWindowInEitherSpace(new Vector2(450, 450), window).Should().BeFalse();
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