using ClickIt.Services.Click.Application;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Services.Click
{
    [TestClass]
    public class OffscreenStickyTargetHandlerTests
    {
        [TestMethod]
        public void TryResolveStickyOffscreenTarget_ReturnsFalse_WhenNoStickyTargetAddressIsSet()
        {
            bool cleared = false;
            var handler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                GetStickyOffscreenTargetAddress: static () => 0,
                SetStickyOffscreenTargetAddress: _ => cleared = true,
                FindEntityByAddress: static _ => null,
                PerformPathingClick: static _ => false,
                IsClickableInEitherSpace: static (_, _) => false,
                ShouldSuppressPathfindingLabel: static _ => false,
                GetMechanicIdForLabel: static _ => null,
                TryResolveLabelClickPosition: static (_, _, _, _, _) => (false, default),
                ExecuteStickyLabelInteraction: static (_, _, _) => false,
                HoldDebugTelemetryAfterSuccess: static _ => { },
                MarkPendingChestOpenConfirmation: static (_, _) => { },
                InvalidateShrineCache: static () => { }));

            bool resolved = handler.TryResolveStickyOffscreenTarget(out var target);

            resolved.Should().BeFalse();
            target.Should().BeNull();
            cleared.Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveStickyOffscreenTarget_ClearsStickyAddress_WhenEntityCannotBeResolved()
        {
            long clearedAddress = -1;
            var handler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                GetStickyOffscreenTargetAddress: static () => 42,
                SetStickyOffscreenTargetAddress: value => clearedAddress = value,
                FindEntityByAddress: static _ => null,
                PerformPathingClick: static _ => false,
                IsClickableInEitherSpace: static (_, _) => false,
                ShouldSuppressPathfindingLabel: static _ => false,
                GetMechanicIdForLabel: static _ => null,
                TryResolveLabelClickPosition: static (_, _, _, _, _) => (false, default),
                ExecuteStickyLabelInteraction: static (_, _, _) => false,
                HoldDebugTelemetryAfterSuccess: static _ => { },
                MarkPendingChestOpenConfirmation: static (_, _) => { },
                InvalidateShrineCache: static () => { }));

            bool resolved = handler.TryResolveStickyOffscreenTarget(out var target);

            resolved.Should().BeFalse();
            target.Should().BeNull();
            clearedAddress.Should().Be(0);
        }
    }
}