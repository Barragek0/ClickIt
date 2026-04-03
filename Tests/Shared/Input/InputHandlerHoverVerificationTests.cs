using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Features.Click.Interaction;

namespace ClickIt.Tests.Common.Input
{
    [TestClass]
    public class InputHandlerHoverVerificationTests
    {
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
