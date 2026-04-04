namespace ClickIt.Tests.Features.Click.Interaction
{
    [TestClass]
    public class InteractionExecutorTests
    {
        [TestMethod]
        public void ShouldSkipClickWhenNotLazyAndHotkeyInactive_ReturnsExpectedValues()
        {
            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: false,
                clickHotkeyActive: false).Should().BeTrue();

            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: false,
                clickHotkeyActive: true).Should().BeFalse();

            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: true,
                clickHotkeyActive: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSkipClickWhenNotLazyAndHotkeyInactive_ReturnsFalse_WhenExplicitOverrideEnabled()
        {
            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: false,
                clickHotkeyActive: false,
                allowWhenHotkeyInactive: true).Should().BeFalse();

            InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                lazyModeEnabled: true,
                clickHotkeyActive: false,
                allowWhenHotkeyInactive: true).Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow(false, false, 100UL, 0UL, false)]
        [DataRow(false, false, 100UL, 200UL, false)]
        [DataRow(false, true, 100UL, 100UL, false)]
        [DataRow(false, true, 100UL, 200UL, true)]
        [DataRow(false, true, 100UL, 0UL, true)]
        [DataRow(false, true, 0UL, 0UL, false)]
        [DataRow(true, false, 100UL, 100UL, false)]
        [DataRow(true, false, 100UL, 200UL, true)]
        [DataRow(true, false, 100UL, 0UL, true)]
        public void ShouldSkipClickDueToHoverMismatch_ReturnsExpectedResult(
            bool lazyModeEnabled,
            bool verifyUiHoverWhenNotLazy,
            ulong expectedAddress,
            ulong hoverAddress,
            bool expected)
        {
            bool result = InteractionExecutor.ShouldSkipClickDueToHoverMismatch(
                lazyModeEnabled,
                verifyUiHoverWhenNotLazy,
                expectedAddress,
                hoverAddress);

            result.Should().Be(expected);
        }

        [TestMethod]
        public void ShouldSkipClickDueToHoverMismatch_RespectsForcedVerification_WhenNotLazyAndSettingDisabled()
        {
            InteractionExecutor.ShouldSkipClickDueToHoverMismatch(
                lazyModeEnabled: false,
                verifyUiHoverWhenNotLazy: false,
                expectedAddress: 100UL,
                hoverAddress: 200UL,
                forceUiHoverVerification: true).Should().BeTrue();

            InteractionExecutor.ShouldSkipClickDueToHoverMismatch(
                lazyModeEnabled: false,
                verifyUiHoverWhenNotLazy: false,
                expectedAddress: 100UL,
                hoverAddress: 100UL,
                forceUiHoverVerification: true).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveClickExecutionPosition_UsesRequestedPosition_WhenCursorMoveAllowed()
        {
            var requested = new SharpDX.Vector2(123f, 456f);

            var resolved = InteractionExecutor.ResolveClickExecutionPosition(requested, avoidCursorMove: false);

            resolved.Should().Be(requested);
        }
    }
}